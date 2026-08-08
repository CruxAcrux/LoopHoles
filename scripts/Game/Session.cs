using System;
using System.Threading.Tasks;
using Godot;
using Loopholes.Client.Net;

namespace Loopholes.Client.Game;

/// <summary>
/// Autoloaded singleton holding the connection and the last known player state.
///
/// Deliberately dumb: it caches what the server last said and re-asks. It never
/// advances a timer, spends energy, or predicts an outcome locally — if the HUD
/// shows it, the server sent it. The one exception is the countdown text in the
/// UI, which interpolates between refreshes purely for display and is corrected
/// by the next response.
/// </summary>
public sealed partial class Session : Node
{
    private const string ConfigPath = "user://session.cfg";
    private const string DefaultBaseUrl = "http://127.0.0.1:5147/api/v1";

    public static Session Instance { get; private set; } = null!;

    public LoopholesApi Api { get; private set; } = null!;

    /// <summary>Last state the server sent. Null until the device has registered.</summary>
    public PlayerStateDto? State { get; private set; }

    public string DeviceId { get; private set; } = "";

    public string DisplayName { get; private set; } = "";

    public bool IsRegistered => !string.IsNullOrEmpty(Api.Token);

    /// <summary>
    /// Offset between the server's clock and this device's, measured on every
    /// response. Countdowns are drawn against server time so a player who moves
    /// their device clock sees no change at all.
    /// </summary>
    public TimeSpan ClockSkew { get; private set; }

    public DateTimeOffset ServerNow => DateTimeOffset.UtcNow + ClockSkew;

    /// <summary>Raised whenever fresh player state arrives, so any screen can redraw.</summary>
    public event Action<PlayerStateDto>? StateChanged;

    /// <summary>Raised when a call fails, for the shared toast strip.</summary>
    public event Action<ApiException>? RequestFailed;

    public override void _Ready()
    {
        Instance = this;

        Api = new LoopholesApi { Name = "Api" };
        AddChild(Api);

        Load();
    }

    // --- Persistence ------------------------------------------------------

    private void Load()
    {
        var config = new ConfigFile();

        if (config.Load(ConfigPath) == Error.Ok)
        {
            DeviceId = (string)config.GetValue("session", "device_id", "");
            DisplayName = (string)config.GetValue("session", "display_name", "");
            Api.Token = (string)config.GetValue("session", "token", "");
            Api.BaseUrl = (string)config.GetValue("session", "base_url", DefaultBaseUrl);
        }

        if (string.IsNullOrEmpty(DeviceId))
        {
            // Identity is per-install. Losing it means losing the account, which is
            // the accepted trade for an MVP with no email or social login.
            DeviceId = Guid.NewGuid().ToString();
            Save();
        }
    }

    private void Save()
    {
        var config = new ConfigFile();
        config.SetValue("session", "device_id", DeviceId);
        config.SetValue("session", "display_name", DisplayName);
        config.SetValue("session", "token", Api.Token ?? "");
        config.SetValue("session", "base_url", Api.BaseUrl);
        config.Save(ConfigPath);
    }

    public void SetBaseUrl(string baseUrl)
    {
        Api.BaseUrl = baseUrl.TrimEnd('/');
        Save();
    }

    /// <summary>Forgets the token so the next launch registers again. Used by "sign out".</summary>
    public void Forget()
    {
        Api.Token = null;
        State = null;
        Save();
    }

    // --- Calls ------------------------------------------------------------

    public async Task<bool> RegisterAsync(string displayName)
    {
        try
        {
            var auth = await Api.RegisterAsync(DeviceId, displayName);
            DisplayName = auth.DisplayName;
            Save();

            await RefreshAsync();
            return true;
        }
        catch (ApiException ex)
        {
            RequestFailed?.Invoke(ex);
            return false;
        }
    }

    public async Task<PlayerStateDto?> RefreshAsync()
    {
        try
        {
            Apply(await Api.GetStateAsync());
            return State;
        }
        catch (ApiException ex)
        {
            RequestFailed?.Invoke(ex);
            return null;
        }
    }

    /// <summary>
    /// Runs a call, folds any player state it returned back into the session, and
    /// routes a refusal to the toast strip instead of throwing into the scene tree.
    /// Returns default on failure, so callers branch on null rather than catch.
    /// </summary>
    public async Task<T?> TryAsync<T>(Func<Task<T>> call, Func<T, PlayerStateDto?>? stateFrom = null)
    {
        try
        {
            var result = await call();

            if (stateFrom?.Invoke(result) is { } state)
                Apply(state);

            return result;
        }
        catch (ApiException ex)
        {
            RequestFailed?.Invoke(ex);

            // A rejected token can't be recovered from in place; drop it so the
            // next launch shows the connect screen rather than looping on 401s.
            if (ex.IsUnauthorized)
                Forget();

            return default;
        }
    }

    public void Apply(PlayerStateDto state)
    {
        ClockSkew = state.ServerTime - DateTimeOffset.UtcNow;
        State = state;
        StateChanged?.Invoke(state);
    }
}
