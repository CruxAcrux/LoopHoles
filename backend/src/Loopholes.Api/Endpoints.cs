using Loopholes.Contracts;
using Loopholes.Domain;
using Loopholes.Infrastructure;
using Loopholes.Infrastructure.Services;

namespace Loopholes.Api;

public static class Endpoints
{
    public static void MapLoopholes(this WebApplication app)
    {
        var api = app.MapGroup("/api/v1");

        MapAuth(api);
        MapPlayer(api);
        MapJobs(api);
        MapMarket(api);
        MapTravel(api);
        MapLicense(api);
        MapFreeze(api);
        MapLeaderboard(api);
        MapProfile(api);
    }

    /// <summary>
    /// The standard shape of a mutating request: resolve the player (which also
    /// advances their timers), run the action, refresh their season standing, then
    /// commit once. A single SaveChanges per request keeps the whole action atomic.
    /// </summary>
    private static async Task<IResult> ActAsync<T>(
        PlayerContext ctx,
        PlayerService players,
        LeaderboardService boards,
        LoopholesDbContext db,
        Func<Player, Task<T>> action,
        CancellationToken ct)
    {
        var player = await players.LoadAsync(ctx.Require(), ct);
        var result = await action(player);

        await boards.RecordAsync(player, ct);
        await db.SaveChangesAsync(ct);

        return Results.Ok(result);
    }

    // --- Auth --------------------------------------------------------------

    private static void MapAuth(RouteGroupBuilder api) =>
        api.MapPost("/auth/register", async (RegisterRequest request, AuthService auth, CancellationToken ct) =>
                Results.Ok(await auth.RegisterOrLoginAsync(request.DeviceId, request.DisplayName, ct)))
            .WithSummary("Register a device, or re-issue a token for one already known.");

    // --- Player ------------------------------------------------------------

    private static void MapPlayer(RouteGroupBuilder api) =>
        api.MapGet("/me", async (PlayerContext ctx, PlayerService players, LeaderboardService boards, LoopholesDbContext db, CancellationToken ct) =>
            {
                var player = await players.LoadAsync(ctx.Require(), ct);
                var dto = await players.ToDtoAsync(player, ct);

                // Refreshing standings here keeps the board honest for players whose
                // score moved without a request of their own — idle trust decay, or a
                // held stack appreciating.
                await boards.RecordAsync(player, ct);

                // Resolve() may have regenerated energy or expired a freeze; persist that.
                await db.SaveChangesAsync(ct);
                return Results.Ok(dto);
            })
            .WithSummary("Current player state, with all timers advanced to now.");

    // --- Jobs --------------------------------------------------------------

    private static void MapJobs(RouteGroupBuilder api)
    {
        api.MapGet("/jobs", async (PlayerContext ctx, PlayerService players, JobService jobs, LoopholesDbContext db, CancellationToken ct) =>
            {
                var player = await players.LoadAsync(ctx.Require(), ct);
                await db.SaveChangesAsync(ct);
                return Results.Ok(new JobBoardDto(jobs.Catalog(player), jobs.Active(player)));
            })
            .WithSummary("Job catalogue with per-player unlock state, plus running jobs.");

        api.MapPost("/jobs/start", (
                StartJobRequest request,
                PlayerContext ctx, PlayerService players, JobService jobs, LeaderboardService boards, LoopholesDbContext db,
                CancellationToken ct) =>
                ActAsync(ctx, players, boards, db, p => Task.FromResult(jobs.Start(p, request.JobId)), ct))
            .WithSummary("Start a job. Payout and risk are rolled and sealed here, not at collect time.");

        api.MapPost("/jobs/collect", (
                CollectJobRequest request,
                PlayerContext ctx, PlayerService players, JobService jobs, LeaderboardService boards, LoopholesDbContext db,
                CancellationToken ct) =>
                ActAsync(ctx, players, boards, db, p =>
                {
                    if (!Guid.TryParse(request.ActiveJobId, out var id))
                        throw new GameRuleException("job_not_running", "That job isn't running.");
                    return jobs.CollectAsync(p, id, ct);
                }, ct))
            .WithSummary("Collect a finished job and reveal the sealed outcome.");
    }

    // --- Market ------------------------------------------------------------

    private static void MapMarket(RouteGroupBuilder api)
    {
        api.MapGet("/market", async (MarketService market, CancellationToken ct) =>
                Results.Ok(await market.QuoteAsync(ct)))
            .WithSummary("Live prices for all goods. Public — the board is the same for everyone.");

        api.MapPost("/market/buy", (
                TradeRequest request,
                PlayerContext ctx, PlayerService players, MarketService market, LeaderboardService boards, LoopholesDbContext db,
                CancellationToken ct) =>
                ActAsync(ctx, players, boards, db, p => market.BuyAsync(p, request.GoodId, request.Quantity, ct), ct))
            .WithSummary("Buy at the server's current price, plus the trading fee.");

        api.MapPost("/market/sell", (
                TradeRequest request,
                PlayerContext ctx, PlayerService players, MarketService market, LeaderboardService boards, LoopholesDbContext db,
                CancellationToken ct) =>
                ActAsync(ctx, players, boards, db, p => market.SellAsync(p, request.GoodId, request.Quantity, ct), ct))
            .WithSummary("Sell from inventory, net of the trading fee.");

        api.MapGet("/inventory", async (PlayerContext ctx, PlayerService players, MarketService market, LoopholesDbContext db, CancellationToken ct) =>
            {
                var player = await players.LoadAsync(ctx.Require(), ct);
                var dto = await market.InventoryAsync(player, ct);
                await db.SaveChangesAsync(ct);
                return Results.Ok(dto);
            })
            .WithSummary("Stacked inventory with hold times and per-stack valuation.");
    }

    // --- Travel ------------------------------------------------------------

    private static void MapTravel(RouteGroupBuilder api)
    {
        api.MapGet("/travel/options", async (PlayerContext ctx, PlayerService players, TravelService travel, LoopholesDbContext db, CancellationToken ct) =>
            {
                var player = await players.LoadAsync(ctx.Require(), ct);
                await db.SaveChangesAsync(ct);
                return Results.Ok(travel.Options(player));
            })
            .WithSummary("Transport options with trust-adjusted risk for this player.");

        api.MapPost("/travel/start", (
                StartTravelRequest request,
                PlayerContext ctx, PlayerService players, TravelService travel, LeaderboardService boards, LoopholesDbContext db,
                CancellationToken ct) =>
                ActAsync(ctx, players, boards, db, p => travel.StartAsync(p, request.Transport, request.Destination, ct), ct))
            .WithSummary("Depart. The risk roll happens once, here.");
    }

    // --- Driver's licence --------------------------------------------------

    private static void MapLicense(RouteGroupBuilder api)
    {
        api.MapPost("/license/study", (
                PlayerContext ctx, PlayerService players, LicenseService license, LeaderboardService boards, LoopholesDbContext db,
                CancellationToken ct) =>
                ActAsync(ctx, players, boards, db, p => license.StartStudyAsync(p, ct), ct))
            .WithSummary("Pay the licence fee and begin the study timer.");

        api.MapPost("/license/exam", (
                TakeExamRequest request,
                PlayerContext ctx, PlayerService players, LicenseService license, LeaderboardService boards, LoopholesDbContext db,
                CancellationToken ct) =>
                ActAsync(ctx, players, boards, db, p => license.TakeExamAsync(p, request.Cheat, ct), ct))
            .WithSummary("Sit the exam, honestly or otherwise.");
    }

    // --- Audit freeze ------------------------------------------------------

    private static void MapFreeze(RouteGroupBuilder api)
    {
        api.MapPost("/freeze/paperwork", (
                PlayerContext ctx, PlayerService players, FreezeService freezes, LeaderboardService boards, LoopholesDbContext db,
                CancellationToken ct) =>
                ActAsync(ctx, players, boards, db, async p =>
                {
                    var outcome = freezes.FilePaperwork(p);
                    return ToResult(outcome, PlayerService.FreezeDto(p), await players.ToDtoAsync(p, ct));
                }, ct))
            .WithSummary("File paperwork to chip time off a freeze. Free, on a cooldown.");

        api.MapPost("/freeze/lawyer", (
                ClearFreezeRequest request,
                PlayerContext ctx, PlayerService players, FreezeService freezes, LeaderboardService boards, LoopholesDbContext db,
                CancellationToken ct) =>
                ActAsync(ctx, players, boards, db, async p =>
                {
                    var outcome = freezes.HireLawyer(p, request.PartialLawyer);
                    return ToResult(outcome, PlayerService.FreezeDto(p), await players.ToDtoAsync(p, ct));
                }, ct))
            .WithSummary("Buy your way out of a freeze, in full or by half.");
    }

    private static FreezeActionResultDto ToResult(FreezeActionOutcome outcome, FreezeDto freeze, PlayerStateDto state) =>
        new(outcome.Action, outcome.Cost, outcome.Removed, freeze, state);

    // --- Leaderboard -------------------------------------------------------

    private static void MapLeaderboard(RouteGroupBuilder api) =>
        api.MapGet("/leaderboard", async (
                LeaderboardMode? mode, int? take,
                PlayerContext ctx, LeaderboardService boards, LoopholesDbContext db,
                CancellationToken ct) =>
            {
                var result = await boards.GetAsync(
                    mode ?? LeaderboardMode.Empire,
                    ctx.PlayerId,
                    take ?? LeaderboardService.DefaultPageSize,
                    ct);

                // CurrentSeasonAsync may have rolled the season over.
                await db.SaveChangesAsync(ct);
                return Results.Ok(result);
            })
            .WithSummary("Seasonal board in Empire, Richest or MostTrusted mode.");

    // --- Stats and wardrobe ------------------------------------------------

    private static void MapProfile(RouteGroupBuilder api)
    {
        api.MapGet("/stats", async (PlayerContext ctx, PlayerService players, StatsService stats, LoopholesDbContext db, CancellationToken ct) =>
            {
                var player = await players.LoadAsync(ctx.Require(), ct);
                var dto = await stats.GetAsync(player, ct);
                await db.SaveChangesAsync(ct);
                return Results.Ok(dto);
            })
            .WithSummary("Lifetime stats and the dated trust history log.");

        api.MapGet("/wardrobe", async (PlayerContext ctx, PlayerService players, WardrobeService wardrobe, LoopholesDbContext db, CancellationToken ct) =>
            {
                var player = await players.LoadAsync(ctx.Require(), ct);
                var dto = await wardrobe.GetAsync(player, ct);
                await db.SaveChangesAsync(ct);
                return Results.Ok(dto);
            })
            .WithSummary("Starter wardrobe with owned/equipped state.");

        api.MapPost("/wardrobe/buy", (
                CosmeticRequest request,
                PlayerContext ctx, PlayerService players, WardrobeService wardrobe, LeaderboardService boards, LoopholesDbContext db,
                CancellationToken ct) =>
                ActAsync(ctx, players, boards, db, p => wardrobe.BuyAsync(p, request.CosmeticId, ct), ct))
            .WithSummary("Buy a cosmetic with Favor. Cosmetics never affect stats.");

        api.MapPost("/wardrobe/equip", (
                CosmeticRequest request,
                PlayerContext ctx, PlayerService players, WardrobeService wardrobe, LeaderboardService boards, LoopholesDbContext db,
                CancellationToken ct) =>
                ActAsync(ctx, players, boards, db, p => wardrobe.EquipAsync(p, request.CosmeticId, ct), ct))
            .WithSummary("Equip an owned cosmetic, replacing whatever is in that slot.");
    }
}
