namespace Loopholes.Contracts;

// ---------------------------------------------------------------------------
// Auth
// ---------------------------------------------------------------------------

public sealed record RegisterRequest(string DeviceId, string DisplayName);

public sealed record AuthResponse(string PlayerId, string Token, string DisplayName);

// ---------------------------------------------------------------------------
// Player state
// ---------------------------------------------------------------------------

/// <summary>
/// Everything the client needs to render the HUD. Energy and freeze timers are
/// resolved server-side at the moment of the request, so the client only ever
/// renders values it was handed — it never advances them itself.
/// </summary>
public sealed record PlayerStateDto(
    string PlayerId,
    string DisplayName,
    decimal Cash,
    int Favor,
    EnergyDto Energy,
    TrustDto Trust,
    LicenseDto License,
    FreezeDto Freeze,
    int InventoryUsed,
    int InventoryCap,
    DateTimeOffset ServerTime);

public sealed record EnergyDto(int Current, int Max, DateTimeOffset? NextPointAt, DateTimeOffset? FullAt);

public sealed record TrustDto(int Score, TrustTier Tier);

public sealed record LicenseDto(LicenseStatus Status, DateTimeOffset? StudyCompletesAt, DateTimeOffset? RetryAvailableAt, bool ObtainedByCheating);

public sealed record FreezeDto(FreezeTier Tier, DateTimeOffset? EndsAt, DateTimeOffset? NextPaperworkAt);

// ---------------------------------------------------------------------------
// Jobs
// ---------------------------------------------------------------------------

public sealed record JobCatalogEntryDto(
    string Id,
    string Name,
    JobTier Tier,
    double DurationMinutes,
    int EnergyCost,
    decimal CashCost,
    decimal PayoutMin,
    decimal PayoutMax,
    double RiskChance,
    string? RiskDescription,
    string Notes,
    bool Unlocked,
    string? LockedReason);

public sealed record ActiveJobDto(
    string ActiveJobId,
    string JobId,
    string Name,
    DateTimeOffset StartedAt,
    DateTimeOffset CompletesAt,
    bool Collectable);

/// <summary>The jobs screen in one payload: what can be started, and what is already running.</summary>
public sealed record JobBoardDto(IReadOnlyList<JobCatalogEntryDto> Available, IReadOnlyList<ActiveJobDto> Active);

public sealed record StartJobRequest(string JobId);

public sealed record CollectJobRequest(string ActiveJobId);

/// <summary>Result of collecting a finished job. The payout roll happens on the server.</summary>
public sealed record JobResultDto(
    string JobId,
    string Name,
    decimal CashPaid,
    IReadOnlyList<ItemGrantDto> ItemsGranted,
    IReadOnlyList<ItemGrantDto> ItemsLost,
    bool RiskTriggered,
    string? RiskOutcome,
    FreezeDto Freeze,
    PlayerStateDto State);

public sealed record ItemGrantDto(string GoodId, string Name, int Quantity);

// ---------------------------------------------------------------------------
// Market
// ---------------------------------------------------------------------------

public sealed record MarketGoodDto(
    string Id,
    string Name,
    decimal BasePrice,
    decimal CurrentPrice,
    double ChangePercent1h,
    PriceBehavior Behavior,
    string BehaviorHint,
    bool Tradeable,
    string? ActiveEvent,
    DateTimeOffset? EventEndsAt);

public sealed record MarketQuoteDto(IReadOnlyList<MarketGoodDto> Goods, decimal TradingFeeRate, DateTimeOffset ServerTime);

public sealed record TradeRequest(string GoodId, int Quantity);

public sealed record TradeResultDto(
    string GoodId,
    string Name,
    int Quantity,
    decimal UnitPrice,
    decimal Gross,
    decimal Fee,
    decimal Net,
    PlayerStateDto State);

// ---------------------------------------------------------------------------
// Inventory
// ---------------------------------------------------------------------------

public sealed record InventoryStackDto(
    string GoodId,
    string Name,
    int Quantity,
    DateTimeOffset AcquiredAt,
    int DaysHeld,
    decimal UnitValue,
    decimal StackValue,
    double? AppreciationPercent);

public sealed record InventoryDto(IReadOnlyList<InventoryStackDto> Stacks, int Used, int Cap, decimal TotalValue);

// ---------------------------------------------------------------------------
// Travel
// ---------------------------------------------------------------------------

public sealed record TravelOptionDto(
    Transport Transport,
    double DurationMinutes,
    double BaseRiskPercent,
    double AdjustedRiskPercent,
    bool LicenseDiscountApplied);

public sealed record StartTravelRequest(Transport Transport, string Destination);

public sealed record TravelResultDto(
    Transport Transport,
    string Destination,
    DateTimeOffset ArrivesAt,
    bool RiskTriggered,
    string? Consequence,
    decimal CargoFinePaid,
    PlayerStateDto State);

// ---------------------------------------------------------------------------
// Driver's license
// ---------------------------------------------------------------------------

public sealed record TakeExamRequest(bool Cheat);

public sealed record ExamResultDto(
    bool Passed,
    bool Cheated,
    bool Caught,
    decimal FinePaid,
    LicenseDto License,
    FreezeDto Freeze,
    PlayerStateDto State);

// ---------------------------------------------------------------------------
// Audit freeze
// ---------------------------------------------------------------------------

public sealed record ClearFreezeRequest(bool PartialLawyer);

public sealed record FreezeActionResultDto(
    string Action,
    decimal CostPaid,
    TimeSpan TimeRemoved,
    FreezeDto Freeze,
    PlayerStateDto State);

// ---------------------------------------------------------------------------
// Leaderboard
// ---------------------------------------------------------------------------

public sealed record LeaderboardEntryDto(
    int Rank,
    string PlayerId,
    string DisplayName,
    decimal Score,
    TrustTier TrustTier,
    int TrustScore);

public sealed record LeaderboardDto(
    LeaderboardMode Mode,
    int SeasonNumber,
    DateTimeOffset SeasonStartedAt,
    DateTimeOffset SeasonEndsAt,
    IReadOnlyList<LeaderboardEntryDto> Entries,
    LeaderboardEntryDto? You);

// ---------------------------------------------------------------------------
// Stats / history
// ---------------------------------------------------------------------------

public sealed record TrustHistoryEntryDto(DateTimeOffset At, TrustEventKind Kind, int Delta, int ScoreAfter, string Description);

public sealed record StatsDto(
    decimal LifetimeCashEarned,
    int JobsCompleted,
    decimal BestEmpireScore,
    int? BestSeasonRank,
    int TimesFrozen,
    TrustDto Trust,
    LicenseDto License,
    IReadOnlyList<TrustHistoryEntryDto> TrustHistory);

// ---------------------------------------------------------------------------
// Cosmetics
// ---------------------------------------------------------------------------

public sealed record CosmeticDto(string Id, string Name, string Slot, int FavorCost, bool Owned, bool Equipped);

public sealed record WardrobeDto(IReadOnlyList<CosmeticDto> Items, int Favor);

public sealed record CosmeticRequest(string CosmeticId);

// ---------------------------------------------------------------------------
// Errors
// ---------------------------------------------------------------------------

public sealed record ErrorDto(string Code, string Message);
