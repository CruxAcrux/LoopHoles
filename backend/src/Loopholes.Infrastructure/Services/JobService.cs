using Loopholes.Contracts;
using Loopholes.Domain;

namespace Loopholes.Infrastructure.Services;

/// <summary>
/// Jobs: the core loop. Spend energy, wait out a timer, come back and collect.
///
/// The payout and the risk roll are both decided when the job <em>starts</em> and
/// stored on the row, not computed at collect time. That closes the re-roll
/// exploit — a player who dislikes a result cannot retry the collect call to get
/// a different one, and the client is never in a position to influence either.
/// </summary>
public sealed class JobService(
    LoopholesDbContext db,
    IClock clock,
    IRandomSource rng,
    PlayerService players,
    MarketPriceStore prices,
    FreezeService freezes)
{
    public IReadOnlyList<JobCatalogEntryDto> Catalog(Player p) =>
    [
        .. GameData.Jobs.Select(job =>
        {
            var locked = LockReason(p, job);
            return new JobCatalogEntryDto(
                job.Id, job.Name, job.Tier, job.DurationMinutes, job.EnergyCost, job.CashCost,
                job.PayoutMin, job.PayoutMax, job.RiskChance, job.RiskDescription, job.Notes,
                locked is null, locked);
        })
    ];

    /// <summary>Why this job can't be started right now, or null if it can.</summary>
    private static string? LockReason(Player p, JobDefinition job)
    {
        if (job.Tier == JobTier.Operation && p.JobsCompleted < GameData.Tier2UnlockJobsCompleted)
            return $"Complete {GameData.Tier2UnlockJobsCompleted - p.JobsCompleted} more hustles to unlock Operations.";

        if (job.CashCost > p.Cash)
            return $"Needs ${job.CashCost:0.00} up front.";

        if (job.EnergyCost > p.EnergySnapshot)
            return $"Needs {job.EnergyCost} energy.";

        if (job.Effect == JobEffect.RequiresHeldGood && !p.Inventory.Any(s => s.Quantity > 0 && Tradeable(s.GoodId)))
            return "Needs a market good in inventory to sell.";

        return null;
    }

    private static bool Tradeable(string goodId) => GameData.FindGood(goodId)?.Tradeable ?? false;

    public IReadOnlyList<ActiveJobDto> Active(Player p)
    {
        var now = clock.UtcNow;
        return [.. p.ActiveJobs
            .OrderBy(j => j.CompletesAt)
            .Select(j => new ActiveJobDto(
                j.Id.ToString(),
                j.JobId,
                GameData.FindJob(j.JobId)?.Name ?? j.JobId,
                j.StartedAt,
                j.CompletesAt,
                j.CompletesAt <= now))];
    }

    public ActiveJobDto Start(Player p, string jobId)
    {
        PlayerService.RequireNotFrozen(p, FreezeGate.Jobs);

        var job = GameData.FindJob(jobId)
            ?? throw new GameRuleException("unknown_job", $"No job '{jobId}'.");

        if (LockReason(p, job) is { } locked)
            throw new GameRuleException("job_locked", locked);

        if (p.ActiveJobs.Any(a => a.JobId == jobId))
            throw new GameRuleException("job_already_running", $"{job.Name} is already in progress.");

        players.SpendEnergy(p, job.EnergyCost);
        PlayerService.SpendCash(p, job.CashCost);

        var now = clock.UtcNow;
        var active = new ActiveJob
        {
            PlayerId = p.Id,
            JobId = job.Id,
            StartedAt = now,
            CompletesAt = now + job.Duration,
            RolledPayout = rng.NextMoney(job.PayoutMin, job.PayoutMax),
            RiskTriggered = job.RiskChance > 0 && rng.Chance(job.RiskChance),
        };

        p.ActiveJobs.Add(active);
        db.ActiveJobs.Add(active);

        return new ActiveJobDto(active.Id.ToString(), job.Id, job.Name, active.StartedAt, active.CompletesAt, false);
    }

    public async Task<JobResultDto> CollectAsync(Player p, Guid activeJobId, CancellationToken ct = default)
    {
        PlayerService.RequireNotFrozen(p, FreezeGate.Jobs);

        var active = p.ActiveJobs.FirstOrDefault(j => j.Id == activeJobId)
            ?? throw new GameRuleException("job_not_running", "That job isn't running.");

        var job = GameData.FindJob(active.JobId)
            ?? throw new GameRuleException("unknown_job", $"No job '{active.JobId}'.");

        var now = clock.UtcNow;
        var states = await prices.GetStatesAsync(ct);

        var payout = active.RolledPayout;
        var granted = new List<ItemGrantDto>();
        var lost = new List<ItemGrantDto>();
        string? riskOutcome = null;

        if (job.Effect == JobEffect.PaysAcrossDuration)
        {
            payout = AccruedSoFar(job, active, now);
            if (payout <= 0)
                throw new GameRuleException("nothing_accrued", "Nothing has accrued yet.");
        }
        else if (active.CompletesAt > now)
        {
            throw new GameRuleException("job_not_finished", $"{job.Name} finishes at {active.CompletesAt:u}.");
        }

        payout = ApplyEffects(p, job, payout, states, granted, lost);

        if (active.RiskTriggered)
        {
            riskOutcome = ApplyRisk(p, job, ref payout, states, lost);
            freezes.Apply(p, FreezeTier.Minor, $"{job.Name} spot check");
        }

        payout = Math.Round(payout, 2, MidpointRounding.AwayFromZero);
        PlayerService.EarnCash(p, payout);

        var finished = job.Effect != JobEffect.PaysAcrossDuration || active.CompletesAt <= now;
        if (finished)
        {
            p.ActiveJobs.Remove(active);
            db.ActiveJobs.Remove(active);
            p.JobsCompleted++;
        }
        else
        {
            active.CollectedSoFar += payout;
        }

        return new JobResultDto(
            job.Id, job.Name, payout, granted, lost,
            active.RiskTriggered, riskOutcome,
            PlayerService.FreezeDto(p),
            players.ToDto(p, states));
    }

    /// <summary>Pop-up Storefront pays continuously, so a mid-run collect takes only what has accrued.</summary>
    private static decimal AccruedSoFar(JobDefinition job, ActiveJob active, DateTimeOffset now)
    {
        var elapsed = now - active.StartedAt;
        var share = Math.Clamp(elapsed.TotalMinutes / job.DurationMinutes, 0, 1);
        var earned = active.RolledPayout * (decimal)share;
        return Math.Round(Math.Max(0, earned - active.CollectedSoFar), 2, MidpointRounding.AwayFromZero);
    }

    /// <summary>Per-job special behaviour: item grants, market-linked payouts, held-time bonuses.</summary>
    private decimal ApplyEffects(
        Player p,
        JobDefinition job,
        decimal payout,
        IReadOnlyDictionary<string, MarketGoodState> states,
        List<ItemGrantDto> granted,
        List<ItemGrantDto> lost)
    {
        var now = clock.UtcNow;

        switch (job.Effect)
        {
            case JobEffect.GrantsItem when job.GrantsGoodId is { } goodId:
                players.AddItem(p, goodId, job.GrantsQuantity);
                granted.Add(new ItemGrantDto(goodId, GameData.FindGood(goodId)?.Name ?? goodId, job.GrantsQuantity));
                break;

            case JobEffect.RequiresHeldGood:
            {
                // Sells the single most valuable unit on hand and adds it to the payout.
                var best = p.Inventory
                    .Where(s => s.Quantity > 0 && Tradeable(s.GoodId))
                    .Select(s => (Stack: s, Price: UnitPriceOf(s, states, now)))
                    .OrderByDescending(x => x.Price)
                    .FirstOrDefault();

                if (best.Stack is not null)
                {
                    players.RemoveItem(p, best.Stack.GoodId, 1);
                    lost.Add(new ItemGrantDto(best.Stack.GoodId, NameOf(best.Stack.GoodId), 1));
                    payout += best.Price;
                }

                break;
            }

            case JobEffect.ClearsRandomItem:
            {
                var held = p.Inventory.Where(s => s.Quantity > 0).ToList();
                if (held.Count > 0)
                {
                    var stack = held[rng.NextInt(0, held.Count)];
                    players.RemoveItem(p, stack.GoodId, 1);
                    lost.Add(new ItemGrantDto(stack.GoodId, NameOf(stack.GoodId), 1));
                }

                break;
            }

            case JobEffect.MarketLinkedPayout when job.MarketLinkGoodId is { } linked:
            {
                // Smuggling is worth more when chips are hot and less when they aren't,
                // bounded so a market spike can't turn one job into the whole economy.
                var good = GameData.FindGood(linked);
                if (good is not null && states.TryGetValue(linked, out var state))
                {
                    var ratio = Math.Clamp(state.CurrentPrice / good.BasePrice, 0.70m, 1.50m);
                    payout *= ratio;
                }

                break;
            }

            case JobEffect.PerDayHeldBonus when job.MarketLinkGoodId is { } linked:
            {
                var stack = p.Inventory.FirstOrDefault(s => s.GoodId == linked && s.Quantity > 0);
                if (stack is not null)
                {
                    var days = Math.Clamp((int)(now - stack.AcquiredAt).TotalDays, 0, GameData.VintageFlipMaxDays);
                    payout += GameData.VintageFlipPerDay * days;
                }

                break;
            }

            case JobEffect.PaperworkDiscount:
                p.HasPaperworkDiscount = true;
                break;
        }

        return payout;
    }

    /// <summary>Consequence of a failed risk roll, decided at start time and revealed here.</summary>
    private string ApplyRisk(
        Player p,
        JobDefinition job,
        ref decimal payout,
        IReadOnlyDictionary<string, MarketGoodState> states,
        List<ItemGrantDto> lost)
    {
        switch (job.RiskEffect)
        {
            case RiskEffect.LosePayoutShare:
            {
                var fine = Math.Round(payout * (decimal)job.RiskMagnitude, 2, MidpointRounding.AwayFromZero);
                payout -= fine;
                return $"Spot check: ${fine:0.00} of the payout withheld.";
            }

            case RiskEffect.LoseGoodsShare:
            {
                var seized = 0;
                foreach (var stack in p.Inventory.Where(s => s.Quantity > 0).ToList())
                {
                    var take = (int)Math.Ceiling(stack.Quantity * job.RiskMagnitude);
                    if (take <= 0) continue;

                    var goodId = stack.GoodId;
                    players.RemoveItem(p, goodId, take);
                    lost.Add(new ItemGrantDto(goodId, NameOf(goodId), take));
                    seized += take;
                }

                return seized > 0
                    ? $"Crackdown: {seized} units seized."
                    : "Crackdown: nothing on hand to seize.";
            }

            default:
                return "Flagged for review.";
        }
    }

    private static decimal UnitPriceOf(InventoryStack stack, IReadOnlyDictionary<string, MarketGoodState> states, DateTimeOffset now)
    {
        var good = GameData.FindGood(stack.GoodId);
        if (good is null || !states.TryGetValue(stack.GoodId, out var state)) return 0m;
        return MarketPriceStore.UnitPrice(good, state, now - stack.AcquiredAt);
    }

    private static string NameOf(string goodId) => GameData.FindGood(goodId)?.Name ?? goodId;
}
