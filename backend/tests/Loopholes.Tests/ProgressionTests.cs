using System.Net.Http.Json;
using Loopholes.Contracts;
using Loopholes.Domain;

namespace Loopholes.Tests;

/// <summary>Licence, travel, market, inventory, leaderboard and wardrobe behaviour end to end.</summary>
public class ProgressionTests(LoopholesApp app) : IClassFixture<LoopholesApp>
{
    // --- Driver's licence --------------------------------------------------

    [Fact]
    public async Task The_legit_path_costs_cash_and_time_and_then_passes()
    {
        var client = await app.NewPlayerAsync("Studious Ltd");
        app.Random.Roll = 0.5; // Inside the 95% pass rate.

        await app.EditPlayerAsync(client, p => p.Cash = 1000m);

        var studying = await (await client.PostJsonAsync("/api/v1/license/study", new { }))
            .ReadAsync<PlayerStateDto>();

        Assert.Equal(LicenseStatus.Studying, studying.License.Status);
        Assert.Equal(500m, studying.Cash);

        var tooSoon = await (await client.PostJsonAsync("/api/v1/license/exam", new TakeExamRequest(false)))
            .ReadErrorAsync();
        Assert.Equal("still_studying", tooSoon.Code);

        app.Clock.AdvanceHours(GameData.LicenseStudyHours + 0.1);

        var exam = await (await client.PostJsonAsync("/api/v1/license/exam", new TakeExamRequest(false)))
            .ReadAsync<ExamResultDto>();

        Assert.True(exam.Passed);
        Assert.False(exam.Cheated);
        Assert.Equal(LicenseStatus.Owned, exam.License.Status);
        Assert.False(exam.License.ObtainedByCheating);
    }

    [Fact]
    public async Task Cheating_and_getting_away_with_it_grants_the_licence_immediately()
    {
        var client = await app.NewPlayerAsync("Bold Ventures");
        app.Random.NeverTrigger(); // 0.999 < 0.35 is false → not caught.

        await app.EditPlayerAsync(client, p => p.Cash = 1000m);

        var exam = await (await client.PostJsonAsync("/api/v1/license/exam", new TakeExamRequest(true)))
            .ReadAsync<ExamResultDto>();

        Assert.True(exam.Passed);
        Assert.False(exam.Caught);
        Assert.Equal(LicenseStatus.Owned, exam.License.Status);
        Assert.True(exam.License.ObtainedByCheating);
    }

    [Fact]
    public async Task Getting_caught_cheating_fines_freezes_and_bars_a_retry_for_48_hours()
    {
        var client = await app.NewPlayerAsync("Sloppy Holdings");
        app.Random.AlwaysTrigger(); // 0 < 0.35 → caught.

        await app.EditPlayerAsync(client, p => p.Cash = 2000m);

        var exam = await (await client.PostJsonAsync("/api/v1/license/exam", new TakeExamRequest(true)))
            .ReadAsync<ExamResultDto>();

        Assert.False(exam.Passed);
        Assert.True(exam.Caught);
        Assert.Equal(GameData.LicenseCaughtFine, exam.FinePaid);
        Assert.Equal(LicenseStatus.Suspended, exam.License.Status);
        Assert.Equal(FreezeTier.Minor, exam.Freeze.Tier);

        // 2000 − 500 licence fee − 300 fine.
        Assert.Equal(1200m, exam.State.Cash);

        var barred = await (await client.PostJsonAsync("/api/v1/license/exam", new TakeExamRequest(true)))
            .ReadErrorAsync();
        Assert.Equal("under_investigation", barred.Code);

        app.Clock.AdvanceHours(GameData.LicenseCaughtCooldownHours + 1);

        var me = await client.GetFromJsonAsync<PlayerStateDto>("/api/v1/me", LoopholesApp.Json);
        Assert.Equal(LicenseStatus.NotOwned, me!.License.Status);
    }

    [Fact]
    public async Task Cheating_never_touches_the_interpersonal_trust_score()
    {
        // Brief §10: the licence is a contained, local consequence. The freeze it
        // triggers costs trust; being a cheat, by itself, does not.
        var client = await app.NewPlayerAsync("Contained Risk AB");
        app.Random.AlwaysTrigger();

        await app.EditPlayerAsync(client, p => p.Cash = 2000m);
        await client.PostJsonAsync("/api/v1/license/exam", new TakeExamRequest(true));

        var stats = await client.GetFromJsonAsync<StatsDto>("/api/v1/stats", LoopholesApp.Json);

        Assert.All(stats!.TrustHistory, entry => Assert.Equal(TrustEventKind.AuditFreeze, entry.Kind));
        Assert.DoesNotContain(stats.TrustHistory, e => e.Kind == TrustEventKind.Betrayal);
    }

    // --- Travel ------------------------------------------------------------

    [Fact]
    public async Task Travel_options_are_quoted_at_the_players_own_trust_adjusted_risk()
    {
        var client = await app.NewPlayerAsync("Frequent Flyer");
        await app.EditPlayerAsync(client, p => p.TrustScore = 72);

        var options = await client.GetFromJsonAsync<List<TravelOptionDto>>("/api/v1/travel/options", LoopholesApp.Json);
        var car = options!.First(o => o.Transport == Transport.Car);

        Assert.Equal(0.08, car.BaseRiskPercent, 6);
        Assert.Equal(0.0624, car.AdjustedRiskPercent, 6);
        Assert.False(car.LicenseDiscountApplied);
    }

    [Fact]
    public async Task A_licence_shortens_the_quoted_car_trip()
    {
        var client = await app.NewPlayerAsync("Licensed Logistics");
        await app.EditPlayerAsync(client, p => p.LicenseStatus = LicenseStatus.Owned);

        var options = await client.GetFromJsonAsync<List<TravelOptionDto>>("/api/v1/travel/options", LoopholesApp.Json);
        var car = options!.First(o => o.Transport == Transport.Car);

        Assert.Equal(90, car.DurationMinutes);
        Assert.True(car.LicenseDiscountApplied);
    }

    [Fact]
    public async Task A_failed_travel_roll_produces_exactly_one_consequence()
    {
        var client = await app.NewPlayerAsync("Unlucky Freight");
        app.Random.AlwaysTrigger(); // Fails the risk roll, then takes the cargo-fine branch.

        await app.EditPlayerAsync(client, p => p.Cash = 1000m);

        var result = await (await client.PostJsonAsync("/api/v1/travel/start", new StartTravelRequest(Transport.Jet, "Zug")))
            .ReadAsync<TravelResultDto>();

        Assert.True(result.RiskTriggered);
        Assert.NotNull(result.Consequence);

        var second = await (await client.PostJsonAsync("/api/v1/travel/start", new StartTravelRequest(Transport.Car, "Zug")))
            .ReadErrorAsync();
        Assert.Equal("already_travelling", second.Code);
    }

    // --- Market ------------------------------------------------------------

    [Fact]
    public async Task Buying_charges_the_fee_on_top_and_selling_takes_it_off()
    {
        var client = await app.NewPlayerAsync("Fee Payer");
        await app.EditPlayerAsync(client, p => p.Cash = 10_000m);

        var quote = await client.GetFromJsonAsync<MarketQuoteDto>("/api/v1/market", LoopholesApp.Json);
        var codes = quote!.Goods.First(g => g.Id == "streaming_codes");

        var buy = await (await client.PostJsonAsync("/api/v1/market/buy", new TradeRequest("streaming_codes", 10)))
            .ReadAsync<TradeResultDto>();

        Assert.Equal(codes.CurrentPrice, buy.UnitPrice);
        Assert.Equal(buy.Gross * GameData.TradingFeeRate, buy.Fee);
        Assert.Equal(-(buy.Gross + buy.Fee), buy.Net);

        var sell = await (await client.PostJsonAsync("/api/v1/market/sell", new TradeRequest("streaming_codes", 10)))
            .ReadAsync<TradeResultDto>();

        Assert.Equal(sell.Gross - sell.Fee, sell.Net);

        // The round trip is a net loss at an unchanged price — the fee is a real sink.
        Assert.True(sell.Net < buy.Gross);
    }

    [Fact]
    public async Task Selling_something_you_do_not_hold_is_rejected()
    {
        var client = await app.NewPlayerAsync("Naked Short");

        var error = await (await client.PostJsonAsync("/api/v1/market/sell", new TradeRequest("grey_chips", 5)))
            .ReadErrorAsync();

        Assert.Equal("not_held", error.Code);
    }

    [Fact]
    public async Task Vintage_furniture_cannot_be_bought_on_the_open_market()
    {
        // It is only obtainable from the Furniture Pickup job, so buy-and-hold
        // can't be farmed into a risk-free +30%.
        var client = await app.NewPlayerAsync("Antiques Inc");
        await app.EditPlayerAsync(client, p => p.Cash = 10_000m);

        var error = await (await client.PostJsonAsync("/api/v1/market/buy", new TradeRequest("vintage_furniture", 1)))
            .ReadErrorAsync();

        Assert.Equal("not_purchasable", error.Code);
    }

    [Fact]
    public async Task Furniture_earned_from_a_job_appreciates_while_it_is_held()
    {
        var client = await app.NewPlayerAsync("Patient Capital");
        app.Random.NeverTrigger();

        var started = await (await client.PostJsonAsync("/api/v1/jobs/start", new StartJobRequest("furniture_pickup")))
            .ReadAsync<ActiveJobDto>();

        app.Clock.AdvanceMinutes(46);

        var result = await (await client.PostJsonAsync("/api/v1/jobs/collect", new CollectJobRequest(started.ActiveJobId)))
            .ReadAsync<JobResultDto>();

        Assert.Contains(result.ItemsGranted, g => g.GoodId == "vintage_furniture" && g.Quantity == 1);

        var fresh = await client.GetFromJsonAsync<InventoryDto>("/api/v1/inventory", LoopholesApp.Json);
        var atPickup = fresh!.Stacks.First(s => s.GoodId == "vintage_furniture").UnitValue;

        app.Clock.Advance(TimeSpan.FromDays(5));

        var aged = await client.GetFromJsonAsync<InventoryDto>("/api/v1/inventory", LoopholesApp.Json);
        var stack = aged!.Stacks.First(s => s.GoodId == "vintage_furniture");

        Assert.Equal(69.00m, stack.UnitValue);
        Assert.True(stack.UnitValue > atPickup);
        Assert.Equal(0.15, stack.AppreciationPercent!.Value, 6);
        Assert.Equal(5, stack.DaysHeld);
    }

    [Fact]
    public async Task Storage_is_capped_so_holding_is_a_real_decision()
    {
        var client = await app.NewPlayerAsync("Overstocked");
        await app.EditPlayerAsync(client, p =>
        {
            p.Cash = 1_000_000m;
            p.InventoryCap = 5;
        });

        await client.PostJsonAsync("/api/v1/market/buy", new TradeRequest("streaming_codes", 5));

        var error = await (await client.PostJsonAsync("/api/v1/market/buy", new TradeRequest("streaming_codes", 1)))
            .ReadErrorAsync();

        Assert.Equal("inventory_full", error.Code);
    }

    // --- Leaderboard and wardrobe -----------------------------------------

    [Fact]
    public async Task The_leaderboard_ranks_by_empire_score_and_shows_the_trust_badge()
    {
        var rich = await app.NewPlayerAsync("Big Numbers AB");
        var poor = await app.NewPlayerAsync("Small Fry AB");

        await app.EditPlayerAsync(rich, p => { p.Cash = 100_000m; p.TrustScore = 95; });
        await app.EditPlayerAsync(poor, p => { p.Cash = 100m; p.TrustScore = 20; });

        // A read is enough to fold each player's standing into the season.
        await rich.GetAsync("/api/v1/me");
        await poor.GetAsync("/api/v1/me");
        await rich.PostJsonAsync("/api/v1/market/buy", new TradeRequest("streaming_codes", 1));
        await poor.PostJsonAsync("/api/v1/market/buy", new TradeRequest("streaming_codes", 1));

        var board = await rich.GetFromJsonAsync<LeaderboardDto>("/api/v1/leaderboard?mode=Empire", LoopholesApp.Json);

        Assert.NotNull(board);
        Assert.Equal(1, board.SeasonNumber);
        Assert.NotEmpty(board.Entries);

        var top = board.Entries.First();
        Assert.Equal("Big Numbers AB", top.DisplayName);
        Assert.Equal(TrustTier.Elite, top.TrustTier);
        Assert.NotNull(board.You);
    }

    [Fact]
    public async Task The_most_trusted_board_is_an_alternate_path_to_the_top()
    {
        var honest = await app.NewPlayerAsync("Squeaky Clean AB");
        await app.EditPlayerAsync(honest, p => { p.Cash = 1m; p.TrustScore = 100; });
        await honest.GetAsync("/api/v1/me");

        var board = await honest.GetFromJsonAsync<LeaderboardDto>("/api/v1/leaderboard?mode=MostTrusted", LoopholesApp.Json);

        Assert.Equal(LeaderboardMode.MostTrusted, board!.Mode);
        Assert.Equal(100m, board.Entries.First().Score);
    }

    [Fact]
    public async Task The_wardrobe_opens_with_a_starter_piece_already_equipped()
    {
        var client = await app.NewPlayerAsync("Well Dressed");
        var wardrobe = await client.GetFromJsonAsync<WardrobeDto>("/api/v1/wardrobe", LoopholesApp.Json);

        var starter = wardrobe!.Items.First(i => i.Id == "starter_suit");
        Assert.True(starter.Owned);
        Assert.True(starter.Equipped);
        Assert.Equal(0, starter.FavorCost);
    }

    [Fact]
    public async Task Cosmetics_cost_favor_never_cash()
    {
        var client = await app.NewPlayerAsync("Fashion Forward");
        await app.EditPlayerAsync(client, p => { p.Favor = 500; p.Cash = 12m; });

        var wardrobe = await (await client.PostJsonAsync("/api/v1/wardrobe/buy", new CosmeticRequest("shades_noir")))
            .ReadAsync<WardrobeDto>();

        Assert.Equal(350, wardrobe.Favor);
        Assert.True(wardrobe.Items.First(i => i.Id == "shades_noir").Owned);

        var state = await client.GetFromJsonAsync<PlayerStateDto>("/api/v1/me", LoopholesApp.Json);
        Assert.Equal(12m, state!.Cash);
    }

    [Fact]
    public async Task Buying_a_cosmetic_without_enough_favor_is_rejected()
    {
        var client = await app.NewPlayerAsync("Aspirational");

        var error = await (await client.PostJsonAsync("/api/v1/wardrobe/buy", new CosmeticRequest("car_skin_matte")))
            .ReadErrorAsync();

        Assert.Equal("insufficient_favor", error.Code);
    }

    // --- Auth --------------------------------------------------------------

    [Fact]
    public async Task An_unauthenticated_request_is_rejected()
    {
        var client = app.CreateClient();
        var response = await client.GetAsync("/api/v1/me");

        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
