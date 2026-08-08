using System.Net;
using System.Net.Http.Json;
using Loopholes.Contracts;
using Loopholes.Domain;

namespace Loopholes.Tests;

/// <summary>
/// Drives the API the way the Godot client will. These are the tests that prove
/// the server is actually authoritative: the client is never trusted for time,
/// price, or outcome.
/// </summary>
public class GameLoopTests(LoopholesApp app) : IClassFixture<LoopholesApp>
{
    [Fact]
    public async Task A_new_player_starts_neutral_and_full_of_energy()
    {
        var client = await app.NewPlayerAsync();
        var me = await client.GetFromJsonAsync<PlayerStateDto>("/api/v1/me", LoopholesApp.Json);

        Assert.NotNull(me);
        Assert.Equal(GameData.MaxEnergy, me.Energy.Current);
        Assert.Equal(GameData.TrustStart, me.Trust.Score);
        Assert.Equal(TrustTier.Unproven, me.Trust.Tier);
        Assert.Equal(GameData.StartingCash, me.Cash);
        Assert.Equal(LicenseStatus.NotOwned, me.License.Status);
        Assert.Equal(FreezeTier.None, me.Freeze.Tier);
    }

    [Fact]
    public async Task The_first_five_minutes_run_start_to_finish()
    {
        // Mirrors the onboarding in brief §16: instant hustle, then a timed one,
        // then a market trade, then the profile screen.
        var client = await app.NewPlayerAsync("Onboarding Co");
        app.Random.NeverTrigger();

        var started = await (await client.PostJsonAsync("/api/v1/jobs/start", new StartJobRequest("flip_crate")))
            .ReadAsync<ActiveJobDto>();

        app.Clock.AdvanceMinutes(1);

        var collected = await (await client.PostJsonAsync("/api/v1/jobs/collect", new CollectJobRequest(started.ActiveJobId)))
            .ReadAsync<JobResultDto>();

        Assert.InRange(collected.CashPaid, 5m, 8m);
        Assert.Equal(GameData.MaxEnergy - 1, collected.State.Energy.Current);

        var courier = await (await client.PostJsonAsync("/api/v1/jobs/start", new StartJobRequest("courier_run")))
            .ReadAsync<ActiveJobDto>();

        app.Clock.AdvanceMinutes(3);
        await (await client.PostJsonAsync("/api/v1/jobs/collect", new CollectJobRequest(courier.ActiveJobId)))
            .ReadAsync<JobResultDto>();

        var buy = await (await client.PostJsonAsync("/api/v1/market/buy", new TradeRequest("streaming_codes", 2)))
            .ReadAsync<TradeResultDto>();

        Assert.Equal(2, buy.Quantity);

        var sell = await (await client.PostJsonAsync("/api/v1/market/sell", new TradeRequest("streaming_codes", 2)))
            .ReadAsync<TradeResultDto>();

        Assert.True(sell.Net > 0);

        var stats = await client.GetFromJsonAsync<StatsDto>("/api/v1/stats", LoopholesApp.Json);
        Assert.Equal(2, stats!.JobsCompleted);
        Assert.True(stats.LifetimeCashEarned > 0);
    }

    [Fact]
    public async Task A_job_cannot_be_collected_before_its_timer_expires()
    {
        var client = await app.NewPlayerAsync();

        var started = await (await client.PostJsonAsync("/api/v1/jobs/start", new StartJobRequest("cook_books")))
            .ReadAsync<ActiveJobDto>();

        var error = await (await client.PostJsonAsync("/api/v1/jobs/collect", new CollectJobRequest(started.ActiveJobId)))
            .ReadErrorAsync();

        Assert.Equal("job_not_finished", error.Code);
    }

    [Fact]
    public async Task Starting_a_job_costs_energy_and_energy_regenerates_on_the_clock()
    {
        var client = await app.NewPlayerAsync();

        var before = await client.GetFromJsonAsync<PlayerStateDto>("/api/v1/me", LoopholesApp.Json);
        await client.PostJsonAsync("/api/v1/jobs/start", new StartJobRequest("late_shift_cleanup"));

        var spent = await client.GetFromJsonAsync<PlayerStateDto>("/api/v1/me", LoopholesApp.Json);
        Assert.Equal(before!.Energy.Current - 3, spent!.Energy.Current);

        app.Clock.AdvanceMinutes(GameData.EnergyRegenMinutes * 2);

        var regenerated = await client.GetFromJsonAsync<PlayerStateDto>("/api/v1/me", LoopholesApp.Json);
        Assert.Equal(spent.Energy.Current + 2, regenerated!.Energy.Current);
    }

    [Fact]
    public async Task Running_out_of_energy_blocks_the_next_job()
    {
        var client = await app.NewPlayerAsync();
        await app.EditPlayerAsync(client, p => p.EnergySnapshot = 0);

        var error = await (await client.PostJsonAsync("/api/v1/jobs/start", new StartJobRequest("courier_run")))
            .ReadErrorAsync();

        Assert.Equal("job_locked", error.Code);
    }

    [Fact]
    public async Task Operations_stay_locked_until_enough_hustles_are_done()
    {
        var client = await app.NewPlayerAsync();
        await app.EditPlayerAsync(client, p => p.Cash = 10_000m);

        var board = await client.GetFromJsonAsync<JobBoardDto>("/api/v1/jobs", LoopholesApp.Json);
        var importRun = board!.Available.First(j => j.Id == "import_run");

        Assert.False(importRun.Unlocked);
        Assert.Contains("hustles", importRun.LockedReason);

        await app.EditPlayerAsync(client, p => p.JobsCompleted = GameData.Tier2UnlockJobsCompleted);

        var unlocked = await client.GetFromJsonAsync<JobBoardDto>("/api/v1/jobs", LoopholesApp.Json);
        Assert.True(unlocked!.Available.First(j => j.Id == "import_run").Unlocked);
    }

    [Fact]
    public async Task The_payout_is_sealed_at_start_so_retrying_a_collect_cannot_reroll_it()
    {
        // This is the DeKrim price-refresh exploit, in job form. The second collect
        // must fail outright rather than hand out a fresh roll.
        var client = await app.NewPlayerAsync();
        app.Random.NeverTrigger();

        var started = await (await client.PostJsonAsync("/api/v1/jobs/start", new StartJobRequest("courier_run")))
            .ReadAsync<ActiveJobDto>();

        app.Clock.AdvanceMinutes(5);

        var first = await (await client.PostJsonAsync("/api/v1/jobs/collect", new CollectJobRequest(started.ActiveJobId)))
            .ReadAsync<JobResultDto>();

        var second = await (await client.PostJsonAsync("/api/v1/jobs/collect", new CollectJobRequest(started.ActiveJobId)))
            .ReadErrorAsync();

        Assert.Equal("job_not_running", second.Code);
        Assert.True(first.CashPaid > 0);
    }

    [Fact]
    public async Task A_failed_operation_roll_docks_the_payout_and_starts_a_minor_freeze()
    {
        var client = await app.NewPlayerAsync();
        app.Random.AlwaysTrigger();

        await app.EditPlayerAsync(client, p =>
        {
            p.Cash = 10_000m;
            p.JobsCompleted = GameData.Tier2UnlockJobsCompleted;
        });

        var started = await (await client.PostJsonAsync("/api/v1/jobs/start", new StartJobRequest("warehouse_clearance")))
            .ReadAsync<ActiveJobDto>();

        app.Clock.AdvanceHours(5);

        var result = await (await client.PostJsonAsync("/api/v1/jobs/collect", new CollectJobRequest(started.ActiveJobId)))
            .ReadAsync<JobResultDto>();

        Assert.True(result.RiskTriggered);
        Assert.Equal(FreezeTier.Minor, result.Freeze.Tier);
        Assert.Equal(GameData.TrustStart + GameData.TrustMinorFreezePenalty, result.State.Trust.Score);

        // 20% of the rolled payout is withheld.
        Assert.Contains("withheld", result.RiskOutcome);
    }

    [Fact]
    public async Task A_minor_freeze_locks_jobs_but_leaves_the_market_open()
    {
        var client = await app.NewPlayerAsync();
        await app.EditPlayerAsync(client, p =>
        {
            p.FreezeTier = FreezeTier.Minor;
            p.FreezeEndsAt = app.Clock.UtcNow.AddHours(4);
            p.Cash = 1000m;
        });

        var jobError = await (await client.PostJsonAsync("/api/v1/jobs/start", new StartJobRequest("courier_run")))
            .ReadErrorAsync();
        Assert.Equal("under_investigation", jobError.Code);

        var trade = await (await client.PostJsonAsync("/api/v1/market/buy", new TradeRequest("streaming_codes", 1)))
            .ReadAsync<TradeResultDto>();
        Assert.Equal(1, trade.Quantity);
    }

    [Fact]
    public async Task A_major_freeze_locks_the_market_too()
    {
        var client = await app.NewPlayerAsync();
        await app.EditPlayerAsync(client, p =>
        {
            p.FreezeTier = FreezeTier.Major;
            p.FreezeEndsAt = app.Clock.UtcNow.AddHours(24);
            p.Cash = 1000m;
        });

        var error = await (await client.PostJsonAsync("/api/v1/market/buy", new TradeRequest("streaming_codes", 1)))
            .ReadErrorAsync();

        Assert.Equal("under_investigation", error.Code);
    }

    [Fact]
    public async Task Paperwork_chips_away_at_a_freeze_and_respects_its_cooldown()
    {
        var client = await app.NewPlayerAsync();
        await app.EditPlayerAsync(client, p =>
        {
            p.FreezeTier = FreezeTier.Minor;
            p.FreezeEndsAt = app.Clock.UtcNow.AddHours(4);
            p.FreezeNextPaperworkAt = app.Clock.UtcNow;
        });

        var first = await (await client.PostJsonAsync("/api/v1/freeze/paperwork", new { }))
            .ReadAsync<FreezeActionResultDto>();

        Assert.Equal(GameData.MinorPaperworkReduction, first.TimeRemoved);

        var tooSoon = await (await client.PostJsonAsync("/api/v1/freeze/paperwork", new { })).ReadErrorAsync();
        Assert.Equal("paperwork_cooldown", tooSoon.Code);

        app.Clock.Advance(GameData.MinorPaperworkCooldown);

        var second = await (await client.PostJsonAsync("/api/v1/freeze/paperwork", new { }))
            .ReadAsync<FreezeActionResultDto>();
        Assert.Equal(GameData.MinorPaperworkReduction, second.TimeRemoved);
    }

    [Fact]
    public async Task A_lawyer_clears_a_freeze_outright_for_the_listed_fee()
    {
        var client = await app.NewPlayerAsync();
        await app.EditPlayerAsync(client, p =>
        {
            p.FreezeTier = FreezeTier.Minor;
            p.FreezeEndsAt = app.Clock.UtcNow.AddHours(4);
            p.Cash = 1000m;
        });

        var result = await (await client.PostJsonAsync("/api/v1/freeze/lawyer", new ClearFreezeRequest(false)))
            .ReadAsync<FreezeActionResultDto>();

        Assert.Equal(GameData.MinorLawyerCost, result.CostPaid);
        Assert.Equal(FreezeTier.None, result.Freeze.Tier);
        Assert.Equal(600m, result.State.Cash);
    }

    [Fact]
    public async Task A_partial_lawyer_halves_a_major_freeze_and_paperwork_finishes_the_job()
    {
        var client = await app.NewPlayerAsync();
        await app.EditPlayerAsync(client, p =>
        {
            p.FreezeTier = FreezeTier.Major;
            p.FreezeEndsAt = app.Clock.UtcNow.AddHours(24);
            p.FreezeNextPaperworkAt = app.Clock.UtcNow;
            p.Cash = 5000m;
        });

        var partial = await (await client.PostJsonAsync("/api/v1/freeze/lawyer", new ClearFreezeRequest(true)))
            .ReadAsync<FreezeActionResultDto>();

        Assert.Equal(GameData.MajorLawyerPartialCost, partial.CostPaid);
        Assert.Equal(TimeSpan.FromHours(12), partial.TimeRemoved);
        Assert.Equal(FreezeTier.Major, partial.Freeze.Tier);

        // Methods combine: keep filing on the remainder.
        var paperwork = await (await client.PostJsonAsync("/api/v1/freeze/paperwork", new { }))
            .ReadAsync<FreezeActionResultDto>();

        Assert.Equal(GameData.MajorPaperworkReduction, paperwork.TimeRemoved);
    }

    [Fact]
    public async Task A_freeze_expires_on_its_own_once_the_clock_runs_out()
    {
        var client = await app.NewPlayerAsync();
        await app.EditPlayerAsync(client, p =>
        {
            p.FreezeTier = FreezeTier.Minor;
            p.FreezeEndsAt = app.Clock.UtcNow.AddHours(4);
        });

        app.Clock.AdvanceHours(5);

        var me = await client.GetFromJsonAsync<PlayerStateDto>("/api/v1/me", LoopholesApp.Json);
        Assert.Equal(FreezeTier.None, me!.Freeze.Tier);
    }
}
