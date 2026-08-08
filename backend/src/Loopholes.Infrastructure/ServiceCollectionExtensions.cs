using Loopholes.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Loopholes.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddLoopholes(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<LoopholesDbContext>(o => o.UseSqlite(connectionString));

        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IRandomSource, GameRandom>();

        services.AddScoped<MarketPriceStore>();
        services.AddScoped<PlayerService>();
        services.AddScoped<FreezeService>();
        services.AddScoped<JobService>();
        services.AddScoped<MarketService>();
        services.AddScoped<TravelService>();
        services.AddScoped<LicenseService>();
        services.AddScoped<LeaderboardService>();
        services.AddScoped<AuthService>();
        services.AddScoped<StatsService>();
        services.AddScoped<WardrobeService>();

        return services;
    }
}
