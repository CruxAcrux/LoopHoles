using Loopholes.Contracts;
using Loopholes.Domain;

namespace Loopholes.Infrastructure.Services;

/// <summary>
/// The driver's licence — optional, never a hard gate, and deliberately the one
/// place where cheating has a purely local consequence. Getting caught costs
/// cash, time and a freeze, but it does <em>not</em> touch the interpersonal
/// trust score, because no other player was wronged (brief §10).
/// </summary>
public sealed class LicenseService(
    IClock clock,
    IRandomSource rng,
    PlayerService players,
    MarketPriceStore prices,
    FreezeService freezes)
{
    /// <summary>
    /// Pays the licence fee and starts the passive study timer. The fee is charged
    /// once per attempt path: here for the legit route, or at exam time when
    /// cheating — either way you pay for the licence itself, only the studying is
    /// what gets skipped.
    /// </summary>
    public async Task<PlayerStateDto> StartStudyAsync(Player p, CancellationToken ct = default)
    {
        PlayerService.RequireNotFrozen(p, FreezeGate.License);
        RequireCanAttempt(p);

        if (p.LicenseStatus == LicenseStatus.Studying)
            throw new GameRuleException("already_studying", $"Already studying until {p.LicenseStudyCompletesAt:u}.");

        PlayerService.SpendCash(p, GameData.LicenseCost);

        p.LicenseStatus = LicenseStatus.Studying;
        p.LicenseStudyCompletesAt = clock.UtcNow.AddHours(GameData.LicenseStudyHours);

        return players.ToDto(p, await prices.GetStatesAsync(ct));
    }

    public async Task<ExamResultDto> TakeExamAsync(Player p, bool cheat, CancellationToken ct = default)
    {
        PlayerService.RequireNotFrozen(p, FreezeGate.License);
        RequireCanAttempt(p);

        var now = clock.UtcNow;
        decimal fine = 0m;
        bool passed, caught = false;

        if (cheat)
        {
            PlayerService.SpendCash(p, GameData.LicenseCost);

            caught = rng.Chance(GameData.LicenseCheatCatchRate);
            passed = !caught;

            if (caught)
            {
                p.CheatingOffenses++;
                fine = Math.Min(GameData.LicenseCaughtFine, p.Cash);
                PlayerService.SpendCash(p, fine);

                p.LicenseStatus = LicenseStatus.Suspended;
                p.LicenseRetryAvailableAt = now.AddHours(GameData.LicenseCaughtCooldownHours);
                p.LicenseStudyCompletesAt = null;

                // First offence is a Minor Freeze; repeat offenders escalate to Major.
                var tier = p.CheatingOffenses >= 2 ? FreezeTier.Major : FreezeTier.Minor;
                freezes.Apply(p, tier, "caught cheating on the licence exam");
            }
            else
            {
                Issue(p, byCheating: true);
            }
        }
        else
        {
            if (p.LicenseStatus != LicenseStatus.Studying)
                throw new GameRuleException("study_required", "Pay for and complete the study period first, or take the exam cold.");

            if (p.LicenseStudyCompletesAt is { } done && done > now)
                throw new GameRuleException("still_studying", $"Studying until {done:u}.");

            passed = rng.Chance(GameData.LicenseLegitPassRate);

            // A rare legit failure is a retake, not a punishment — study stays banked.
            if (passed)
                Issue(p, byCheating: false);
        }

        return new ExamResultDto(
            passed, cheat, caught, fine,
            new LicenseDto(p.LicenseStatus, p.LicenseStudyCompletesAt, p.LicenseRetryAvailableAt, p.LicenseObtainedByCheating),
            PlayerService.FreezeDto(p),
            players.ToDto(p, await prices.GetStatesAsync(ct)));
    }

    private static void Issue(Player p, bool byCheating)
    {
        p.LicenseStatus = LicenseStatus.Owned;
        p.LicenseObtainedByCheating = byCheating;
        p.LicenseStudyCompletesAt = null;
        p.LicenseRetryAvailableAt = null;
    }

    private static void RequireCanAttempt(Player p)
    {
        if (p.LicenseStatus == LicenseStatus.Owned)
            throw new GameRuleException("already_licensed", "Licence already held.");

        if (p.LicenseStatus == LicenseStatus.Suspended && p.LicenseRetryAvailableAt is { } retry)
            throw new GameRuleException("license_cooldown", $"Barred from retaking until {retry:u}.");
    }
}
