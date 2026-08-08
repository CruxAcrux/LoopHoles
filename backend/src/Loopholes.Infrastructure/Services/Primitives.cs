namespace Loopholes.Infrastructure.Services;

/// <summary>
/// All game time comes from here, never from the client. Tests substitute a fake
/// clock to fast-forward timers without sleeping.
/// </summary>
public interface IClock
{
    DateTimeOffset UtcNow { get; }
}

public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}

/// <summary>
/// Every roll in the game — payouts, risk checks, exam results, market noise —
/// goes through this. It lives only on the server, which is the whole point:
/// the client is never told an outcome before it has been decided here.
/// </summary>
public interface IRandomSource
{
    /// <summary>Uniform double in [0, 1).</summary>
    double NextDouble();

    /// <summary>Uniform double in [-1, 1), for symmetric price noise.</summary>
    double NextSigned();

    /// <summary>Uniform integer in [minInclusive, maxExclusive).</summary>
    int NextInt(int minInclusive, int maxExclusive);

    /// <summary>Uniform money value in [min, max], rounded to cents.</summary>
    decimal NextMoney(decimal min, decimal max);

    /// <summary>True with the given probability.</summary>
    bool Chance(double probability);
}

public sealed class GameRandom : IRandomSource
{
    private readonly Random _random;

    public GameRandom() => _random = Random.Shared;

    public GameRandom(int seed) => _random = new Random(seed);

    public double NextDouble() => _random.NextDouble();

    public double NextSigned() => (_random.NextDouble() * 2) - 1;

    public int NextInt(int minInclusive, int maxExclusive) => _random.Next(minInclusive, maxExclusive);

    public decimal NextMoney(decimal min, decimal max)
    {
        if (max <= min) return Math.Round(min, 2, MidpointRounding.AwayFromZero);
        var value = min + ((max - min) * (decimal)_random.NextDouble());
        return Math.Round(value, 2, MidpointRounding.AwayFromZero);
    }

    public bool Chance(double probability) => _random.NextDouble() < probability;
}

/// <summary>
/// A rule the player broke — insufficient energy, a locked job, trading while
/// frozen. Expected and recoverable, so it maps to a 4xx with a stable code the
/// client can branch on rather than a stack trace.
/// </summary>
public sealed class GameRuleException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}
