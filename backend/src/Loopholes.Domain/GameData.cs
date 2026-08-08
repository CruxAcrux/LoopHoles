using Loopholes.Contracts;

namespace Loopholes.Domain;

/// <summary>
/// Every tunable number in the game, transcribed from loopholes_economy_model.xlsx.
/// This is the single source of truth for balance: nothing else hardcodes an
/// economy value, so a balance pass means editing this file and re-running the
/// tests in Loopholes.Tests/BalanceTests.cs.
/// </summary>
public static class GameData
{
    // --- Global settings (sheet: Global Settings) --------------------------

    public const int MaxEnergy = 10;
    public const int EnergyRegenMinutes = 6;

    /// <summary>Starting cash for a brand new account — one Ghost Invoice's worth of runway.</summary>
    public const decimal StartingCash = 50m;
    public const int StartingFavor = 0;
    public const int StartingInventoryCap = 40;

    /// <summary>Cash sink on every market trade, applied to both buys and sells.</summary>
    public const decimal TradingFeeRate = 0.02m;

    /// <summary>Tier 2 opens once the player has finished this many Tier 1 hustles.</summary>
    public const int Tier2UnlockJobsCompleted = 5;

    // --- Trust (sheet: Trust System) ---------------------------------------

    public const int TrustStart = 50;
    public const int TrustNeutral = 50;
    public const int TrustMin = 0;
    public const int TrustMax = 100;
    public const int TrustFairSplit = 5;
    public const int TrustBetrayal = -30;
    public const int TrustIdleDecayPerDay = 1;
    public const int TrustCleanStreakRecovery = 8;
    public const decimal RestitutionMultiplier = 1.5m;

    public const int TrustMinorFreezePenalty = -10;
    public const int TrustMajorFreezePenalty = -25;

    public static TrustTier TierFor(int score) => score switch
    {
        <= 29 => TrustTier.Untrustworthy,
        <= 69 => TrustTier.Unproven,
        <= 89 => TrustTier.Trusted,
        _ => TrustTier.Elite,
    };

    // --- Travel (sheet: Travel Risk) ---------------------------------------

    public static readonly IReadOnlyDictionary<Transport, TravelDefinition> Travel =
        new Dictionary<Transport, TravelDefinition>
        {
            [Transport.Car] = new(Transport.Car, DurationMinutes: 120, BaseRisk: 0.08),
            [Transport.Jet] = new(Transport.Jet, DurationMinutes: 20, BaseRisk: 0.15),
            [Transport.Freight] = new(Transport.Freight, DurationMinutes: 360, BaseRisk: 0.03),
        };

    /// <summary>Share of cargo value taken as a fine on a failed travel roll.</summary>
    public const decimal TravelCargoFineRate = 0.20m;

    /// <summary>Alternative consequence on a failed roll: the trip takes this much longer.</summary>
    public const double TravelDelayMultiplier = 1.50;

    // --- Driver's license (sheet: Driver's License) ------------------------

    public const decimal LicenseCost = 500m;
    public const double LicenseStudyHours = 3;
    public const double LicenseLegitPassRate = 0.95;
    public const double LicenseCheatCatchRate = 0.35;
    public const decimal LicenseCaughtFine = 300m;
    public const double LicenseCaughtCooldownHours = 48;
    public const double LicenseCarTimeReduction = 0.25;

    // --- Audit freeze (sheet: Audit Freeze) --------------------------------

    public static readonly TimeSpan MinorFreezeDuration = TimeSpan.FromHours(4);
    public static readonly TimeSpan MajorFreezeDuration = TimeSpan.FromHours(24);

    public static readonly TimeSpan MinorPaperworkReduction = TimeSpan.FromMinutes(20);
    public static readonly TimeSpan MinorPaperworkCooldown = TimeSpan.FromMinutes(30);
    public static readonly TimeSpan MajorPaperworkReduction = TimeSpan.FromMinutes(45);
    public static readonly TimeSpan MajorPaperworkCooldown = TimeSpan.FromHours(1);

    public const decimal MinorLawyerCost = 400m;
    public const decimal MajorLawyerCost = 2500m;
    public const decimal MajorLawyerPartialCost = 1200m;
    public const double MajorLawyerPartialReduction = 0.50;

    // --- Leaderboard (brief §14) -------------------------------------------

    public static readonly TimeSpan SeasonLength = TimeSpan.FromDays(28);

    /// <summary>Empire Score multiplier applied per trust tier — rewards playing honestly, not just volume.</summary>
    public static decimal EmpireTrustModifier(TrustTier tier) => tier switch
    {
        TrustTier.Untrustworthy => 0.90m,
        TrustTier.Unproven => 1.00m,
        TrustTier.Trusted => 1.05m,
        TrustTier.Elite => 1.10m,
        _ => 1.00m,
    };

    // --- Jobs (sheets: Tier1 Jobs, Tier2 Jobs) -----------------------------

    public static readonly IReadOnlyList<JobDefinition> Jobs =
    [
        // Tier 1 — Hustles
        new("flip_crate", "Flip a Crate", JobTier.Hustle, 0.5, 1, 0m, 5m, 8m,
            Notes: "First-ever action, instant tap"),
        new("courier_run", "Courier Run", JobTier.Hustle, 3, 1, 0m, 10m, 15m,
            Notes: "First real timer"),
        new("sidewalk_sale", "Sidewalk Sale", JobTier.Hustle, 15, 2, 0m, 25m, 35m,
            Effect: JobEffect.RequiresHeldGood, Notes: "Sells a held market good, +bonus"),
        new("cook_books", "Cook the Books", JobTier.Hustle, 10, 2, 0m, 30m, 40m,
            Notes: "Soft preview of audit risk (no penalty yet)"),
        new("ghost_invoice", "Ghost Invoice", JobTier.Hustle, 30, 2, 50m, 90m, 120m,
            Notes: "First spend-to-earn job"),
        new("overnight_storage", "Overnight Storage Flip", JobTier.Hustle, 480, 3, 0m, 150m, 200m,
            Notes: "Start before bed, collect in morning"),
        new("sneaker_line", "Sneaker Line Save", JobTier.Hustle, 5, 1, 0m, 15m, 20m,
            Notes: "Timed around the Knockoff Sneakers drop event"),
        new("follower_farm", "Follower Farm Shift", JobTier.Hustle, 8, 1, 0m, 18m, 25m,
            Notes: "Foreshadows Bulk Influencer Followers"),
        new("pawn_goods", "Pawn the Goods", JobTier.Hustle, 20, 2, 0m, 35m, 45m,
            Effect: JobEffect.ClearsRandomItem, Notes: "Clears a random inventory item"),
        new("exam_key_delivery", "Exam Key Delivery", JobTier.Hustle, 25, 2, 20m, 55m, 70m,
            Notes: "Foreshadows Leaked Exam Keys seasonality"),
        new("furniture_pickup", "Furniture Pickup", JobTier.Hustle, 45, 2, 0m, 10m, 10m,
            Effect: JobEffect.GrantsItem, GrantsGoodId: "vintage_furniture", GrantsQuantity: 1,
            Notes: "Grants 1x Vintage Furniture + $10 cash — teaches delayed gratification"),
        new("late_shift_cleanup", "Late Shift Cleanup", JobTier.Hustle, 90, 3, 0m, 110m, 140m,
            Notes: "Second mid-length walk-away job"),

        // Tier 2 — Operations
        new("import_run", "Import Run", JobTier.Operation, 120, 4, 200m, 350m, 450m,
            Notes: "First multi-step job: market + timer"),
        new("warehouse_clearance", "Warehouse Clearance", JobTier.Operation, 240, 5, 400m, 700m, 900m,
            RiskChance: 0.08, RiskEffect: RiskEffect.LosePayoutShare, RiskMagnitude: 0.20,
            RiskDescription: "8% chance: lose 20% of payout and enter a Minor Freeze",
            Notes: "First real spot-check fine risk"),
        new("popup_storefront", "Pop-up Storefront", JobTier.Operation, 360, 5, 300m, 600m, 750m,
            Effect: JobEffect.PaysAcrossDuration, Notes: "Sells across duration, not one lump sum"),
        new("streaming_wholesale", "Streaming Code Wholesale", JobTier.Operation, 180, 4, 250m, 450m, 550m,
            Notes: "Teaches staggered selling"),
        new("chip_smuggling", "Chip Smuggling Route", JobTier.Operation, 150, 4, 300m, 400m, 700m,
            Effect: JobEffect.MarketLinkedPayout, MarketLinkGoodId: "grey_chips",
            Notes: "First job priced off the live market"),
        new("crackdown_dodge", "Crackdown Dodge", JobTier.Operation, 180, 4, 250m, 500m, 650m,
            RiskChance: 0.10, RiskEffect: RiskEffect.LoseGoodsShare, RiskMagnitude: 0.30,
            RiskDescription: "10% chance: lose 30% of goods and enter a Minor Freeze",
            Notes: "First genuine risk/reward T2 job"),
        new("vintage_flip", "Vintage Flip Circuit", JobTier.Operation, 300, 3, 50m, 100m, 100m,
            Effect: JobEffect.PerDayHeldBonus, MarketLinkGoodId: "vintage_furniture",
            Notes: "Base $100 + $50/day of Vintage Furniture held, capped at 5 days"),
        new("paperwork_laundering", "Paperwork Laundering", JobTier.Operation, 240, 3, 200m, 150m, 150m,
            Effect: JobEffect.PaperworkDiscount,
            Notes: "Also grants 25% off future Shell Company Paperwork purchases"),
    ];

    /// <summary>Vintage Flip Circuit pays this per day of furniture held, up to <see cref="VintageFlipMaxDays"/>.</summary>
    public const decimal VintageFlipPerDay = 50m;
    public const int VintageFlipMaxDays = 5;

    /// <summary>Discount on Shell Company Paperwork granted by Paperwork Laundering.</summary>
    public const decimal PaperworkDiscountRate = 0.25m;

    public static JobDefinition? FindJob(string id) => Jobs.FirstOrDefault(j => j.Id == id);

    // --- Market goods (sheet: Market Goods) --------------------------------

    public static readonly IReadOnlyList<GoodDefinition> Goods =
    [
        new("streaming_codes", "Bootleg Streaming Codes", 10m, PriceBehavior.Steady,
            HourlyVolatility: 0.05, Hint: "Steady — teaches basic buy-low/sell-high"),
        new("grey_chips", "Grey-Market Chips", 50m, PriceBehavior.Volatile,
            HourlyVolatility: 0.20, Hint: "Rumor-driven, rewards frequent check-ins"),
        new("knockoff_sneakers", "Knockoff Sneakers", 80m, PriceBehavior.ScheduledSpike,
            HourlyVolatility: 0.03, EventMultiplier: 2.50, EventHours: 2, CrashMultiplier: 0.40,
            EventsPerDay: 3, EventName: "Drop",
            Hint: "+150% for 2hr on drop, then crashes -60%"),
        new("energy_drinks", "Contraband Energy Drinks", 15m, PriceBehavior.EventDriven,
            HourlyVolatility: 0.04, EventMultiplier: 2.00, EventHours: 4,
            EventsPerDay: 1, EventName: "Crackdown",
            Hint: "+100% for 4hr during a crackdown event"),
        new("exam_keys", "Leaked Exam Keys", 40m, PriceBehavior.Seasonal,
            HourlyVolatility: 0.03, EventMultiplier: 1.80, EventHours: 72,
            EventsPerDay: 2.0 / 30.0, EventName: "Exam Season",
            Hint: "+80% for 3 days, roughly twice a month"),
        new("influencer_followers", "Bulk Influencer Followers", 5m, PriceBehavior.Bubble,
            HourlyVolatility: 0.10, BubbleHighMultiplier: 1.50, BubbleLowMultiplier: 0.60, BubbleCycleMinutes: 45,
            Hint: "+50%/-40% cycling roughly every 45 min"),
        new("vintage_furniture", "\"Vintage\" Furniture", 60m, PriceBehavior.Appreciating,
            HourlyVolatility: 0.01, AppreciationPerDay: 0.03, AppreciationCap: 0.30,
            Hint: "+3%/day held, capped at +30% (day 10)"),
        new("shell_paperwork", "Shell Company Paperwork", 25m, PriceBehavior.Steady,
            HourlyVolatility: 0.02, Hint: "Utility good for company upkeep, not really traded"),
    ];

    public static GoodDefinition? FindGood(string id) => Goods.FirstOrDefault(g => g.Id == id);

    /// <summary>
    /// How hard aggregate player buying/selling pushes a price. Net volume over a
    /// tick is divided by this before being applied as a percentage move, so a
    /// price is driven by the playerbase rather than by pure randomness (brief §6).
    /// </summary>
    public const double MarketPressureDivisor = 500.0;

    /// <summary>Ceiling on how far a single tick can move a price from player pressure alone.</summary>
    public const double MarketMaxPressurePerTick = 0.10;

    /// <summary>Prices are pulled this share of the way back toward base each tick.</summary>
    public const double MarketMeanReversion = 0.05;

    /// <summary>Hard floor/ceiling on price as a multiple of base, so nothing runs away.</summary>
    public const double MarketFloorMultiplier = 0.25;
    public const double MarketCeilingMultiplier = 4.00;

    public static readonly TimeSpan MarketTickInterval = TimeSpan.FromMinutes(5);

    // --- Cosmetics (brief §13 — starter wardrobe only for MVP) -------------

    public static readonly IReadOnlyList<CosmeticDefinition> Cosmetics =
    [
        new("starter_suit", "Off-The-Rack Suit", "Outfit", FavorCost: 0, IsStarter: true),
        new("shades_noir", "Deniability Shades", "Face", FavorCost: 150),
        new("car_skin_matte", "Matte Black Company Car", "Vehicle", FavorCost: 400),
        new("hq_decor_award", "Fake Industry Award", "HQ", FavorCost: 250),
    ];

    public static CosmeticDefinition? FindCosmetic(string id) => Cosmetics.FirstOrDefault(c => c.Id == id);
}

// ---------------------------------------------------------------------------
// Definition records
// ---------------------------------------------------------------------------

public sealed record TravelDefinition(Transport Transport, double DurationMinutes, double BaseRisk);

/// <summary>Special behaviour a job has beyond "wait, then get paid".</summary>
public enum JobEffect
{
    None,
    /// <summary>Cannot start unless the player holds at least one tradeable good.</summary>
    RequiresHeldGood,
    /// <summary>Grants a fixed item stack on collect.</summary>
    GrantsItem,
    /// <summary>Removes one random inventory stack on collect (and pays for it).</summary>
    ClearsRandomItem,
    /// <summary>Payout scales with the live price of <see cref="JobDefinition.MarketLinkGoodId"/>.</summary>
    MarketLinkedPayout,
    /// <summary>Payout accrues continuously; partial collection is allowed.</summary>
    PaysAcrossDuration,
    /// <summary>Base payout plus a per-day bonus for how long the linked good has been held.</summary>
    PerDayHeldBonus,
    /// <summary>Grants a standing discount on Shell Company Paperwork.</summary>
    PaperworkDiscount,
}

public enum RiskEffect
{
    None,
    LosePayoutShare,
    LoseGoodsShare,
}

public sealed record JobDefinition(
    string Id,
    string Name,
    JobTier Tier,
    double DurationMinutes,
    int EnergyCost,
    decimal CashCost,
    decimal PayoutMin,
    decimal PayoutMax,
    double RiskChance = 0,
    RiskEffect RiskEffect = RiskEffect.None,
    double RiskMagnitude = 0,
    string? RiskDescription = null,
    JobEffect Effect = JobEffect.None,
    string? GrantsGoodId = null,
    int GrantsQuantity = 0,
    string? MarketLinkGoodId = null,
    string Notes = "")
{
    public TimeSpan Duration => TimeSpan.FromMinutes(DurationMinutes);
    public decimal PayoutMid => (PayoutMin + PayoutMax) / 2m;
}

public sealed record GoodDefinition(
    string Id,
    string Name,
    decimal BasePrice,
    PriceBehavior Behavior,
    double HourlyVolatility,
    double EventMultiplier = 1.0,
    double EventHours = 0,
    double CrashMultiplier = 1.0,
    double BubbleHighMultiplier = 1.0,
    double BubbleLowMultiplier = 1.0,
    double BubbleCycleMinutes = 0,
    double AppreciationPerDay = 0,
    double AppreciationCap = 0,
    double EventsPerDay = 0,
    string? EventName = null,
    string Hint = "")
{
    /// <summary>
    /// Vintage Furniture is priced per-stack by hold time rather than by the open
    /// market, so it can be sold but never bought — otherwise buy-and-hold would be
    /// a risk-free +30% bounded only by storage.
    /// </summary>
    public bool Tradeable => Behavior != PriceBehavior.Appreciating;

    /// <summary>Odds this good's event fires on any single market tick.</summary>
    public double EventChancePerTick =>
        EventsPerDay <= 0 ? 0 : EventsPerDay * GameData.MarketTickInterval.TotalDays;
}

public sealed record CosmeticDefinition(string Id, string Name, string Slot, int FavorCost, bool IsStarter = false);
