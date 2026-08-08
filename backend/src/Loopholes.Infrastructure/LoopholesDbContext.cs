using Loopholes.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Loopholes.Infrastructure;

public sealed class LoopholesDbContext(DbContextOptions<LoopholesDbContext> options) : DbContext(options)
{
    public DbSet<Player> Players => Set<Player>();
    public DbSet<ActiveJob> ActiveJobs => Set<ActiveJob>();
    public DbSet<InventoryStack> InventoryStacks => Set<InventoryStack>();
    public DbSet<TrustEvent> TrustEvents => Set<TrustEvent>();
    public DbSet<PlayerCosmetic> PlayerCosmetics => Set<PlayerCosmetic>();
    public DbSet<MarketGoodState> MarketGoods => Set<MarketGoodState>();
    public DbSet<Season> Seasons => Set<Season>();
    public DbSet<SeasonScore> SeasonScores => Set<SeasonScore>();

    protected override void ConfigureConventions(ModelConfigurationBuilder builder)
    {
        // SQLite has no decimal type and stores it as TEXT by default, which makes
        // ORDER BY on money lexicographic — wrong for the leaderboard. Storing
        // integer cents keeps ordering correct and the arithmetic exact.
        builder.Properties<decimal>().HaveConversion<MoneyToCentsConverter>();
        builder.Properties<decimal?>().HaveConversion<NullableMoneyToCentsConverter>();

        // SQLite can't ORDER BY or compare a DateTimeOffset at all. Everything here
        // is UTC by construction (all time comes from IClock), so storing UTC ticks
        // loses nothing and makes timers queryable.
        builder.Properties<DateTimeOffset>().HaveConversion<UtcTicksConverter>();
        builder.Properties<DateTimeOffset?>().HaveConversion<NullableUtcTicksConverter>();
    }

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Player>(e =>
        {
            e.HasKey(p => p.Id);
            e.HasIndex(p => p.DeviceId).IsUnique();
            e.HasIndex(p => p.TokenHash);
            e.Property(p => p.DisplayName).HasMaxLength(32).IsRequired();
            e.Property(p => p.DeviceId).HasMaxLength(128).IsRequired();
            e.Property(p => p.TokenHash).HasMaxLength(128).IsRequired();

            e.HasMany(p => p.ActiveJobs).WithOne(j => j.Player!).HasForeignKey(j => j.PlayerId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(p => p.Inventory).WithOne(i => i.Player!).HasForeignKey(i => i.PlayerId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(p => p.TrustHistory).WithOne(t => t.Player!).HasForeignKey(t => t.PlayerId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(p => p.Cosmetics).WithOne(c => c.Player!).HasForeignKey(c => c.PlayerId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<ActiveJob>(e =>
        {
            e.HasKey(j => j.Id);
            e.HasIndex(j => new { j.PlayerId, j.CompletesAt });
            e.Property(j => j.JobId).HasMaxLength(64).IsRequired();
        });

        b.Entity<InventoryStack>(e =>
        {
            e.HasKey(i => i.Id);
            // One stack per good per player — quantities are stacked, not item-by-item.
            e.HasIndex(i => new { i.PlayerId, i.GoodId }).IsUnique();
            e.Property(i => i.GoodId).HasMaxLength(64).IsRequired();
        });

        b.Entity<TrustEvent>(e =>
        {
            e.HasKey(t => t.Id);
            e.HasIndex(t => new { t.PlayerId, t.At });
            e.Property(t => t.Description).HasMaxLength(256);
        });

        b.Entity<PlayerCosmetic>(e =>
        {
            e.HasKey(c => c.Id);
            e.HasIndex(c => new { c.PlayerId, c.CosmeticId }).IsUnique();
            e.Property(c => c.CosmeticId).HasMaxLength(64).IsRequired();
        });

        b.Entity<MarketGoodState>(e =>
        {
            e.HasKey(m => m.GoodId);
            e.Property(m => m.GoodId).HasMaxLength(64);
            e.Property(m => m.ActiveEvent).HasMaxLength(64);
        });

        b.Entity<Season>(e =>
        {
            e.HasKey(s => s.Number);
            e.Property(s => s.Number).ValueGeneratedNever();
        });

        b.Entity<SeasonScore>(e =>
        {
            e.HasKey(s => s.Id);
            e.HasIndex(s => new { s.SeasonNumber, s.PlayerId }).IsUnique();
            e.HasIndex(s => new { s.SeasonNumber, s.EmpireScore });
            e.HasOne(s => s.Player!).WithMany().HasForeignKey(s => s.PlayerId).OnDelete(DeleteBehavior.Cascade);
        });
    }
}

/// <summary>Stores money as integer cents so SQLite can order and sum it correctly.</summary>
public sealed class MoneyToCentsConverter() : ValueConverter<decimal, long>(
    v => (long)Math.Round(v * 100m, MidpointRounding.AwayFromZero),
    v => v / 100m);

public sealed class NullableMoneyToCentsConverter() : ValueConverter<decimal?, long?>(
    v => v == null ? null : (long)Math.Round(v.Value * 100m, MidpointRounding.AwayFromZero),
    v => v == null ? null : v.Value / 100m);

/// <summary>Stores timestamps as UTC ticks so SQLite can order and compare them.</summary>
public sealed class UtcTicksConverter() : ValueConverter<DateTimeOffset, long>(
    v => v.UtcTicks,
    v => new DateTimeOffset(v, TimeSpan.Zero));

public sealed class NullableUtcTicksConverter() : ValueConverter<DateTimeOffset?, long?>(
    v => v == null ? null : v.Value.UtcTicks,
    v => v == null ? null : new DateTimeOffset(v.Value, TimeSpan.Zero));
