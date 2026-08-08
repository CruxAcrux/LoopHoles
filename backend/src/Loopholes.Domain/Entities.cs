using Loopholes.Contracts;

namespace Loopholes.Domain;

public sealed class Player
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string DeviceId { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string TokenHash { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset LastSeenAt { get; set; }

    public decimal Cash { get; set; } = GameData.StartingCash;
    public int Favor { get; set; } = GameData.StartingFavor;

    /// <summary>
    /// Energy is stored lazily: a snapshot plus the instant it was taken. Current
    /// energy is derived on read, so a client that never calls in still regenerates
    /// correctly and can't forge a value.
    /// </summary>
    public int EnergySnapshot { get; set; } = GameData.MaxEnergy;
    public DateTimeOffset EnergyUpdatedAt { get; set; }

    public int TrustScore { get; set; } = GameData.TrustStart;
    /// <summary>Last time idle decay was applied — decay pulls toward neutral, not upward.</summary>
    public DateTimeOffset TrustDecayedAt { get; set; }

    public LicenseStatus LicenseStatus { get; set; } = LicenseStatus.NotOwned;
    public DateTimeOffset? LicenseStudyCompletesAt { get; set; }
    public DateTimeOffset? LicenseRetryAvailableAt { get; set; }
    public bool LicenseObtainedByCheating { get; set; }

    public FreezeTier FreezeTier { get; set; } = FreezeTier.None;
    public DateTimeOffset? FreezeEndsAt { get; set; }
    public DateTimeOffset? FreezeNextPaperworkAt { get; set; }
    /// <summary>Drives the Minor-then-Major escalation on repeat cheating (brief §11).</summary>
    public int CheatingOffenses { get; set; }

    public int InventoryCap { get; set; } = GameData.StartingInventoryCap;
    public bool HasPaperworkDiscount { get; set; }

    public Transport? TravelTransport { get; set; }
    public string? TravelDestination { get; set; }
    public DateTimeOffset? TravelArrivesAt { get; set; }

    // Lifetime stats — permanent, never reset by a season rollover (brief §15).
    public decimal LifetimeCashEarned { get; set; }
    public int JobsCompleted { get; set; }
    public int TimesFrozen { get; set; }
    public decimal BestEmpireScore { get; set; }
    public int? BestSeasonRank { get; set; }

    public List<ActiveJob> ActiveJobs { get; set; } = [];
    public List<InventoryStack> Inventory { get; set; } = [];
    public List<TrustEvent> TrustHistory { get; set; } = [];
    public List<PlayerCosmetic> Cosmetics { get; set; } = [];
}

public sealed class ActiveJob
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PlayerId { get; set; }
    public Player? Player { get; set; }

    public string JobId { get; set; } = "";
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset CompletesAt { get; set; }

    /// <summary>
    /// The payout roll is made when the job starts and stored here, so a player
    /// cannot re-roll it by retrying the collect call. Only revealed on collect.
    /// </summary>
    public decimal RolledPayout { get; set; }

    /// <summary>Risk outcome, also decided at start time and sealed until collect.</summary>
    public bool RiskTriggered { get; set; }

    /// <summary>For Pop-up Storefront, which accrues continuously.</summary>
    public decimal CollectedSoFar { get; set; }
}

public sealed class InventoryStack
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PlayerId { get; set; }
    public Player? Player { get; set; }

    public string GoodId { get; set; } = "";
    public int Quantity { get; set; }

    /// <summary>
    /// Weighted-average acquisition time. Adding to a stack moves this forward
    /// proportionally, so buying more Vintage Furniture dilutes hold time rather
    /// than resetting or free-riding on it.
    /// </summary>
    public DateTimeOffset AcquiredAt { get; set; }
}

public sealed class TrustEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PlayerId { get; set; }
    public Player? Player { get; set; }

    public DateTimeOffset At { get; set; }
    public TrustEventKind Kind { get; set; }
    public int Delta { get; set; }
    public int ScoreAfter { get; set; }
    public string Description { get; set; } = "";
}

public sealed class PlayerCosmetic
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PlayerId { get; set; }
    public Player? Player { get; set; }

    public string CosmeticId { get; set; } = "";
    public bool Equipped { get; set; }
    public DateTimeOffset AcquiredAt { get; set; }
}

/// <summary>Live price state for one market good. There is exactly one row per good.</summary>
public sealed class MarketGoodState
{
    public string GoodId { get; set; } = "";
    public decimal CurrentPrice { get; set; }
    public decimal PriceOneHourAgo { get; set; }
    public DateTimeOffset PriceOneHourAgoAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>Net units bought minus sold since the last tick — the player-driven pressure term.</summary>
    public int NetVolumeSinceTick { get; set; }

    public string? ActiveEvent { get; set; }
    public DateTimeOffset? EventStartedAt { get; set; }
    public DateTimeOffset? EventEndsAt { get; set; }

    /// <summary>Set after a Knockoff Sneakers spike so the following crash fires exactly once.</summary>
    public bool CrashPending { get; set; }
}

/// <summary>A 4-week leaderboard season. Only season scores reset; account progress never does.</summary>
public sealed class Season
{
    public int Number { get; set; }
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset EndsAt { get; set; }
    public bool Closed { get; set; }
}

public sealed class SeasonScore
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public int SeasonNumber { get; set; }
    public Guid PlayerId { get; set; }
    public Player? Player { get; set; }

    public decimal EmpireScore { get; set; }
    public decimal Cash { get; set; }
    public int TrustScore { get; set; }

    /// <summary>When this player first reached <see cref="EmpireScore"/> — ties break to whoever got there first.</summary>
    public DateTimeOffset ReachedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
