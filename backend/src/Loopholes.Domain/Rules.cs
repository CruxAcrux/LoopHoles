using Loopholes.Contracts;

namespace Loopholes.Domain;

/// <summary>
/// Lazy energy regeneration. Nothing schedules a tick — energy is derived from a
/// snapshot and a timestamp whenever the player is read, which is both cheap and
/// impossible to spoof from the client.
/// </summary>
public static class EnergyMath
{
    public static readonly TimeSpan RegenInterval = TimeSpan.FromMinutes(GameData.EnergyRegenMinutes);

    /// <summary>
    /// Advances a stored snapshot to <paramref name="now"/>. Returns the new value
    /// and the timestamp to store with it — partial progress toward the next point
    /// is preserved by advancing the timestamp only by whole intervals.
    /// </summary>
    public static (int Energy, DateTimeOffset UpdatedAt) Regenerate(int snapshot, DateTimeOffset updatedAt, DateTimeOffset now)
    {
        if (snapshot >= GameData.MaxEnergy)
            return (GameData.MaxEnergy, now);

        var elapsed = now - updatedAt;
        if (elapsed <= TimeSpan.Zero)
            return (snapshot, updatedAt);

        var earned = (int)(elapsed.Ticks / RegenInterval.Ticks);
        if (earned <= 0)
            return (snapshot, updatedAt);

        var gained = Math.Min(earned, GameData.MaxEnergy - snapshot);
        var energy = snapshot + gained;

        // At full, the clock restarts from now so downtime doesn't bank extra points.
        var stamp = energy >= GameData.MaxEnergy ? now : updatedAt + (RegenInterval * gained);
        return (energy, stamp);
    }

    public static DateTimeOffset? NextPointAt(int energy, DateTimeOffset updatedAt) =>
        energy >= GameData.MaxEnergy ? null : updatedAt + RegenInterval;

    public static DateTimeOffset? FullAt(int energy, DateTimeOffset updatedAt) =>
        energy >= GameData.MaxEnergy ? null : updatedAt + (RegenInterval * (GameData.MaxEnergy - energy));
}

public static class TrustMath
{
    public static int Clamp(int score) => Math.Clamp(score, GameData.TrustMin, GameData.TrustMax);

    public static TrustTier TierFor(int score) => GameData.TierFor(score);

    /// <summary>
    /// Idle decay: +1/day <em>toward</em> neutral, never past it. A saint decays down
    /// and a crook decays up, so an abandoned account drifts back to unremarkable.
    /// </summary>
    public static (int Score, DateTimeOffset DecayedAt) ApplyIdleDecay(int score, DateTimeOffset decayedAt, DateTimeOffset now)
    {
        var days = (int)((now - decayedAt).TotalDays);
        if (days <= 0 || score == GameData.TrustNeutral)
            return (score, days > 0 ? now : decayedAt);

        var step = GameData.TrustIdleDecayPerDay * days;
        var moved = score > GameData.TrustNeutral
            ? Math.Max(GameData.TrustNeutral, score - step)
            : Math.Min(GameData.TrustNeutral, score + step);

        return (moved, decayedAt.AddDays(days));
    }

    /// <summary>
    /// Reward for a completed honest deal. While Untrustworthy this is the larger
    /// clean-streak rate — an active way back up rather than waiting out decay.
    /// </summary>
    public static int HonestDealReward(int currentScore) =>
        TierFor(currentScore) == TrustTier.Untrustworthy
            ? GameData.TrustCleanStreakRecovery
            : GameData.TrustFairSplit;

    /// <summary>Cash owed to instantly clear an untrustworthy flag: 150% of what was taken.</summary>
    public static decimal RestitutionOwed(decimal amountEmbezzled) =>
        Math.Round(amountEmbezzled * GameData.RestitutionMultiplier, 2, MidpointRounding.AwayFromZero);
}

public static class TravelMath
{
    /// <summary>
    /// risk × (1 − (trust − 50) / 100). Higher trust lowers risk, lower trust raises
    /// it — the core link between the trust system and travel (brief §9).
    /// </summary>
    public static double AdjustedRisk(double baseRisk, int trustScore)
    {
        var adjusted = baseRisk * (1 - ((trustScore - GameData.TrustNeutral) / 100.0));
        return Math.Clamp(adjusted, 0, 1);
    }

    /// <summary>Car trips are 25% shorter with a licence; other transports are unaffected.</summary>
    public static TimeSpan Duration(Transport transport, bool hasLicense)
    {
        var def = GameData.Travel[transport];
        var minutes = def.DurationMinutes;
        if (transport == Transport.Car && hasLicense)
            minutes *= 1 - GameData.LicenseCarTimeReduction;
        return TimeSpan.FromMinutes(minutes);
    }
}

public static class MarketMath
{
    /// <summary>Value of one unit of an appreciating good given how long it has been held.</summary>
    public static decimal AppreciatedValue(GoodDefinition good, decimal basePrice, TimeSpan held)
    {
        if (good.AppreciationPerDay <= 0)
            return basePrice;

        var growth = Math.Min(good.AppreciationPerDay * held.TotalDays, good.AppreciationCap);
        return Math.Round(basePrice * (decimal)(1 + growth), 2, MidpointRounding.AwayFromZero);
    }

    public static double AppreciationPercent(GoodDefinition good, TimeSpan held) =>
        good.AppreciationPerDay <= 0
            ? 0
            : Math.Min(good.AppreciationPerDay * held.TotalDays, good.AppreciationCap);

    /// <summary>
    /// Price move for one tick. Combines player pressure (net volume), mean
    /// reversion toward base, and the good's own volatility, then clamps to the
    /// floor/ceiling band so nothing runs away.
    /// </summary>
    public static decimal NextPrice(GoodDefinition good, decimal current, int netVolume, double noise)
    {
        var pressure = Math.Clamp(
            netVolume / GameData.MarketPressureDivisor,
            -GameData.MarketMaxPressurePerTick,
            GameData.MarketMaxPressurePerTick);

        // Volatility is quoted per hour; scale it to the tick length.
        var tickShare = GameData.MarketTickInterval.TotalHours;
        var drift = good.HourlyVolatility * tickShare * noise;

        var moved = current * (decimal)(1 + pressure + drift);

        // Pull back toward base. A sub-cent pull would be lost to rounding and leave
        // the price permanently parked a few cents off base, so guarantee a full
        // cent of progress (or close the gap outright if it's smaller than that).
        var gap = good.BasePrice - moved;
        var reversion = gap * (decimal)GameData.MarketMeanReversion;

        if (reversion != 0 && Math.Abs(reversion) < 0.01m)
            reversion = Math.Abs(gap) < 0.01m ? gap : Math.Sign(gap) * 0.01m;

        moved += reversion;

        var floor = good.BasePrice * (decimal)GameData.MarketFloorMultiplier;
        var ceiling = good.BasePrice * (decimal)GameData.MarketCeilingMultiplier;
        return Math.Round(Math.Clamp(moved, floor, ceiling), 2, MidpointRounding.AwayFromZero);
    }

    public static decimal Fee(decimal gross) =>
        Math.Round(gross * GameData.TradingFeeRate, 2, MidpointRounding.AwayFromZero);
}

public static class ScoreMath
{
    /// <summary>
    /// Empire Score = net worth × trust-tier modifier. Rewards playing well
    /// <em>and</em> honestly rather than raw volume (brief §14).
    /// </summary>
    public static decimal EmpireScore(decimal cash, decimal inventoryValue, int trustScore)
    {
        var netWorth = cash + inventoryValue;
        var modifier = GameData.EmpireTrustModifier(GameData.TierFor(trustScore));
        return Math.Round(netWorth * modifier, 2, MidpointRounding.AwayFromZero);
    }
}
