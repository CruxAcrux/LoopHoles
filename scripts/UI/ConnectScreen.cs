using System;
using Godot;
using Loopholes.Client.Game;

namespace Loopholes.Client.UI;

/// <summary>
/// First-run screen: pick a server and a company name, then register the device.
///
/// The server field is editable on purpose — a solo developer needs to point the
/// build at localhost, a LAN address, and staging without a rebuild. It is
/// persisted, so this screen is seen once per install.
/// </summary>
public sealed partial class ConnectScreen : VBoxContainer
{
    private LineEdit _url = null!;
    private LineEdit _name = null!;
    private Button _connect = null!;
    private Label _status = null!;

    /// <summary>Raised once the device is registered and player state has arrived.</summary>
    public event Action? Connected;

    public override void _Ready()
    {
        var session = Session.Instance;

        AddThemeConstantOverride("separation", 12);
        SizeFlagsVertical = SizeFlags.ExpandFill;

        AddChild(UiKit.Text("Loopholes", 34));
        AddChild(UiKit.Wrapped("Everything here is legal until somebody checks."));

        AddChild(new Control { CustomMinimumSize = new Vector2(0, 12) });

        AddChild(UiKit.Small("Company name"));
        _name = new LineEdit
        {
            Text = string.IsNullOrEmpty(session.DisplayName) ? "" : session.DisplayName,
            PlaceholderText = "Meridian Consulting AB",
            MaxLength = 32,
        };
        AddChild(_name);

        AddChild(UiKit.Small("Server"));
        _url = new LineEdit { Text = session.Api.BaseUrl };
        AddChild(_url);

        _connect = UiKit.Action("Open for business", () => _ = ConnectAsync());
        AddChild(_connect);

        _status = UiKit.Wrapped("");
        AddChild(_status);
    }

    private async System.Threading.Tasks.Task ConnectAsync()
    {
        var session = Session.Instance;

        _connect.Disabled = true;
        _status.AddThemeColorOverride("font_color", UiKit.Muted);
        _status.Text = "Registering…";

        session.SetBaseUrl(_url.Text.Trim());

        var name = _name.Text.Trim();
        var ok = await session.RegisterAsync(string.IsNullOrEmpty(name) ? "Anonymous LLC" : name);

        _connect.Disabled = false;

        if (ok)
        {
            _status.Text = "";
            Connected?.Invoke();
        }
        else
        {
            // The specific reason already went to the toast strip via RequestFailed.
            _status.AddThemeColorOverride("font_color", UiKit.Danger);
            _status.Text = "Couldn't register. Check the server address and try again.";
        }
    }
}
