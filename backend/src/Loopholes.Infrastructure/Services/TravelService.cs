using Loopholes.Contracts;
using Loopholes.Domain;

namespace Loopholes.Infrastructure.Services;

/// <summary>
/// Travel to meetings and exclusive markets. The risk roll happens exactly once
/// per trip, at departure, and is scaled by trust — the design link that makes
/// playing honestly mechanically cheaper (brief §9).
/// </summary>
public sealed class TravelService(
    IClock clock,
    IRandomSource rng,
    PlayerService players,
    MarketPriceStore prices)
{
    public IReadOnlyList<TravelOptionDto> Options(Player p)
    {
        var hasLicense = p.LicenseStatus == LicenseStatus.Owned;

        return [.. GameData.Travel.Values.Select(def =>
        {
            var duration = TravelMath.Duration(def.Transport, hasLicense);
            return new TravelOptionDto(
                def.Transport,
                duration.TotalMinutes,
                def.BaseRisk,
                TravelMath.AdjustedRisk(def.BaseRisk, p.TrustScore),
                def.Transport == Transport.Car && hasLicense);
        })];
    }

    public async Task<TravelResultDto> StartAsync(Player p, Transport transport, string destination, CancellationToken ct = default)
    {
        PlayerService.RequireNotFrozen(p, FreezeGate.Travel);

        if (p.TravelArrivesAt is { } arrives)
            throw new GameRuleException("already_travelling", $"Already in transit until {arrives:u}.");

        if (!GameData.Travel.TryGetValue(transport, out var def))
            throw new GameRuleException("unknown_transport", $"No transport '{transport}'.");

        var now = clock.UtcNow;
        var hasLicense = p.LicenseStatus == LicenseStatus.Owned;
        var duration = TravelMath.Duration(transport, hasLicense);

        var risk = TravelMath.AdjustedRisk(def.BaseRisk, p.TrustScore);
        var triggered = rng.Chance(risk);

        string? consequence = null;
        decimal fine = 0m;

        if (triggered)
        {
            // Two consequences of equal weight: a cut of the cargo, or a delayed arrival.
            if (rng.Chance(0.5))
            {
                var states = await prices.GetStatesAsync(ct);
                var cargoValue = players.InventoryValue(p, states);
                fine = Math.Min(
                    Math.Round(cargoValue * GameData.TravelCargoFineRate, 2, MidpointRounding.AwayFromZero),
                    p.Cash);

                PlayerService.SpendCash(p, fine);
                consequence = fine > 0
                    ? $"Customs assessed a ${fine:0.00} cargo fine."
                    : "Customs stopped the shipment, but there was nothing worth fining.";
            }
            else
            {
                duration *= GameData.TravelDelayMultiplier;
                consequence = "Held up in transit — arrival delayed by 50%.";
            }
        }

        p.TravelTransport = transport;
        p.TravelDestination = destination;
        p.TravelArrivesAt = now + duration;

        return new TravelResultDto(
            transport, destination, p.TravelArrivesAt.Value, triggered, consequence, fine,
            players.ToDto(p, await prices.GetStatesAsync(ct)));
    }
}
