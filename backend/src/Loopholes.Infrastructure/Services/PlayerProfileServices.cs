using System.Security.Cryptography;
using System.Text;
using Loopholes.Contracts;
using Loopholes.Domain;
using Microsoft.EntityFrameworkCore;

namespace Loopholes.Infrastructure.Services;

/// <summary>
/// Device-bound accounts. Good enough for an MVP where the account <em>is</em> the
/// handset: the device registers once and gets a bearer token, and only the SHA-256
/// hash of that token is ever stored.
/// </summary>
public sealed class AuthService(LoopholesDbContext db, IClock clock)
{
    public async Task<AuthResponse> RegisterOrLoginAsync(string deviceId, string displayName, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(deviceId))
            throw new GameRuleException("invalid_device", "A device id is required.");

        var name = string.IsNullOrWhiteSpace(displayName) ? "Anonymous LLC" : displayName.Trim();
        if (name.Length > 32) name = name[..32];

        var now = clock.UtcNow;
        var player = await db.Players.FirstOrDefaultAsync(p => p.DeviceId == deviceId, ct);

        if (player is null)
        {
            player = new Player
            {
                DeviceId = deviceId,
                DisplayName = name,
                CreatedAt = now,
                LastSeenAt = now,
                EnergyUpdatedAt = now,
                TrustDecayedAt = now,
            };

            // Everyone opens the wardrobe on day one with something already on.
            var starter = GameData.Cosmetics.First(c => c.IsStarter);
            player.Cosmetics.Add(new PlayerCosmetic
            {
                PlayerId = player.Id,
                CosmeticId = starter.Id,
                Equipped = true,
                AcquiredAt = now,
            });

            db.Players.Add(player);
        }

        var token = GenerateToken();
        player.TokenHash = HashToken(token);
        player.LastSeenAt = now;

        await db.SaveChangesAsync(ct);
        return new AuthResponse(player.Id.ToString(), token, player.DisplayName);
    }

    public async Task<Player?> ResolveAsync(string token, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(token)) return null;
        var hash = HashToken(token);
        return await db.Players.FirstOrDefaultAsync(p => p.TokenHash == hash, ct);
    }

    private static string GenerateToken() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static string HashToken(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}

/// <summary>The "receipts" screen: lifetime totals plus the dated trust log behind the badge (brief §15).</summary>
public sealed class StatsService(LoopholesDbContext db)
{
    public const int HistoryPageSize = 50;

    public async Task<StatsDto> GetAsync(Player p, CancellationToken ct = default)
    {
        var history = await db.TrustEvents
            .Where(t => t.PlayerId == p.Id)
            .OrderByDescending(t => t.At)
            .Take(HistoryPageSize)
            .Select(t => new TrustHistoryEntryDto(t.At, t.Kind, t.Delta, t.ScoreAfter, t.Description))
            .ToListAsync(ct);

        return new StatsDto(
            p.LifetimeCashEarned,
            p.JobsCompleted,
            p.BestEmpireScore,
            p.BestSeasonRank,
            p.TimesFrozen,
            new TrustDto(p.TrustScore, TrustMath.TierFor(p.TrustScore)),
            new LicenseDto(p.LicenseStatus, p.LicenseStudyCompletesAt, p.LicenseRetryAvailableAt, p.LicenseObtainedByCheating),
            history);
    }
}

/// <summary>
/// Cosmetics. Priced in Favor, purely visual, and structurally incapable of
/// touching a stat — nothing here writes to cash, energy, trust or risk.
/// </summary>
public sealed class WardrobeService(LoopholesDbContext db, IClock clock)
{
    public async Task<WardrobeDto> GetAsync(Player p, CancellationToken ct = default)
    {
        var owned = await db.PlayerCosmetics.Where(c => c.PlayerId == p.Id).ToListAsync(ct);

        var items = GameData.Cosmetics.Select(c =>
        {
            var mine = owned.FirstOrDefault(o => o.CosmeticId == c.Id);
            return new CosmeticDto(c.Id, c.Name, c.Slot, c.FavorCost, mine is not null, mine?.Equipped ?? false);
        }).ToList();

        return new WardrobeDto(items, p.Favor);
    }

    public async Task<WardrobeDto> BuyAsync(Player p, string cosmeticId, CancellationToken ct = default)
    {
        var cosmetic = GameData.FindCosmetic(cosmeticId)
            ?? throw new GameRuleException("unknown_cosmetic", $"No cosmetic '{cosmeticId}'.");

        var already = await db.PlayerCosmetics.AnyAsync(c => c.PlayerId == p.Id && c.CosmeticId == cosmeticId, ct);
        if (already)
            throw new GameRuleException("already_owned", $"{cosmetic.Name} is already in the wardrobe.");

        PlayerService.SpendFavor(p, cosmetic.FavorCost);

        db.PlayerCosmetics.Add(new PlayerCosmetic
        {
            PlayerId = p.Id,
            CosmeticId = cosmetic.Id,
            Equipped = false,
            AcquiredAt = clock.UtcNow,
        });

        await db.SaveChangesAsync(ct);
        return await GetAsync(p, ct);
    }

    public async Task<WardrobeDto> EquipAsync(Player p, string cosmeticId, CancellationToken ct = default)
    {
        var cosmetic = GameData.FindCosmetic(cosmeticId)
            ?? throw new GameRuleException("unknown_cosmetic", $"No cosmetic '{cosmeticId}'.");

        var owned = await db.PlayerCosmetics.Where(c => c.PlayerId == p.Id).ToListAsync(ct);

        var target = owned.FirstOrDefault(c => c.CosmeticId == cosmeticId)
            ?? throw new GameRuleException("not_owned", $"{cosmetic.Name} isn't owned.");

        // One item per slot.
        var slotMates = owned.Where(c => GameData.FindCosmetic(c.CosmeticId)?.Slot == cosmetic.Slot);
        foreach (var item in slotMates)
            item.Equipped = false;

        target.Equipped = true;

        await db.SaveChangesAsync(ct);
        return await GetAsync(p, ct);
    }
}
