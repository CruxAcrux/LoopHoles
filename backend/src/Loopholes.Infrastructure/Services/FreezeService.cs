using Loopholes.Contracts;
using Loopholes.Domain;

namespace Loopholes.Infrastructure.Services;

/// <summary>
/// The Audit Freeze ("Under Investigation") — the top-tier consequence, themed as
/// a regulator freezing your accounts rather than a prison sentence (brief §11).
/// </summary>
public sealed class FreezeService(IClock clock, PlayerService players)
{
    public static TimeSpan DurationFor(FreezeTier tier) => tier switch
    {
        FreezeTier.Minor => GameData.MinorFreezeDuration,
        FreezeTier.Major => GameData.MajorFreezeDuration,
        _ => TimeSpan.Zero,
    };

    public static TimeSpan PaperworkReductionFor(FreezeTier tier) => tier switch
    {
        FreezeTier.Minor => GameData.MinorPaperworkReduction,
        FreezeTier.Major => GameData.MajorPaperworkReduction,
        _ => TimeSpan.Zero,
    };

    public static TimeSpan PaperworkCooldownFor(FreezeTier tier) => tier switch
    {
        FreezeTier.Minor => GameData.MinorPaperworkCooldown,
        FreezeTier.Major => GameData.MajorPaperworkCooldown,
        _ => TimeSpan.Zero,
    };

    public static int TrustPenaltyFor(FreezeTier tier) => tier switch
    {
        FreezeTier.Minor => GameData.TrustMinorFreezePenalty,
        FreezeTier.Major => GameData.TrustMajorFreezePenalty,
        _ => 0,
    };

    /// <summary>
    /// Puts the player under investigation. A freeze landing on an existing one
    /// takes the harsher tier and adds its duration on top, so stacking failures
    /// escalate instead of quietly resetting the clock.
    /// </summary>
    public void Apply(Player p, FreezeTier tier, string reason)
    {
        if (tier == FreezeTier.None) return;

        var now = clock.UtcNow;
        var addition = DurationFor(tier);
        var remaining = p.FreezeEndsAt is { } ends && ends > now ? ends - now : TimeSpan.Zero;

        p.FreezeTier = (FreezeTier)Math.Max((int)p.FreezeTier, (int)tier);
        p.FreezeEndsAt = now + remaining + addition;
        p.FreezeNextPaperworkAt = now;
        p.TimesFrozen++;

        players.ApplyTrust(p, TrustPenaltyFor(tier), TrustEventKind.AuditFreeze, $"{tier} Freeze: {reason}");
    }

    /// <summary>The free "keep busy" action — chips time off the freeze on a cooldown.</summary>
    public FreezeActionOutcome FilePaperwork(Player p)
    {
        RequireFrozen(p);

        var now = clock.UtcNow;
        if (p.FreezeNextPaperworkAt is { } next && next > now)
            throw new GameRuleException("paperwork_cooldown", $"More paperwork can be filed at {next:u}.");

        var reduction = PaperworkReductionFor(p.FreezeTier);
        var removed = Reduce(p, reduction);
        p.FreezeNextPaperworkAt = now + PaperworkCooldownFor(p.FreezeTier);

        return new FreezeActionOutcome("file_paperwork", 0m, removed);
    }

    /// <summary>Paid escape. Minor and Major clear outright; Major also has a cheaper half-off option.</summary>
    public FreezeActionOutcome HireLawyer(Player p, bool partial)
    {
        RequireFrozen(p);

        if (partial && p.FreezeTier != FreezeTier.Major)
            throw new GameRuleException("no_partial_option", "Partial representation is only offered on a Major Freeze.");

        var cost = (p.FreezeTier, partial) switch
        {
            (FreezeTier.Minor, _) => GameData.MinorLawyerCost,
            (FreezeTier.Major, false) => GameData.MajorLawyerCost,
            (FreezeTier.Major, true) => GameData.MajorLawyerPartialCost,
            _ => 0m,
        };

        PlayerService.SpendCash(p, cost);

        var now = clock.UtcNow;
        var remaining = p.FreezeEndsAt is { } ends && ends > now ? ends - now : TimeSpan.Zero;
        var reduction = partial ? remaining * GameData.MajorLawyerPartialReduction : remaining;
        var removed = Reduce(p, reduction);

        return new FreezeActionOutcome(partial ? "lawyer_partial" : "lawyer", cost, removed);
    }

    /// <summary>Takes time off the freeze, clearing it entirely if that empties the clock.</summary>
    private TimeSpan Reduce(Player p, TimeSpan amount)
    {
        var now = clock.UtcNow;
        var remaining = p.FreezeEndsAt is { } ends && ends > now ? ends - now : TimeSpan.Zero;
        var removed = amount >= remaining ? remaining : amount;

        if (removed >= remaining)
        {
            p.FreezeTier = FreezeTier.None;
            p.FreezeEndsAt = null;
            p.FreezeNextPaperworkAt = null;
        }
        else
        {
            p.FreezeEndsAt = now + (remaining - removed);
        }

        return removed;
    }

    private static void RequireFrozen(Player p)
    {
        if (p.FreezeTier == FreezeTier.None)
            throw new GameRuleException("not_frozen", "Not currently under investigation.");
    }
}

/// <summary>What a freeze-reduction action actually did, before the endpoint wraps it with player state.</summary>
public sealed record FreezeActionOutcome(string Action, decimal Cost, TimeSpan Removed);
