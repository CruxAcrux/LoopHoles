using Loopholes.Contracts;
using Loopholes.Domain;

namespace Loopholes.Infrastructure.Services;

/// <summary>
/// Advances market prices one tick. Kept free of EF and DI so a test can run a
/// week of ticks in a loop and assert the price band never breaks.
///
/// Three forces combine, in this order: scripted events (spikes, crackdowns,
/// exam seasons), aggregate player pressure from net buy/sell volume, and mean
/// reversion toward the good's base price.
/// </summary>
public static class MarketEngine
{
    public static void Tick(MarketGoodState state, GoodDefinition good, DateTimeOffset now, IRandomSource rng)
    {
        var priceBefore = state.CurrentPrice;

        ResolveEvent(state, good, now, rng);

        state.CurrentPrice = good.Behavior == PriceBehavior.Bubble
            ? BubblePrice(good, state, now)
            : MarketMath.NextPrice(good, state.CurrentPrice, state.NetVolumeSinceTick, rng.NextSigned());

        state.NetVolumeSinceTick = 0;
        state.UpdatedAt = now;

        // Keep a one-hour reference point so the client can show a change indicator.
        if (now - state.PriceOneHourAgoAt >= TimeSpan.FromHours(1))
        {
            state.PriceOneHourAgo = priceBefore;
            state.PriceOneHourAgoAt = now;
        }
    }

    /// <summary>Starts an event if one is due, and unwinds it (including the sneaker crash) when it ends.</summary>
    private static void ResolveEvent(MarketGoodState state, GoodDefinition good, DateTimeOffset now, IRandomSource rng)
    {
        if (state.ActiveEvent is not null)
        {
            if (state.EventEndsAt is { } ends && ends <= now)
            {
                // A scheduled spike doesn't just fade — it crashes on the way out.
                state.CurrentPrice = state.CrashPending
                    ? state.CurrentPrice * (decimal)good.CrashMultiplier
                    : state.CurrentPrice / (decimal)good.EventMultiplier;

                state.ActiveEvent = null;
                state.EventStartedAt = null;
                state.EventEndsAt = null;
                state.CrashPending = false;
            }

            return;
        }

        if (good.EventChancePerTick <= 0 || !rng.Chance(good.EventChancePerTick))
            return;

        state.CurrentPrice *= (decimal)good.EventMultiplier;
        state.ActiveEvent = good.EventName;
        state.EventStartedAt = now;
        state.EventEndsAt = now.AddHours(good.EventHours);
        state.CrashPending = good.Behavior == PriceBehavior.ScheduledSpike;
    }

    /// <summary>
    /// Bulk Influencer Followers ride a continuous sine bubble rather than discrete
    /// events, so the price is derived from the clock and then nudged by player
    /// pressure — the cycle is learnable, the exact peak is not.
    /// </summary>
    private static decimal BubblePrice(GoodDefinition good, MarketGoodState state, DateTimeOffset now)
    {
        var phase = (now.ToUnixTimeSeconds() % (long)(good.BubbleCycleMinutes * 60)) / (good.BubbleCycleMinutes * 60);
        var wave = Math.Sin(phase * 2 * Math.PI);

        var mid = (good.BubbleHighMultiplier + good.BubbleLowMultiplier) / 2;
        var amplitude = (good.BubbleHighMultiplier - good.BubbleLowMultiplier) / 2;
        var target = good.BasePrice * (decimal)(mid + (amplitude * wave));

        var pressure = Math.Clamp(
            state.NetVolumeSinceTick / GameData.MarketPressureDivisor,
            -GameData.MarketMaxPressurePerTick,
            GameData.MarketMaxPressurePerTick);

        var floor = good.BasePrice * (decimal)GameData.MarketFloorMultiplier;
        var ceiling = good.BasePrice * (decimal)GameData.MarketCeilingMultiplier;

        return Math.Round(Math.Clamp(target * (decimal)(1 + pressure), floor, ceiling), 2, MidpointRounding.AwayFromZero);
    }
}
