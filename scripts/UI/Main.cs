using System;
using Godot;
using Loopholes.Client.Game;
using Loopholes.Client.Net;

namespace Loopholes.Client.UI;

/// <summary>
/// Root scene. Shows the connect screen until the device has a token, then the
/// game shell, and owns the toast strip that surfaces every refused call.
///
/// Refusals are shown, not swallowed. The server is the authority on whether an
/// action was allowed, so "not enough energy" or "under investigation" arriving
/// from the network is normal traffic rather than an error condition — the player
/// just needs to be told which it was.
/// </summary>
public sealed partial class Main : Control
{
    private const double ToastSeconds = 4;

    private VBoxContainer _root = null!;
    private PanelContainer _toast = null!;
    private Label _toastText = null!;
    private double _toastRemaining;

    private Session _session = null!;

    public override void _Ready()
    {
        _session = Session.Instance;

        SetAnchorsPreset(LayoutPreset.FullRect);

        var background = new ColorRect { Color = new Color("#151412") };
        background.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(background);

        _root = UiKit.Column(0);
        _root.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(_root);

        BuildToast();

        _session.RequestFailed += OnRequestFailed;

        if (_session.IsRegistered)
            ShowGame();
        else
            ShowConnect();
    }

    public override void _ExitTree() => _session.RequestFailed -= OnRequestFailed;

    // --- Screens ----------------------------------------------------------

    private void ShowConnect()
    {
        UiKit.Clear(_root);

        var screen = new ConnectScreen();
        screen.Connected += ShowGame;

        _root.AddChild(UiKit.Padded(screen, 24));
    }

    private void ShowGame()
    {
        UiKit.Clear(_root);

        var hud = new HudBar { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _root.AddChild(hud);

        var tabs = new TabContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };

        Add(tabs, new JobsScreen(), "Jobs");
        Add(tabs, new MarketScreen(), "Market");
        Add(tabs, new TravelScreen(), "Travel");
        Add(tabs, new ProfileScreen(), "Profile");

        _root.AddChild(tabs);

        // The connect screen already fetched state; this covers a returning launch.
        _ = _session.RefreshAsync();
    }

    private static void Add(TabContainer tabs, Control screen, string title)
    {
        var padded = UiKit.Padded(screen, 14);
        padded.Name = title;
        tabs.AddChild(padded);
    }

    // --- Toast ------------------------------------------------------------

    private void BuildToast()
    {
        (_toast, var body) = UiKit.Card(new Color("#3a221f"));

        _toast.SetAnchorsPreset(LayoutPreset.BottomWide);
        _toast.OffsetLeft = 14;
        _toast.OffsetRight = -14;
        _toast.OffsetTop = -78;
        _toast.OffsetBottom = -14;
        _toast.Visible = false;
        _toast.MouseFilter = MouseFilterEnum.Ignore;

        _toastText = UiKit.Text("", UiKit.BodySize, UiKit.Danger);
        body.AddChild(_toastText);

        AddChild(_toast);
    }

    private void OnRequestFailed(ApiException error)
    {
        _toastText.Text = error.IsTransport
            ? $"Offline — {error.Message}"
            : error.Message;

        _toast.Visible = true;
        _toastRemaining = ToastSeconds;

        // A dropped token means the account is gone from this session's point of
        // view; there is nothing to show but the connect screen.
        if (error.IsUnauthorized)
            ShowConnect();
    }

    public override void _Process(double delta)
    {
        if (!_toast.Visible) return;

        _toastRemaining -= delta;
        if (_toastRemaining <= 0)
            _toast.Visible = false;
    }
}
