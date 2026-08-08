using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;
using Loopholes.Client.Game;
using Loopholes.Client.Net;

namespace Loopholes.Client.UI;

/// <summary>
/// Buy low, sell high. Prices are read-only here — the client sends a good id and
/// a quantity, and the server prices the trade from its own live state, so a
/// stale board on screen can never be traded against.
///
/// Each good's <c>BehaviorHint</c> is surfaced verbatim, because the design
/// intends the personalities to be learnable rather than hidden.
/// </summary>
public sealed partial class MarketScreen : VBoxContainer
{
    private Session _session = null!;
    private VBoxContainer _goodsList = null!;
    private VBoxContainer _inventoryList = null!;
    private Label _feeLine = null!;
    private Label _resultLine = null!;
    private Button _refresh = null!;

    private readonly Dictionary<string, int> _held = [];
    private readonly List<(Label Label, DateTimeOffset EndsAt)> _eventTimers = [];
    private bool _busy;

    public override void _Ready()
    {
        _session = Session.Instance;

        AddThemeConstantOverride("separation", 10);
        SizeFlagsVertical = SizeFlags.ExpandFill;

        var header = UiKit.Row();
        header.AddChild(UiKit.Text("Market", UiKit.TitleSize));
        header.AddChild(UiKit.Spacer());
        _refresh = UiKit.Action("Refresh", () => _ = ReloadAsync());
        header.AddChild(_refresh);
        AddChild(header);

        _feeLine = UiKit.Wrapped("");
        AddChild(_feeLine);

        _resultLine = UiKit.Wrapped("");
        AddChild(_resultLine);

        var content = UiKit.Column(14);

        content.AddChild(UiKit.Heading("Goods"));
        _goodsList = UiKit.Column(8);
        content.AddChild(_goodsList);

        content.AddChild(UiKit.Heading("Inventory"));
        _inventoryList = UiKit.Column(8);
        content.AddChild(_inventoryList);

        AddChild(UiKit.Scroll(content));

        _ = ReloadAsync();
    }

    public async Task ReloadAsync()
    {
        if (_busy) return;
        _busy = true;
        _refresh.Disabled = true;

        try
        {
            var inventory = await _session.TryAsync(() => _session.Api.GetInventoryAsync());
            var quote = await _session.TryAsync(() => _session.Api.GetMarketAsync());

            _held.Clear();
            if (inventory is not null)
            {
                foreach (var stack in inventory.Stacks)
                    _held[stack.GoodId] = stack.Quantity;

                RenderInventory(inventory);
            }

            if (quote is not null) RenderGoods(quote);

            await _session.RefreshAsync();
        }
        finally
        {
            _busy = false;
            _refresh.Disabled = false;
        }
    }

    private void RenderGoods(MarketQuoteDto quote)
    {
        _eventTimers.Clear();
        UiKit.Clear(_goodsList);

        _feeLine.Text = $"Every trade pays a {UiKit.Percent((double)quote.TradingFeeRate, 0)} fee, both directions.";

        foreach (var good in quote.Goods)
            _goodsList.AddChild(BuildGoodCard(good));
    }

    private Control BuildGoodCard(MarketGoodDto good)
    {
        var (panel, body) = UiKit.Card();

        var top = UiKit.Row();
        top.AddChild(UiKit.Heading(good.Name));
        top.AddChild(UiKit.Spacer());
        top.AddChild(UiKit.Text(UiKit.Money2(good.CurrentPrice), UiKit.HeadingSize, UiKit.Money));
        body.AddChild(top);

        var detail = UiKit.Row(10);
        detail.AddChild(UiKit.Small(UiKit.SignedPercent(good.ChangePercent1h) + " /1h", UiKit.ChangeColor(good.ChangePercent1h)));
        detail.AddChild(UiKit.Small($"base {UiKit.Money2(good.BasePrice)}"));
        body.AddChild(detail);

        body.AddChild(UiKit.Wrapped(good.BehaviorHint));

        if (good.ActiveEvent is { Length: > 0 } activeEvent)
        {
            var label = UiKit.Small($"{activeEvent.ToUpperInvariant()} in progress", UiKit.Warning);
            body.AddChild(label);

            if (good.EventEndsAt is { } endsAt)
                _eventTimers.Add((label, endsAt));
        }

        var held = _held.GetValueOrDefault(good.Id);
        if (held > 0)
            body.AddChild(UiKit.Small($"holding {held}"));

        var buttons = UiKit.Row(6);

        if (good.Tradeable)
        {
            buttons.AddChild(UiKit.Action("Buy 1", () => _ = TradeAsync(good, 1, buy: true)));
            buttons.AddChild(UiKit.Action("Buy 10", () => _ = TradeAsync(good, 10, buy: true)));
        }
        else
        {
            buttons.AddChild(UiKit.Action("Buy", () => { }, enabled: false,
                tooltip: "Not sold on the open market — this one has to be earned."));
        }

        buttons.AddChild(UiKit.Action("Sell 1", () => _ = TradeAsync(good, 1, buy: false), enabled: held >= 1));
        buttons.AddChild(UiKit.Action("Sell all", () => _ = TradeAsync(good, held, buy: false), enabled: held >= 1));

        body.AddChild(buttons);
        return panel;
    }

    private void RenderInventory(InventoryDto inventory)
    {
        UiKit.Clear(_inventoryList);

        _inventoryList.AddChild(UiKit.Wrapped(
            $"{inventory.Used}/{inventory.Cap} slots used · worth {UiKit.Money2(inventory.TotalValue)}"));

        if (inventory.Stacks.Count == 0)
        {
            _inventoryList.AddChild(UiKit.Small("Nothing on hand."));
            return;
        }

        foreach (var stack in inventory.Stacks)
        {
            var (panel, body) = UiKit.Card();

            var top = UiKit.Row();
            top.AddChild(UiKit.Text($"{stack.Quantity}x {stack.Name}"));
            top.AddChild(UiKit.Spacer());
            top.AddChild(UiKit.Text(UiKit.Money2(stack.StackValue), UiKit.BodySize, UiKit.Money));
            body.AddChild(top);

            var detail = $"{UiKit.Money2(stack.UnitValue)} each · held {stack.DaysHeld}d";

            // Only appreciating stock has a hold-time payoff worth pointing at.
            if (stack.AppreciationPercent is { } appreciation)
                detail += $" · {UiKit.SignedPercent(appreciation)} from age";

            body.AddChild(UiKit.Wrapped(detail));
            _inventoryList.AddChild(panel);
        }
    }

    private async Task TradeAsync(MarketGoodDto good, int quantity, bool buy)
    {
        if (quantity <= 0) return;

        var result = await _session.TryAsync(
            () => buy ? _session.Api.BuyAsync(good.Id, quantity) : _session.Api.SellAsync(good.Id, quantity),
            r => r.State);

        if (result is null) return;

        _resultLine.Text = buy
            ? $"Bought {result.Quantity}x {result.Name} at {UiKit.Money2(result.UnitPrice)} · fee {UiKit.Money2(result.Fee)} · paid {UiKit.Money2(-result.Net)}"
            : $"Sold {result.Quantity}x {result.Name} at {UiKit.Money2(result.UnitPrice)} · fee {UiKit.Money2(result.Fee)} · took {UiKit.Money2(result.Net)}";

        _resultLine.AddThemeColorOverride("font_color", buy ? UiKit.Muted : UiKit.Money);
        await ReloadAsync();
    }

    public override void _Process(double delta)
    {
        var now = _session.ServerNow;

        foreach (var (label, endsAt) in _eventTimers)
        {
            var remaining = endsAt - now;
            if (remaining > TimeSpan.Zero)
                label.Text = label.Text.Split(" · ")[0] + $" · {UiKit.Countdown(remaining)} left";
        }
    }
}
