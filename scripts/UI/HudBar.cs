using System;
using Godot;
using Loopholes.Client.Game;
using Loopholes.Client.Net;

namespace Loopholes.Client.UI;

/// <summary>
/// The persistent top strip: cash, Favor, energy, trust, and the freeze banner.
///
/// Every number here is whatever the server last sent. The only thing this
/// control computes is how long until the next energy point, and that is drawn
/// against server time so a device-clock change does nothing.
/// </summary>
public sealed partial class HudBar : PanelContainer
{
    private Label _cash = null!;
    private Label _favor = null!;
    private Label _energy = null!;
    private Label _energyTimer = null!;
    private Label _trust = null!;
    private PanelContainer _freezeBanner = null!;
    private Label _freezeText = null!;

    private Session _session = null!;

    public override void _Ready()
    {
        _session = Session.Instance;

        AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = UiKit.Surface,
            ContentMarginLeft = 14,
            ContentMarginRight = 14,
            ContentMarginTop = 10,
            ContentMarginBottom = 10,
        });

        var column = UiKit.Column(8);
        AddChild(column);

        var row = UiKit.Row(16);
        column.AddChild(row);

        _cash = UiKit.Text("$0.00", UiKit.HeadingSize, UiKit.Money);
        row.AddChild(_cash);

        _favor = UiKit.Small("0 Favor");
        row.AddChild(_favor);

        row.AddChild(UiKit.Spacer());

        var energyBox = UiKit.Row(6);
        _energy = UiKit.Text("—/—", UiKit.HeadingSize);
        _energyTimer = UiKit.Small("");
        energyBox.AddChild(UiKit.Small("Energy"));
        energyBox.AddChild(_energy);
        energyBox.AddChild(_energyTimer);
        row.AddChild(energyBox);

        _trust = UiKit.Small("Unproven");
        row.AddChild(_trust);

        (_freezeBanner, var banner) = UiKit.Card(UiKit.Danger with { A = 0.22f });
        _freezeText = UiKit.Wrapped("", color: UiKit.Danger);
        banner.AddChild(_freezeText);
        _freezeBanner.Visible = false;
        column.AddChild(_freezeBanner);

        _session.StateChanged += OnStateChanged;
        if (_session.State is { } current) OnStateChanged(current);
    }

    public override void _ExitTree() => _session.StateChanged -= OnStateChanged;

    private void OnStateChanged(PlayerStateDto state)
    {
        _cash.Text = UiKit.Money2(state.Cash);
        _favor.Text = $"{state.Favor} Favor";
        _energy.Text = $"{state.Energy.Current}/{state.Energy.Max}";

        _trust.Text = $"{UiKit.TrustBadge(state.Trust.Tier)}  {state.Trust.Score}";
        _trust.AddThemeColorOverride("font_color", UiKit.TrustColor(state.Trust.Tier));

        var freeze = state.Freeze;
        _freezeBanner.Visible = freeze.Tier != FreezeTier.None;

        if (freeze.Tier != FreezeTier.None)
        {
            var scope = freeze.Tier == FreezeTier.Major
                ? "Everything is locked"
                : "Jobs and travel locked — the market is still open";
            _freezeText.Text = $"UNDER INVESTIGATION · {freeze.Tier} Freeze · {scope}";
        }
    }

    public override void _Process(double delta)
    {
        if (_session.State?.Energy is not { } energy)
            return;

        if (energy.NextPointAt is not { } next)
        {
            _energyTimer.Text = "full";
            return;
        }

        var remaining = next - _session.ServerNow;
        _energyTimer.Text = remaining <= TimeSpan.Zero ? "…" : $"+1 in {UiKit.Countdown(remaining)}";
    }
}
