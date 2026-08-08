using System;
using System.Globalization;
using Godot;
using Loopholes.Client.Net;

namespace Loopholes.Client.UI;

/// <summary>
/// Small builders for the controls this game uses, plus the formatting rules.
///
/// The UI is assembled in code rather than in .tscn files. For a screen that is
/// almost entirely a data-driven list rebuilt on every server response, a scene
/// tree would be a handful of container nodes and a script that ignores them.
/// </summary>
public static class UiKit
{
    // Dry corporate palette: paper, ink, and a single accent for money.
    public static readonly Color Ink = new("#e8e6e1");
    public static readonly Color Muted = new("#9a968f");
    public static readonly Color Money = new("#7ec699");
    public static readonly Color Warning = new("#e0a458");
    public static readonly Color Danger = new("#d9736a");
    public static readonly Color Surface = new("#1c1b19");
    public static readonly Color SurfaceRaised = new("#26241f");

    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    public const int TitleSize = 22;
    public const int HeadingSize = 17;
    public const int BodySize = 14;
    public const int SmallSize = 12;

    // --- Controls ---------------------------------------------------------

    /// <summary>
    /// A single-line label. Autowrap is deliberately off: a wrapping Label reports a
    /// minimum width of about one character, so any HBox containing a
    /// <see cref="Spacer"/> would crush it into a vertical column of letters.
    /// Use <see cref="Wrapped"/> when text genuinely needs to flow.
    /// </summary>
    public static Label Text(string text, int size = BodySize, Color? color = null)
    {
        var label = new Label { Text = text, AutowrapMode = TextServer.AutowrapMode.Off };
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", color ?? Ink);
        return label;
    }

    /// <summary>
    /// Multi-line body copy. Only safe as a direct child of a vertical container,
    /// where it is handed the full width before it decides where to break.
    /// </summary>
    public static Label Wrapped(string text, int size = SmallSize, Color? color = null)
    {
        var label = Text(text, size, color ?? Muted);
        label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        label.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        return label;
    }

    public static Label Heading(string text) => Text(text, HeadingSize, Ink);

    public static Label Small(string text, Color? color = null) => Text(text, SmallSize, color ?? Muted);

    public static Button Action(string text, Action onPressed, bool enabled = true, string? tooltip = null)
    {
        var button = new Button
        {
            Text = text,
            Disabled = !enabled,
            TooltipText = tooltip ?? "",
            CustomMinimumSize = new Vector2(0, 36),
        };

        button.AddThemeFontSizeOverride("font_size", BodySize);
        button.Pressed += onPressed;
        return button;
    }

    public static VBoxContainer Column(int separation = 6)
    {
        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", separation);
        return box;
    }

    public static HBoxContainer Row(int separation = 8)
    {
        var box = new HBoxContainer();
        box.AddThemeConstantOverride("separation", separation);
        return box;
    }

    /// <summary>A raised card with padding, used for every list item.</summary>
    public static (PanelContainer Panel, VBoxContainer Body) Card(Color? background = null)
    {
        var panel = new PanelContainer();
        panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = background ?? SurfaceRaised,
            CornerRadiusTopLeft = 6,
            CornerRadiusTopRight = 6,
            CornerRadiusBottomLeft = 6,
            CornerRadiusBottomRight = 6,
            ContentMarginLeft = 12,
            ContentMarginRight = 12,
            ContentMarginTop = 10,
            ContentMarginBottom = 10,
        });

        var body = Column(4);
        panel.AddChild(body);
        return (panel, body);
    }

    public static Control Spacer() => new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };

    public static ScrollContainer Scroll(Control content)
    {
        var scroll = new ScrollContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
        };

        content.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        scroll.AddChild(content);
        return scroll;
    }

    public static MarginContainer Padded(Control child, int margin = 12)
    {
        var container = new MarginContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };

        foreach (var side in new[] { "margin_left", "margin_right", "margin_top", "margin_bottom" })
            container.AddThemeConstantOverride(side, margin);

        container.AddChild(child);
        return container;
    }

    public static void Clear(Node node)
    {
        foreach (var child in node.GetChildren())
        {
            node.RemoveChild(child);
            child.QueueFree();
        }
    }

    // --- Formatting -------------------------------------------------------

    public static string Money2(decimal amount) => "$" + amount.ToString("N2", Invariant);

    public static string MoneyRange(decimal min, decimal max) =>
        min == max ? Money2(min) : $"{Money2(min)}–{Money2(max)}";

    public static string Percent(double fraction, int decimals = 1) =>
        (fraction * 100).ToString("F" + decimals, Invariant) + "%";

    public static string SignedPercent(double fraction)
    {
        var text = Percent(Math.Abs(fraction));
        return fraction >= 0 ? "+" + text : "−" + text;
    }

    public static Color ChangeColor(double fraction) =>
        fraction > 0.0005 ? Money : fraction < -0.0005 ? Danger : Muted;

    /// <summary>Job durations as designers wrote them: "30 s", "45 min", "8 hr".</summary>
    public static string DurationLabel(double minutes) => minutes switch
    {
        < 1 => $"{minutes * 60:0} s",
        < 60 => $"{minutes:0.#} min",
        _ => $"{minutes / 60:0.#} hr",
    };

    /// <summary>A live countdown, coarsening as it gets further away.</summary>
    public static string Countdown(TimeSpan remaining)
    {
        if (remaining <= TimeSpan.Zero) return "ready";
        if (remaining.TotalHours >= 1) return $"{(int)remaining.TotalHours}h {remaining.Minutes:00}m";
        if (remaining.TotalMinutes >= 1) return $"{remaining.Minutes}m {remaining.Seconds:00}s";
        return $"{remaining.Seconds}s";
    }

    public static string TrustBadge(TrustTier tier) => tier switch
    {
        TrustTier.Untrustworthy => "⚑ Untrustworthy",
        TrustTier.Trusted => "◆ Trusted",
        TrustTier.Elite => "★ Elite",
        _ => "Unproven",
    };

    public static Color TrustColor(TrustTier tier) => tier switch
    {
        TrustTier.Untrustworthy => Danger,
        TrustTier.Trusted => Money,
        TrustTier.Elite => Warning,
        _ => Muted,
    };

    public static string LicenseLabel(LicenseDto license) => license.Status switch
    {
        LicenseStatus.Owned => license.ObtainedByCheating ? "Licensed (unverified)" : "Licensed",
        LicenseStatus.Studying => "Studying",
        LicenseStatus.Suspended => "Suspended",
        _ => "No licence",
    };
}
