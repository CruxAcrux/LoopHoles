namespace Loopholes.Contracts;

public enum JobTier
{
    Hustle = 1,
    Operation = 2,
}

public enum TrustTier
{
    Untrustworthy,
    Unproven,
    Trusted,
    Elite,
}

public enum FreezeTier
{
    None = 0,
    Minor = 1,
    Major = 2,
}

public enum LicenseStatus
{
    NotOwned,
    Studying,
    Owned,
    Suspended,
}

public enum Transport
{
    Car,
    Jet,
    Freight,
}

/// <summary>How a good's price is driven. Each good has one learnable "personality".</summary>
public enum PriceBehavior
{
    /// <summary>Small drift, low volatility. Bootleg Streaming Codes, Shell Company Paperwork.</summary>
    Steady,

    /// <summary>Large drift, rumor-driven. Grey-Market Chips.</summary>
    Volatile,

    /// <summary>Scheduled drop: big spike, then a crash. Knockoff Sneakers.</summary>
    ScheduledSpike,

    /// <summary>Fires only on a random crackdown event. Contraband Energy Drinks.</summary>
    EventDriven,

    /// <summary>Multi-day seasonal window, roughly twice a month. Leaked Exam Keys.</summary>
    Seasonal,

    /// <summary>Fast sine-like bubble cycle. Bulk Influencer Followers.</summary>
    Bubble,

    /// <summary>Does not trade on the open market; appreciates per day held. "Vintage" Furniture.</summary>
    Appreciating,
}

public enum TrustEventKind
{
    FairSplit,
    Betrayal,
    Restitution,
    CleanStreak,
    IdleDecay,
    AuditFreeze,
}

public enum LeaderboardMode
{
    /// <summary>Net worth with a trust-tier modifier. The primary board.</summary>
    Empire,

    /// <summary>Pure cash.</summary>
    Richest,

    /// <summary>Pure trust score.</summary>
    MostTrusted,
}
