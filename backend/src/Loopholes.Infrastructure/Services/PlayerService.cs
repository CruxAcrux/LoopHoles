using Loopholes.Contracts;
using Loopholes.Domain;
using Microsoft.EntityFrameworkCore;

namespace Loopholes.Infrastructure.Services;

/// <summary>
/// Loads a player and brings their derived state up to the current instant:
/// energy regen, trust idle decay, expired freezes, completed travel. Every
/// endpoint calls <see cref="LoadAsync"/> first, so no timer needs a scheduler.
/// </summary>
public sealed class PlayerService(LoopholesDbContext db, IClock clock, MarketPriceStore prices)
{
    public async Task<Player> LoadAsync(Guid playerId, CancellationToken ct = default)
    {
        var player = await db.Players
            .Include(p => p.Inventory)
            .Include(p => p.ActiveJobs)
            .FirstOrDefaultAsync(p => p.Id == playerId, ct)
            ?? throw new GameRuleException("player_not_found", "No such player.");

        Resolve(player);
        return player;
    }

    /// <summary>
    /// Advances everything time-based on the player. Safe to call repeatedly —
    /// each step is idempotent for a given instant.
    /// </summary>
    public void Resolve(Player p)
    {
        var now = clock.UtcNow;

        var (energy, stamp) = EnergyMath.Regenerate(p.EnergySnapshot, p.EnergyUpdatedAt, now);
        p.EnergySnapshot = energy;
        p.EnergyUpdatedAt = stamp;

        var (trust, decayedAt) = TrustMath.ApplyIdleDecay(p.TrustScore, p.TrustDecayedAt, now);
        p.TrustScore = trust;
        p.TrustDecayedAt = decayedAt;

        if (p.FreezeTier != FreezeTier.None && p.FreezeEndsAt is { } ends && ends <= now)
        {
            p.FreezeTier = FreezeTier.None;
            p.FreezeEndsAt = null;
            p.FreezeNextPaperworkAt = null;
        }

        if (p.LicenseStatus == LicenseStatus.Studying && p.LicenseStudyCompletesAt is { } study && study <= now)
        {
            // Study finished, but the exam is still a deliberate player action.
            p.LicenseStudyCompletesAt = study;
        }

        if (p.LicenseStatus == LicenseStatus.Suspended && p.LicenseRetryAvailableAt is { } retry && retry <= now)
        {
            p.LicenseStatus = LicenseStatus.NotOwned;
            p.LicenseRetryAvailableAt = null;
        }

        if (p.TravelArrivesAt is { } arrives && arrives <= now)
        {
            p.TravelArrivesAt = null;
            p.TravelTransport = null;
            p.TravelDestination = null;
        }

        p.LastSeenAt = now;
    }

    // --- Resource mutation ------------------------------------------------

    public void SpendEnergy(Player p, int amount)
    {
        if (p.EnergySnapshot < amount)
            throw new GameRuleException("insufficient_energy", $"Needs {amount} energy, has {p.EnergySnapshot}.");

        // Dropping below the cap starts the regen clock from now.
        if (p.EnergySnapshot >= GameData.MaxEnergy)
            p.EnergyUpdatedAt = clock.UtcNow;

        p.EnergySnapshot -= amount;
    }

    public static void SpendCash(Player p, decimal amount)
    {
        if (amount <= 0) return;
        if (p.Cash < amount)
            throw new GameRuleException("insufficient_cash", $"Needs ${amount:0.00}, has ${p.Cash:0.00}.");
        p.Cash -= amount;
    }

    public static void EarnCash(Player p, decimal amount)
    {
        if (amount <= 0) return;
        p.Cash += amount;
        p.LifetimeCashEarned += amount;
    }

    public static void SpendFavor(Player p, int amount)
    {
        if (amount <= 0) return;
        if (p.Favor < amount)
            throw new GameRuleException("insufficient_favor", $"Needs {amount} Favor, has {p.Favor}.");
        p.Favor -= amount;
    }

    // --- Freeze gating ----------------------------------------------------

    /// <summary>
    /// A Minor freeze locks jobs and travel but leaves market trading open; a Major
    /// freeze locks everything (brief §11).
    /// </summary>
    public static void RequireNotFrozen(Player p, FreezeGate gate)
    {
        if (p.FreezeTier == FreezeTier.None) return;

        var blocked = p.FreezeTier == FreezeTier.Major || gate != FreezeGate.Market;
        if (blocked)
            throw new GameRuleException("under_investigation",
                $"Blocked by a {p.FreezeTier} Freeze until {p.FreezeEndsAt:u}.");
    }

    // --- Inventory --------------------------------------------------------

    public int InventoryUsed(Player p) => p.Inventory.Sum(s => s.Quantity);

    /// <summary>
    /// Adds to a stack, moving its acquisition time to the quantity-weighted
    /// average. Buying more Vintage Furniture therefore dilutes hold time instead
    /// of inheriting the oldest unit's appreciation.
    /// </summary>
    public void AddItem(Player p, string goodId, int quantity)
    {
        if (quantity <= 0) return;

        if (InventoryUsed(p) + quantity > p.InventoryCap)
            throw new GameRuleException("inventory_full",
                $"Storage holds {p.InventoryCap}; {InventoryUsed(p)} used.");

        var now = clock.UtcNow;
        var stack = p.Inventory.FirstOrDefault(s => s.GoodId == goodId);

        if (stack is null)
        {
            stack = new InventoryStack { PlayerId = p.Id, GoodId = goodId, Quantity = quantity, AcquiredAt = now };
            p.Inventory.Add(stack);
            db.InventoryStacks.Add(stack);
            return;
        }

        var totalQuantity = stack.Quantity + quantity;
        var weightedTicks = ((stack.AcquiredAt.UtcTicks * stack.Quantity) + (now.UtcTicks * (long)quantity)) / totalQuantity;
        stack.AcquiredAt = new DateTimeOffset(weightedTicks, TimeSpan.Zero);
        stack.Quantity = totalQuantity;
    }

    public void RemoveItem(Player p, string goodId, int quantity)
    {
        var stack = p.Inventory.FirstOrDefault(s => s.GoodId == goodId)
            ?? throw new GameRuleException("not_held", $"No {goodId} in inventory.");

        if (stack.Quantity < quantity)
            throw new GameRuleException("not_held", $"Holds {stack.Quantity}x {goodId}, needs {quantity}.");

        stack.Quantity -= quantity;
        if (stack.Quantity == 0)
        {
            p.Inventory.Remove(stack);
            db.InventoryStacks.Remove(stack);
        }
    }

    public decimal InventoryValue(Player p, IReadOnlyDictionary<string, MarketGoodState> states)
    {
        var now = clock.UtcNow;
        decimal total = 0;

        foreach (var stack in p.Inventory)
        {
            var good = GameData.FindGood(stack.GoodId);
            if (good is null || !states.TryGetValue(stack.GoodId, out var state)) continue;
            total += MarketPriceStore.UnitPrice(good, state, now - stack.AcquiredAt) * stack.Quantity;
        }

        return Math.Round(total, 2, MidpointRounding.AwayFromZero);
    }

    // --- Trust ------------------------------------------------------------

    public void ApplyTrust(Player p, int delta, TrustEventKind kind, string description)
    {
        var before = p.TrustScore;
        p.TrustScore = TrustMath.Clamp(p.TrustScore + delta);

        var entry = new TrustEvent
        {
            PlayerId = p.Id,
            At = clock.UtcNow,
            Kind = kind,
            Delta = p.TrustScore - before,
            ScoreAfter = p.TrustScore,
            Description = description,
        };

        p.TrustHistory.Add(entry);
        db.TrustEvents.Add(entry);
    }

    // --- Projection -------------------------------------------------------

    public async Task<PlayerStateDto> ToDtoAsync(Player p, CancellationToken ct = default)
    {
        var states = await prices.GetStatesAsync(ct);
        return ToDto(p, states);
    }

    public PlayerStateDto ToDto(Player p, IReadOnlyDictionary<string, MarketGoodState> states) => new(
        p.Id.ToString(),
        p.DisplayName,
        Math.Round(p.Cash, 2, MidpointRounding.AwayFromZero),
        p.Favor,
        new EnergyDto(
            p.EnergySnapshot,
            GameData.MaxEnergy,
            EnergyMath.NextPointAt(p.EnergySnapshot, p.EnergyUpdatedAt),
            EnergyMath.FullAt(p.EnergySnapshot, p.EnergyUpdatedAt)),
        new TrustDto(p.TrustScore, TrustMath.TierFor(p.TrustScore)),
        new LicenseDto(p.LicenseStatus, p.LicenseStudyCompletesAt, p.LicenseRetryAvailableAt, p.LicenseObtainedByCheating),
        FreezeDto(p),
        InventoryUsed(p),
        p.InventoryCap,
        clock.UtcNow);

    public static FreezeDto FreezeDto(Player p) =>
        new(p.FreezeTier, p.FreezeEndsAt, p.FreezeNextPaperworkAt);
}

public enum FreezeGate
{
    Jobs,
    Market,
    Travel,
    License,
}
