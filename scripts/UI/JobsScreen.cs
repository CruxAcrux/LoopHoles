using System;
using System.Collections.Generic;
using Godot;
using Loopholes.Client.Game;
using Loopholes.Client.Net;

namespace Loopholes.Client.UI;

/// <summary>
/// The core loop screen: start a job, walk away, come back and collect.
///
/// Note what this screen does <em>not</em> do. It never decides whether a job can
/// be started — it renders the server's <c>Unlocked</c>/<c>LockedReason</c> and
/// lets the Start button through, because the server re-checks anyway. And it
/// never predicts a payout: the range shown is the advertised range, and the
/// actual figure only exists once the server reveals it on collect.
/// </summary>
public sealed partial class JobsScreen : VBoxContainer
{
    private Session _session = null!;
    private VBoxContainer _activeList = null!;
    private VBoxContainer _availableList = null!;
    private Label _resultLine = null!;
    private Button _refresh = null!;

    private readonly List<(ActiveJobDto Job, Label Timer, Button Collect)> _timers = [];
    private bool _busy;

    public override void _Ready()
    {
        _session = Session.Instance;

        AddThemeConstantOverride("separation", 10);
        SizeFlagsVertical = SizeFlags.ExpandFill;

        var header = UiKit.Row();
        header.AddChild(UiKit.Text("Jobs", UiKit.TitleSize));
        header.AddChild(UiKit.Spacer());
        _refresh = UiKit.Action("Refresh", () => _ = ReloadAsync());
        header.AddChild(_refresh);
        AddChild(header);

        _resultLine = UiKit.Wrapped("");
        AddChild(_resultLine);

        var content = UiKit.Column(14);

        content.AddChild(UiKit.Heading("In progress"));
        _activeList = UiKit.Column(8);
        content.AddChild(_activeList);

        content.AddChild(UiKit.Heading("Available"));
        _availableList = UiKit.Column(8);
        content.AddChild(_availableList);

        AddChild(UiKit.Scroll(content));

        _ = ReloadAsync();
    }

    public async System.Threading.Tasks.Task ReloadAsync()
    {
        if (_busy) return;
        _busy = true;
        _refresh.Disabled = true;

        try
        {
            var board = await _session.TryAsync(() => _session.Api.GetJobsAsync());
            if (board is not null) Render(board);

            // The board carries no player state, so pull fresh cash/energy for the HUD.
            await _session.RefreshAsync();
        }
        finally
        {
            _busy = false;
            _refresh.Disabled = false;
        }
    }

    private void Render(JobBoardDto board)
    {
        _timers.Clear();
        UiKit.Clear(_activeList);
        UiKit.Clear(_availableList);

        if (board.Active.Count == 0)
            _activeList.AddChild(UiKit.Small("Nothing running. Start something below."));

        foreach (var active in board.Active)
            _activeList.AddChild(BuildActiveCard(active));

        foreach (var job in board.Available)
            _availableList.AddChild(BuildJobCard(job));
    }

    private Control BuildActiveCard(ActiveJobDto active)
    {
        var (panel, body) = UiKit.Card();

        var top = UiKit.Row();
        top.AddChild(UiKit.Heading(active.Name));
        top.AddChild(UiKit.Spacer());

        var timer = UiKit.Small("");
        top.AddChild(timer);
        body.AddChild(top);

        var collect = UiKit.Action("Collect", () => _ = CollectAsync(active), enabled: false);
        body.AddChild(collect);

        _timers.Add((active, timer, collect));
        return panel;
    }

    private Control BuildJobCard(JobCatalogEntryDto job)
    {
        var (panel, body) = UiKit.Card();

        var top = UiKit.Row();
        top.AddChild(UiKit.Heading(job.Name));
        top.AddChild(UiKit.Spacer());
        top.AddChild(UiKit.Small(job.Tier == JobTier.Operation ? "Operation" : "Hustle"));
        body.AddChild(top);

        var cost = job.CashCost > 0 ? $" · costs {UiKit.Money2(job.CashCost)}" : "";
        body.AddChild(UiKit.Wrapped(
            $"{UiKit.DurationLabel(job.DurationMinutes)} · {job.EnergyCost} energy{cost} · pays {UiKit.MoneyRange(job.PayoutMin, job.PayoutMax)}"));

        if (!string.IsNullOrEmpty(job.Notes))
            body.AddChild(UiKit.Wrapped(job.Notes));

        if (job.RiskDescription is { Length: > 0 } risk)
            body.AddChild(UiKit.Wrapped(risk, color: UiKit.Warning));

        if (job.Unlocked)
        {
            body.AddChild(UiKit.Action("Start", () => _ = StartAsync(job)));
        }
        else
        {
            body.AddChild(UiKit.Wrapped(job.LockedReason ?? "Locked", color: UiKit.Warning));
            body.AddChild(UiKit.Action("Start", () => { }, enabled: false));
        }

        return panel;
    }

    private async System.Threading.Tasks.Task StartAsync(JobCatalogEntryDto job)
    {
        var started = await _session.TryAsync(() => _session.Api.StartJobAsync(job.Id));
        if (started is null) return;

        _resultLine.Text = $"{started.Name} started — back in {UiKit.DurationLabel(job.DurationMinutes)}.";
        _resultLine.AddThemeColorOverride("font_color", UiKit.Muted);
        await ReloadAsync();
    }

    private async System.Threading.Tasks.Task CollectAsync(ActiveJobDto active)
    {
        var result = await _session.TryAsync(
            () => _session.Api.CollectJobAsync(active.ActiveJobId),
            r => r.State);

        if (result is null) return;

        _resultLine.Text = Describe(result);
        _resultLine.AddThemeColorOverride("font_color", result.RiskTriggered ? UiKit.Danger : UiKit.Money);
        await ReloadAsync();
    }

    private static string Describe(JobResultDto result)
    {
        var text = $"{result.Name}: {UiKit.Money2(result.CashPaid)}";

        foreach (var item in result.ItemsGranted)
            text += $" · +{item.Quantity}x {item.Name}";

        foreach (var item in result.ItemsLost)
            text += $" · −{item.Quantity}x {item.Name}";

        if (result.RiskOutcome is { Length: > 0 } outcome)
            text += $" · {outcome}";

        return text;
    }

    public override void _Process(double delta)
    {
        var now = _session.ServerNow;

        foreach (var (job, timer, collect) in _timers)
        {
            var remaining = job.CompletesAt - now;
            var ready = remaining <= TimeSpan.Zero;

            timer.Text = ready ? "ready" : UiKit.Countdown(remaining);
            timer.AddThemeColorOverride("font_color", ready ? UiKit.Money : UiKit.Muted);
            collect.Disabled = !ready;
        }
    }
}
