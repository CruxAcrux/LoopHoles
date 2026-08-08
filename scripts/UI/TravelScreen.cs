using System;
using System.Threading.Tasks;
using Godot;
using Loopholes.Client.Game;
using Loopholes.Client.Net;

namespace Loopholes.Client.UI;

/// <summary>
/// Travel, the driver's licence, and digging out of an Audit Freeze — the three
/// systems that trade time against money and honesty.
///
/// The risk percentages shown are the server's own trust-adjusted figures, not a
/// local recalculation of the formula. That matters: if the two ever disagreed,
/// the player would be planning against a number that isn't the one being rolled.
/// </summary>
public sealed partial class TravelScreen : VBoxContainer
{
    private Session _session = null!;
    private VBoxContainer _options = null!;
    private VBoxContainer _license = null!;
    private VBoxContainer _freeze = null!;
    private Label _resultLine = null!;
    private Button _refresh = null!;
    private bool _busy;

    public override void _Ready()
    {
        _session = Session.Instance;

        AddThemeConstantOverride("separation", 10);
        SizeFlagsVertical = SizeFlags.ExpandFill;

        var header = UiKit.Row();
        header.AddChild(UiKit.Text("Travel & Standing", UiKit.TitleSize));
        header.AddChild(UiKit.Spacer());
        _refresh = UiKit.Action("Refresh", () => _ = ReloadAsync());
        header.AddChild(_refresh);
        AddChild(header);

        _resultLine = UiKit.Wrapped("");
        AddChild(_resultLine);

        var content = UiKit.Column(14);

        content.AddChild(UiKit.Heading("Under investigation"));
        _freeze = UiKit.Column(8);
        content.AddChild(_freeze);

        content.AddChild(UiKit.Heading("Transport"));
        _options = UiKit.Column(8);
        content.AddChild(_options);

        content.AddChild(UiKit.Heading("Driver's licence"));
        _license = UiKit.Column(8);
        content.AddChild(_license);

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
            var state = await _session.RefreshAsync();
            var options = await _session.TryAsync(() => _session.Api.GetTravelOptionsAsync());

            if (state is not null)
            {
                RenderFreeze(state);
                RenderLicense(state);
            }

            if (options is not null) RenderOptions(options);
        }
        finally
        {
            _busy = false;
            _refresh.Disabled = false;
        }
    }

    private void RenderFreeze(PlayerStateDto state)
    {
        UiKit.Clear(_freeze);

        if (state.Freeze.Tier == FreezeTier.None)
        {
            _freeze.AddChild(UiKit.Small("Nothing on file. Accounts are clear."));
            return;
        }

        var (panel, body) = UiKit.Card();
        var tier = state.Freeze.Tier;

        body.AddChild(UiKit.Text($"{tier} Freeze", UiKit.HeadingSize, UiKit.Danger));

        if (state.Freeze.EndsAt is { } endsAt)
            body.AddChild(UiKit.Small($"Lifts in {UiKit.Countdown(endsAt - _session.ServerNow)}"));

        var canFile = state.Freeze.NextPaperworkAt is not { } next || next <= _session.ServerNow;
        var paperworkNote = tier == FreezeTier.Major ? "−45 min, hourly" : "−20 min, every 30 min";

        body.AddChild(UiKit.Action(
            $"File paperwork ({paperworkNote})",
            () => _ = FilePaperworkAsync(),
            enabled: canFile,
            tooltip: canFile ? null : "The filing window hasn't reopened yet."));

        if (tier == FreezeTier.Major)
        {
            body.AddChild(UiKit.Action("Hire a lawyer — $2,500, clears it", () => _ = LawyerAsync(false)));
            body.AddChild(UiKit.Action("Partial representation — $1,200, halves it", () => _ = LawyerAsync(true)));
        }
        else
        {
            body.AddChild(UiKit.Action("Hire a lawyer — $400, clears it", () => _ = LawyerAsync(false)));
        }

        body.AddChild(UiKit.Wrapped("Paperwork and lawyers combine: pay down half, then keep filing."));
        _freeze.AddChild(panel);
    }

    private void RenderOptions(System.Collections.Generic.List<TravelOptionDto> options)
    {
        UiKit.Clear(_options);

        foreach (var option in options)
        {
            var (panel, body) = UiKit.Card();

            var top = UiKit.Row();
            top.AddChild(UiKit.Heading(option.Transport.ToString()));
            top.AddChild(UiKit.Spacer());
            top.AddChild(UiKit.Text(UiKit.DurationLabel(option.DurationMinutes), UiKit.BodySize));
            body.AddChild(top);

            var better = option.AdjustedRiskPercent < option.BaseRiskPercent;
            var worse = option.AdjustedRiskPercent > option.BaseRiskPercent;

            var riskColor = better ? UiKit.Money : worse ? UiKit.Danger : UiKit.Muted;
            var riskNote = better ? " (your standing helps)" : worse ? " (your standing hurts)" : "";

            body.AddChild(UiKit.Wrapped(
                $"Risk {UiKit.Percent(option.AdjustedRiskPercent, 2)} · base {UiKit.Percent(option.BaseRiskPercent, 0)}{riskNote}",
                color: riskColor));

            if (option.LicenseDiscountApplied)
                body.AddChild(UiKit.Small("Licence applied: −25% travel time", UiKit.Money));

            body.AddChild(UiKit.Action("Depart", () => _ = TravelAsync(option.Transport)));
            _options.AddChild(panel);
        }
    }

    private void RenderLicense(PlayerStateDto state)
    {
        UiKit.Clear(_license);

        var (panel, body) = UiKit.Card();
        var license = state.License;

        body.AddChild(UiKit.Text(UiKit.LicenseLabel(license), UiKit.HeadingSize));

        switch (license.Status)
        {
            case LicenseStatus.Owned:
                body.AddChild(UiKit.Small("Car trips take 25% less time."));
                if (license.ObtainedByCheating)
                    body.AddChild(UiKit.Small("Obtained without sitting the study period.", UiKit.Warning));
                break;

            case LicenseStatus.Studying:
            {
                var done = license.StudyCompletesAt is not { } at || at <= _session.ServerNow;

                body.AddChild(UiKit.Wrapped(done
                    ? "Study complete — sit the exam."
                    : $"Studying for {UiKit.Countdown(license.StudyCompletesAt!.Value - _session.ServerNow)} more."));

                body.AddChild(UiKit.Action("Sit the exam (95% pass)", () => _ = ExamAsync(false), enabled: done));
                break;
            }

            case LicenseStatus.Suspended:
                body.AddChild(UiKit.Small(license.RetryAvailableAt is { } retry
                    ? $"Barred from retaking for {UiKit.Countdown(retry - _session.ServerNow)}."
                    : "Barred from retaking.", UiKit.Danger));
                break;

            default:
                body.AddChild(UiKit.Wrapped("Optional. Speeds up car travel; never gates anything."));
                body.AddChild(UiKit.Action("Study properly — $500, 3 hr", () => _ = StudyAsync()));
                body.AddChild(UiKit.Action("Skip study, sit it now — $500, 35% caught", () => _ = ExamAsync(true)));
                body.AddChild(UiKit.Wrapped(
                    "Getting caught is a fine, a 48 hr bar and a freeze — but it never touches your trust score."));
                break;
        }

        _license.AddChild(panel);
    }

    // --- Actions ----------------------------------------------------------

    private async Task TravelAsync(Transport transport)
    {
        var result = await _session.TryAsync(
            () => _session.Api.StartTravelAsync(transport, "Quarterly Offsite"),
            r => r.State);

        if (result is null) return;

        _resultLine.Text = result.RiskTriggered
            ? $"{result.Transport}: {result.Consequence}"
            : $"{result.Transport} departed — arriving {UiKit.Countdown(result.ArrivesAt - _session.ServerNow)} from now.";

        _resultLine.AddThemeColorOverride("font_color", result.RiskTriggered ? UiKit.Danger : UiKit.Muted);
        await ReloadAsync();
    }

    private async Task StudyAsync()
    {
        var state = await _session.TryAsync(() => _session.Api.StartStudyAsync(), s => s);
        if (state is null) return;

        _resultLine.Text = "Enrolled. Come back in three hours.";
        await ReloadAsync();
    }

    private async Task ExamAsync(bool cheat)
    {
        var result = await _session.TryAsync(() => _session.Api.TakeExamAsync(cheat), r => r.State);
        if (result is null) return;

        _resultLine.Text = result switch
        {
            { Caught: true } => $"Caught. {UiKit.Money2(result.FinePaid)} fine, 48 hr bar, and a freeze.",
            { Passed: true, Cheated: true } => "Passed. Nobody asked any questions.",
            { Passed: true } => "Passed, fair and square.",
            _ => "Failed. Sit it again when you're ready.",
        };

        _resultLine.AddThemeColorOverride("font_color", result.Passed ? UiKit.Money : UiKit.Danger);
        await ReloadAsync();
    }

    private async Task FilePaperworkAsync()
    {
        var result = await _session.TryAsync(() => _session.Api.FilePaperworkAsync(), r => r.State);
        if (result is null) return;

        _resultLine.Text = $"Filed. {UiKit.Countdown(result.TimeRemoved)} off the clock.";
        await ReloadAsync();
    }

    private async Task LawyerAsync(bool partial)
    {
        var result = await _session.TryAsync(() => _session.Api.HireLawyerAsync(partial), r => r.State);
        if (result is null) return;

        _resultLine.Text = result.Freeze.Tier == FreezeTier.None
            ? $"Cleared for {UiKit.Money2(result.CostPaid)}."
            : $"{UiKit.Money2(result.CostPaid)} spent, {UiKit.Countdown(result.TimeRemoved)} knocked off.";

        await ReloadAsync();
    }
}
