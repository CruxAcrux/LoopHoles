using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Loopholes.Client.Net;

// -----------------------------------------------------------------------------
// The wire format, as seen from the client.
//
// These are a deliberate, independent copy of the server's JSON shape — the game
// and the service share no assemblies and reference no common project. That means
// this file is the client's contract with /api/v1, and a server change that isn't
// backwards compatible has to be handled here as a version bump, exactly as it
// would be for any third-party consumer.
//
// Only fields the client actually uses are declared. Unknown JSON properties are
// ignored on deserialization, so the server can add fields without breaking us.
// -----------------------------------------------------------------------------

// Enums travel as names, not ordinals, so adding a value server-side can't silently
// reinterpret an existing one. The attribute form (rather than a converter in the
// options) is what keeps this working under the AOT compilation an iOS export needs.

[JsonConverter(typeof(JsonStringEnumConverter<JobTier>))]
public enum JobTier { Hustle = 1, Operation = 2 }

[JsonConverter(typeof(JsonStringEnumConverter<TrustTier>))]
public enum TrustTier { Untrustworthy, Unproven, Trusted, Elite }

[JsonConverter(typeof(JsonStringEnumConverter<FreezeTier>))]
public enum FreezeTier { None = 0, Minor = 1, Major = 2 }

[JsonConverter(typeof(JsonStringEnumConverter<LicenseStatus>))]
public enum LicenseStatus { NotOwned, Studying, Owned, Suspended }

[JsonConverter(typeof(JsonStringEnumConverter<Transport>))]
public enum Transport { Car, Jet, Freight }

[JsonConverter(typeof(JsonStringEnumConverter<PriceBehavior>))]
public enum PriceBehavior { Steady, Volatile, ScheduledSpike, EventDriven, Seasonal, Bubble, Appreciating }

[JsonConverter(typeof(JsonStringEnumConverter<TrustEventKind>))]
public enum TrustEventKind { FairSplit, Betrayal, Restitution, CleanStreak, IdleDecay, AuditFreeze }

[JsonConverter(typeof(JsonStringEnumConverter<LeaderboardMode>))]
public enum LeaderboardMode { Empire, Richest, MostTrusted }

// --- Auth --------------------------------------------------------------------

public sealed record RegisterRequest(string DeviceId, string DisplayName);

public sealed record AuthResponse(string PlayerId, string Token, string DisplayName);

// --- Player state ------------------------------------------------------------

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

public sealed record LicenseDto(
    LicenseStatus Status,
    DateTimeOffset? StudyCompletesAt,
    DateTimeOffset? RetryAvailableAt,
    bool ObtainedByCheating);

public sealed record FreezeDto(FreezeTier Tier, DateTimeOffset? EndsAt, DateTimeOffset? NextPaperworkAt);

// --- Jobs --------------------------------------------------------------------

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

public sealed record JobBoardDto(List<JobCatalogEntryDto> Available, List<ActiveJobDto> Active);

public sealed record StartJobRequest(string JobId);

public sealed record CollectJobRequest(string ActiveJobId);

public sealed record ItemGrantDto(string GoodId, string Name, int Quantity);

public sealed record JobResultDto(
    string JobId,
    string Name,
    decimal CashPaid,
    List<ItemGrantDto> ItemsGranted,
    List<ItemGrantDto> ItemsLost,
    bool RiskTriggered,
    string? RiskOutcome,
    FreezeDto Freeze,
    PlayerStateDto State);

// --- Market ------------------------------------------------------------------

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

public sealed record MarketQuoteDto(List<MarketGoodDto> Goods, decimal TradingFeeRate, DateTimeOffset ServerTime);

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

// --- Inventory ---------------------------------------------------------------

public sealed record InventoryStackDto(
    string GoodId,
    string Name,
    int Quantity,
    DateTimeOffset AcquiredAt,
    int DaysHeld,
    decimal UnitValue,
    decimal StackValue,
    double? AppreciationPercent);

public sealed record InventoryDto(List<InventoryStackDto> Stacks, int Used, int Cap, decimal TotalValue);

// --- Travel ------------------------------------------------------------------

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

// --- Driver's licence --------------------------------------------------------

public sealed record TakeExamRequest(bool Cheat);

public sealed record ExamResultDto(
    bool Passed,
    bool Cheated,
    bool Caught,
    decimal FinePaid,
    LicenseDto License,
    FreezeDto Freeze,
    PlayerStateDto State);

// --- Audit freeze ------------------------------------------------------------

public sealed record ClearFreezeRequest(bool PartialLawyer);

public sealed record FreezeActionResultDto(
    string Action,
    decimal CostPaid,
    TimeSpan TimeRemoved,
    FreezeDto Freeze,
    PlayerStateDto State);

// --- Leaderboard -------------------------------------------------------------

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
    List<LeaderboardEntryDto> Entries,
    LeaderboardEntryDto? You);

// --- Stats and wardrobe ------------------------------------------------------

public sealed record TrustHistoryEntryDto(
    DateTimeOffset At,
    TrustEventKind Kind,
    int Delta,
    int ScoreAfter,
    string Description);

public sealed record StatsDto(
    decimal LifetimeCashEarned,
    int JobsCompleted,
    decimal BestEmpireScore,
    int? BestSeasonRank,
    int TimesFrozen,
    TrustDto Trust,
    LicenseDto License,
    List<TrustHistoryEntryDto> TrustHistory);

public sealed record CosmeticDto(string Id, string Name, string Slot, int FavorCost, bool Owned, bool Equipped);

public sealed record WardrobeDto(List<CosmeticDto> Items, int Favor);

public sealed record CosmeticRequest(string CosmeticId);

// --- Errors ------------------------------------------------------------------

public sealed record ErrorDto(string Code, string Message);
