using Loopholes.Contracts;
using Loopholes.Domain;

namespace Loopholes.Infrastructure.Services;

/// <summary>
/// Buying and selling. Prices come from the server's live state, never from the
/// request — the client sends a good and a quantity, nothing else. Every trade
/// pays a fee (the cash sink from brief §6) and feeds net volume back into the
/// price engine, which is what makes prices move with the playerbase.
/// </summary>
public sealed class MarketService(
    LoopholesDbContext db,
    IClock clock,
    IRandomSource rng,
    PlayerService players,
    MarketPriceStore prices)
{
    public async Task<MarketQuoteDto> QuoteAsync(CancellationToken ct = default)
    {
        var states = await prices.GetStatesAsync(ct);
        var goods = GameData.Goods.Select(good =>
        {
            var state = states[good.Id];
            return new MarketGoodDto(
                good.Id,
                good.Name,
                good.BasePrice,
                MarketPriceStore.UnitPrice(good, state),
                MarketPriceStore.ChangePercent1h(state),
                good.Behavior,
                good.Hint,
                good.Tradeable,
                state.ActiveEvent,
                state.EventEndsAt);
        }).ToList();

        return new MarketQuoteDto(goods, GameData.TradingFeeRate, clock.UtcNow);
    }

    public async Task<TradeResultDto> BuyAsync(Player p, string goodId, int quantity, CancellationToken ct = default)
    {
        PlayerService.RequireNotFrozen(p, FreezeGate.Market);
        var (good, state) = await ResolveAsync(goodId, quantity, ct);

        if (!good.Tradeable)
            throw new GameRuleException("not_purchasable", $"{good.Name} isn't sold on the open market.");

        var unit = UnitBuyPrice(p, good, state);
        var gross = Math.Round(unit * quantity, 2, MidpointRounding.AwayFromZero);
        var fee = MarketMath.Fee(gross);
        var total = gross + fee;

        PlayerService.SpendCash(p, total);
        players.AddItem(p, good.Id, quantity);
        state.NetVolumeSinceTick += quantity;

        return new TradeResultDto(good.Id, good.Name, quantity, unit, gross, fee, -total,
            players.ToDto(p, await prices.GetStatesAsync(ct)));
    }

    public async Task<TradeResultDto> SellAsync(Player p, string goodId, int quantity, CancellationToken ct = default)
    {
        PlayerService.RequireNotFrozen(p, FreezeGate.Market);
        var (good, state) = await ResolveAsync(goodId, quantity, ct);

        var stack = p.Inventory.FirstOrDefault(s => s.GoodId == good.Id && s.Quantity >= quantity)
            ?? throw new GameRuleException("not_held", $"Not holding {quantity}x {good.Name}.");

        // Appreciating stock is valued by how long this stack has been sat on.
        var unit = MarketPriceStore.UnitPrice(good, state, clock.UtcNow - stack.AcquiredAt);
        var gross = Math.Round(unit * quantity, 2, MidpointRounding.AwayFromZero);
        var fee = MarketMath.Fee(gross);
        var net = gross - fee;

        players.RemoveItem(p, good.Id, quantity);
        PlayerService.EarnCash(p, net);
        state.NetVolumeSinceTick -= quantity;

        return new TradeResultDto(good.Id, good.Name, quantity, unit, gross, fee, net,
            players.ToDto(p, await prices.GetStatesAsync(ct)));
    }

    /// <summary>Paperwork Laundering leaves a standing 25% discount on Shell Company Paperwork.</summary>
    private static decimal UnitBuyPrice(Player p, GoodDefinition good, MarketGoodState state)
    {
        var unit = MarketPriceStore.UnitPrice(good, state);
        if (p.HasPaperworkDiscount && good.Id == "shell_paperwork")
            unit = Math.Round(unit * (1 - GameData.PaperworkDiscountRate), 2, MidpointRounding.AwayFromZero);
        return unit;
    }

    private async Task<(GoodDefinition Good, MarketGoodState State)> ResolveAsync(string goodId, int quantity, CancellationToken ct)
    {
        if (quantity <= 0)
            throw new GameRuleException("invalid_quantity", "Quantity must be positive.");

        var good = GameData.FindGood(goodId)
            ?? throw new GameRuleException("unknown_good", $"No good '{goodId}'.");

        var states = await prices.GetStatesAsync(ct);
        return (good, states[good.Id]);
    }

    public async Task<InventoryDto> InventoryAsync(Player p, CancellationToken ct = default)
    {
        var states = await prices.GetStatesAsync(ct);
        var now = clock.UtcNow;

        var stacks = p.Inventory
            .Where(s => s.Quantity > 0)
            .Select(s =>
            {
                var good = GameData.FindGood(s.GoodId);
                var held = now - s.AcquiredAt;
                var unit = good is null ? 0m : MarketPriceStore.UnitPrice(good, states[s.GoodId], held);
                return new InventoryStackDto(
                    s.GoodId,
                    good?.Name ?? s.GoodId,
                    s.Quantity,
                    s.AcquiredAt,
                    (int)held.TotalDays,
                    unit,
                    Math.Round(unit * s.Quantity, 2, MidpointRounding.AwayFromZero),
                    good is null || good.AppreciationPerDay <= 0 ? null : MarketMath.AppreciationPercent(good, held));
            })
            .OrderByDescending(s => s.StackValue)
            .ToList();

        return new InventoryDto(stacks, players.InventoryUsed(p), p.InventoryCap, stacks.Sum(s => s.StackValue));
    }

    /// <summary>Runs one market tick across every good. Called by the hosted ticker.</summary>
    public async Task TickAsync(CancellationToken ct = default)
    {
        var states = await prices.GetStatesAsync(ct);
        var now = clock.UtcNow;

        foreach (var good in GameData.Goods)
            MarketEngine.Tick(states[good.Id], good, now, rng);

        await db.SaveChangesAsync(ct);
    }
}
