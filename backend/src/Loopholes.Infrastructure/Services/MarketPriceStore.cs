using Loopholes.Contracts;
using Loopholes.Domain;
using Microsoft.EntityFrameworkCore;

namespace Loopholes.Infrastructure.Services;

/// <summary>
/// Reads and seeds live market prices. Both the market endpoints and inventory
/// valuation go through here, so there is exactly one authoritative price per good.
/// </summary>
public sealed class MarketPriceStore(LoopholesDbContext db, IClock clock)
{
    public async Task<Dictionary<string, MarketGoodState>> GetStatesAsync(CancellationToken ct = default)
    {
        var states = await db.MarketGoods.ToDictionaryAsync(m => m.GoodId, ct);

        // Seed any good that has been added to GameData since the last run.
        var missing = GameData.Goods.Where(g => !states.ContainsKey(g.Id)).ToList();
        if (missing.Count > 0)
        {
            foreach (var good in missing)
            {
                var state = new MarketGoodState
                {
                    GoodId = good.Id,
                    CurrentPrice = good.BasePrice,
                    PriceOneHourAgo = good.BasePrice,
                    UpdatedAt = clock.UtcNow,
                };
                db.MarketGoods.Add(state);
                states[good.Id] = state;
            }

            await db.SaveChangesAsync(ct);
        }

        return states;
    }

    /// <summary>
    /// Price of one unit for trading purposes. Appreciating goods are priced from
    /// how long the specific stack has been held, not from the open market.
    /// </summary>
    public static decimal UnitPrice(GoodDefinition good, MarketGoodState state, TimeSpan? held = null) =>
        good.Behavior == PriceBehavior.Appreciating
            ? MarketMath.AppreciatedValue(good, good.BasePrice, held ?? TimeSpan.Zero)
            : state.CurrentPrice;

    public static double ChangePercent1h(MarketGoodState state) =>
        state.PriceOneHourAgo <= 0
            ? 0
            : (double)((state.CurrentPrice - state.PriceOneHourAgo) / state.PriceOneHourAgo);
}
