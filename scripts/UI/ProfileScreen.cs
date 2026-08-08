using System;
using System.Threading.Tasks;
using Godot;
using Loopholes.Client.Game;
using Loopholes.Client.Net;

namespace Loopholes.Client.UI;

/// <summary>
/// The receipts screen: lifetime stats, the dated trust log behind the badge, the
/// seasonal boards, and the wardrobe.
///
/// The trust history is shown as a plain ledger with its deltas because the whole
/// theme rests on being able to check someone's record rather than take a badge
/// at face value.
/// </summary>
public sealed partial class ProfileScreen : VBoxContainer
{
    private Session _session = null!;
    private VBoxContainer _summary = null!;
    private VBoxContainer _history = null!;
    private VBoxContainer _board = null!;
    private VBoxContainer _wardrobe = null!;
    private Label _resultLine = null!;
    private Button _refresh = null!;

    private LeaderboardMode _mode = LeaderboardMode.Empire;
    private bool _busy;

    public override void _Ready()
    {
        _session = Session.Instance;

        AddThemeConstantOverride("separation", 10);
        SizeFlagsVertical = SizeFlags.ExpandFill;

        var header = UiKit.Row();
        header.AddChild(UiKit.Text("Profile", UiKit.TitleSize));
        header.AddChild(UiKit.Spacer());
        _refresh = UiKit.Action("Refresh", () => _ = ReloadAsync());
        header.AddChild(_refresh);
        AddChild(header);

        _resultLine = UiKit.Wrapped("");
        AddChild(_resultLine);

        var content = UiKit.Column(14);

        _summary = UiKit.Column(8);
        content.AddChild(_summary);

        content.AddChild(UiKit.Heading("Season leaderboard"));

        var modes = UiKit.Row(6);
        foreach (var mode in Enum.GetValues<LeaderboardMode>())
        {
            var captured = mode;
            modes.AddChild(UiKit.Action(Label(captured), () =>
            {
                _mode = captured;
                _ = ReloadAsync();
            }));
        }

        content.AddChild(modes);
        _board = UiKit.Column(4);
        content.AddChild(_board);

        content.AddChild(UiKit.Heading("Trust history"));
        _history = UiKit.Column(4);
        content.AddChild(_history);

        content.AddChild(UiKit.Heading("Wardrobe"));
        _wardrobe = UiKit.Column(8);
        content.AddChild(_wardrobe);

        AddChild(UiKit.Scroll(content));

        _ = ReloadAsync();
    }

    private static string Label(LeaderboardMode mode) => mode switch
    {
        LeaderboardMode.Richest => "Richest",
        LeaderboardMode.MostTrusted => "Most Trusted",
        _ => "Empire",
    };

    public async Task ReloadAsync()
    {
        if (_busy) return;
        _busy = true;
        _refresh.Disabled = true;

        try
        {
            await _session.RefreshAsync();

            var stats = await _session.TryAsync(() => _session.Api.GetStatsAsync());
            var board = await _session.TryAsync(() => _session.Api.GetLeaderboardAsync(_mode));
            var wardrobe = await _session.TryAsync(() => _session.Api.GetWardrobeAsync());

            if (stats is not null)
            {
                RenderSummary(stats);
                RenderHistory(stats);
            }

            if (board is not null) RenderBoard(board);
            if (wardrobe is not null) RenderWardrobe(wardrobe);
        }
        finally
        {
            _busy = false;
            _refresh.Disabled = false;
        }
    }

    private void RenderSummary(StatsDto stats)
    {
        UiKit.Clear(_summary);

        var (panel, body) = UiKit.Card();

        body.AddChild(UiKit.Text(_session.DisplayName, UiKit.HeadingSize));

        var badge = UiKit.Small($"{UiKit.TrustBadge(stats.Trust.Tier)} · {stats.Trust.Score}/100");
        badge.AddThemeColorOverride("font_color", UiKit.TrustColor(stats.Trust.Tier));
        body.AddChild(badge);

        body.AddChild(UiKit.Small(UiKit.LicenseLabel(stats.License)));

        body.AddChild(UiKit.Small($"Lifetime earned: {UiKit.Money2(stats.LifetimeCashEarned)}"));
        body.AddChild(UiKit.Small($"Jobs completed: {stats.JobsCompleted}"));
        body.AddChild(UiKit.Small($"Best empire score: {UiKit.Money2(stats.BestEmpireScore)}"));
        body.AddChild(UiKit.Small($"Best season rank: {(stats.BestSeasonRank is { } rank ? "#" + rank : "—")}"));
        body.AddChild(UiKit.Small($"Times investigated: {stats.TimesFrozen}"));

        _summary.AddChild(panel);
    }

    private void RenderHistory(StatsDto stats)
    {
        UiKit.Clear(_history);

        if (stats.TrustHistory.Count == 0)
        {
            _history.AddChild(UiKit.Small("No entries. Nothing has moved your standing yet."));
            return;
        }

        foreach (var entry in stats.TrustHistory)
        {
            var row = UiKit.Row(10);

            row.AddChild(UiKit.Small(entry.At.ToLocalTime().ToString("MMM d HH:mm")));

            var delta = UiKit.Small(entry.Delta >= 0 ? $"+{entry.Delta}" : entry.Delta.ToString());
            delta.AddThemeColorOverride("font_color", entry.Delta >= 0 ? UiKit.Money : UiKit.Danger);
            row.AddChild(delta);

            row.AddChild(UiKit.Small($"→ {entry.ScoreAfter}"));
            row.AddChild(UiKit.Text(entry.Description, UiKit.SmallSize, UiKit.Ink));
            row.AddChild(UiKit.Spacer());

            _history.AddChild(row);
        }
    }

    private void RenderBoard(LeaderboardDto board)
    {
        UiKit.Clear(_board);

        var remaining = board.SeasonEndsAt - _session.ServerNow;
        _board.AddChild(UiKit.Wrapped(
            $"Season {board.SeasonNumber} · {(int)remaining.TotalDays}d left · only the board resets, never your progress."));

        if (board.Entries.Count == 0)
        {
            _board.AddChild(UiKit.Small("Nobody has posted a score yet."));
            return;
        }

        foreach (var entry in board.Entries)
            _board.AddChild(BoardRow(entry, isYou: entry.PlayerId == _session.State?.PlayerId));

        // Keep the player's own rank visible even when they're off the bottom of the page.
        if (board.You is { } you && !board.Entries.Exists(e => e.PlayerId == you.PlayerId))
        {
            _board.AddChild(UiKit.Small("…"));
            _board.AddChild(BoardRow(you, isYou: true));
        }
    }

    private static Control BoardRow(LeaderboardEntryDto entry, bool isYou)
    {
        var row = UiKit.Row(10);

        row.AddChild(UiKit.Small($"#{entry.Rank}"));
        row.AddChild(UiKit.Text(entry.DisplayName, UiKit.BodySize, isYou ? UiKit.Money : UiKit.Ink));

        var badge = UiKit.Small(UiKit.TrustBadge(entry.TrustTier));
        badge.AddThemeColorOverride("font_color", UiKit.TrustColor(entry.TrustTier));
        row.AddChild(badge);

        row.AddChild(UiKit.Spacer());
        row.AddChild(UiKit.Small(UiKit.Money2(entry.Score), UiKit.Ink));

        return row;
    }

    private void RenderWardrobe(WardrobeDto wardrobe)
    {
        UiKit.Clear(_wardrobe);

        _wardrobe.AddChild(UiKit.Wrapped(
            $"{wardrobe.Favor} Favor · cosmetics are strictly cosmetic and never affect a stat."));

        foreach (var item in wardrobe.Items)
        {
            var (panel, body) = UiKit.Card();

            var top = UiKit.Row();
            top.AddChild(UiKit.Text(item.Name));
            top.AddChild(UiKit.Spacer());
            top.AddChild(UiKit.Small(item.Slot));
            body.AddChild(top);

            if (item.Equipped)
                body.AddChild(UiKit.Small("Equipped", UiKit.Money));
            else if (item.Owned)
                body.AddChild(UiKit.Action("Equip", () => _ = EquipAsync(item)));
            else
                body.AddChild(UiKit.Action($"Buy — {item.FavorCost} Favor", () => _ = BuyAsync(item),
                    enabled: wardrobe.Favor >= item.FavorCost));

            _wardrobe.AddChild(panel);
        }
    }

    private async Task BuyAsync(CosmeticDto item)
    {
        var wardrobe = await _session.TryAsync(() => _session.Api.BuyCosmeticAsync(item.Id));
        if (wardrobe is null) return;

        _resultLine.Text = $"Bought {item.Name}.";
        await ReloadAsync();
    }

    private async Task EquipAsync(CosmeticDto item)
    {
        var wardrobe = await _session.TryAsync(() => _session.Api.EquipCosmeticAsync(item.Id));
        if (wardrobe is null) return;

        _resultLine.Text = $"Wearing {item.Name}.";
        await ReloadAsync();
    }
}
