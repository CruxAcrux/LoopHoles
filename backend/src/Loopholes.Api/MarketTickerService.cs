using Loopholes.Domain;
using Loopholes.Infrastructure.Services;

namespace Loopholes.Api;

/// <summary>
/// Drives the market clock. Prices only ever move here, on the server, on a fixed
/// cadence — which is the fix for the price-refresh exploit called out in the
/// brief: there is no client-triggered path that can cause a re-roll.
/// </summary>
public sealed class MarketTickerService(IServiceScopeFactory scopes, ILogger<MarketTickerService> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Market ticker started; interval {Interval}.", GameData.MarketTickInterval);

        using var timer = new PeriodicTimer(GameData.MarketTickInterval);

        // Seed prices immediately so the first request never sees an empty board.
        await TickAsync(stoppingToken);

        while (await timer.WaitForNextTickAsync(stoppingToken))
            await TickAsync(stoppingToken);
    }

    private async Task TickAsync(CancellationToken ct)
    {
        try
        {
            using var scope = scopes.CreateScope();
            var market = scope.ServiceProvider.GetRequiredService<MarketService>();
            await market.TickAsync(ct);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            // A failed tick must not kill the ticker — the next one recovers.
            logger.LogError(ex, "Market tick failed.");
        }
    }
}
