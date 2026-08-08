using Loopholes.Infrastructure.Services;

namespace Loopholes.Tests;

/// <summary>A clock the test drives by hand, so multi-hour timers cost no wall time.</summary>
public sealed class FakeClock : IClock
{
    public DateTimeOffset UtcNow { get; set; } = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    public void Advance(TimeSpan amount) => UtcNow += amount;

    public void AdvanceMinutes(double minutes) => Advance(TimeSpan.FromMinutes(minutes));

    public void AdvanceHours(double hours) => Advance(TimeSpan.FromHours(hours));
}

/// <summary>
/// A random source with the dice replaced by a dial. <see cref="Roll"/> is what
/// every probability check sees, so a test can force "always caught" or
/// "never caught" without touching production code.
/// </summary>
public sealed class FixedRandom : IRandomSource
{
    /// <summary>Value returned by NextDouble; Chance(p) is true when this is below p.</summary>
    public double Roll { get; set; } = 0.5;

    /// <summary>Value returned by NextSigned, for price noise.</summary>
    public double Signed { get; set; }

    /// <summary>Index returned by NextInt, clamped into range.</summary>
    public int Index { get; set; }

    public double NextDouble() => Roll;

    public double NextSigned() => Signed;

    public int NextInt(int minInclusive, int maxExclusive) =>
        Math.Clamp(minInclusive + Index, minInclusive, Math.Max(minInclusive, maxExclusive - 1));

    public decimal NextMoney(decimal min, decimal max) =>
        Math.Round(min + ((max - min) * (decimal)Roll), 2, MidpointRounding.AwayFromZero);

    public bool Chance(double probability) => Roll < probability;

    /// <summary>Makes every risk check fail the player.</summary>
    public FixedRandom AlwaysTrigger()
    {
        Roll = 0;
        return this;
    }

    /// <summary>Makes every risk check pass the player.</summary>
    public FixedRandom NeverTrigger()
    {
        Roll = 0.999999;
        return this;
    }
}
