using Loopholes.Contracts;
using Loopholes.Domain;
using Microsoft.EntityFrameworkCore;

namespace Loopholes.Infrastructure.Services;

/// <summary>
/// Seasonal leaderboards. A season rollover resets <em>only</em> the board — cash,
/// trust, inventory, cosmetics and licence are account progress and are never
/// wiped (brief §14).
/// </summary>
public sealed class LeaderboardService(
    LoopholesDbContext db,
    IClock clock,
    PlayerService players,
    MarketPriceStore prices)
{
    public const int DefaultPageSize = 100;

    public async Task<Season> CurrentSeasonAsync(CancellationToken ct = default)
    {
        var now = clock.UtcNow;
        var season = await db.Seasons.OrderByDescending(s => s.Number).FirstOrDefaultAsync(ct);

        if (season is null)
        {
            season = new Season { Number = 1, StartedAt = now, EndsAt = now + GameData.SeasonLength };
            db.Seasons.Add(season);
            await db.SaveChangesAsync(ct);
            return season;
        }

        // A server that was down over a rollover may need to skip several seasons.
        while (season.EndsAt <= now)
        {
            await CloseAsync(season, ct);

            season = new Season
            {
                Number = season.Number + 1,
                StartedAt = season.EndsAt,
                EndsAt = season.EndsAt + GameData.SeasonLength,
            };

            db.Seasons.Add(season);
            await db.SaveChangesAsync(ct);
        }

        return season;
    }

    /// <summary>Freezes a season's final standings into each player's lifetime best rank.</summary>
    private async Task CloseAsync(Season season, CancellationToken ct)
    {
        if (season.Closed) return;

        var standings = await db.SeasonScores
            .Where(s => s.SeasonNumber == season.Number)
            .OrderByDescending(s => s.EmpireScore)
            .ThenBy(s => s.ReachedAt)
            .Include(s => s.Player)
            .ToListAsync(ct);

        for (var i = 0; i < standings.Count; i++)
        {
            var player = standings[i].Player;
            if (player is null) continue;

            var rank = i + 1;
            if (player.BestSeasonRank is null || rank < player.BestSeasonRank)
                player.BestSeasonRank = rank;
        }

        season.Closed = true;
        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Writes the player's current standing into the live season. Empire Score is
    /// only ratcheted upward within a season, so a deliberate sell-off late in the
    /// month can't be used to dodge a rival's rank.
    /// </summary>
    public async Task RecordAsync(Player p, CancellationToken ct = default)
    {
        var season = await CurrentSeasonAsync(ct);
        var states = await prices.GetStatesAsync(ct);

        var empire = ScoreMath.EmpireScore(p.Cash, players.InventoryValue(p, states), p.TrustScore);
        var now = clock.UtcNow;

        var score = await db.SeasonScores
            .FirstOrDefaultAsync(s => s.SeasonNumber == season.Number && s.PlayerId == p.Id, ct);

        if (score is null)
        {
            score = new SeasonScore
            {
                SeasonNumber = season.Number,
                PlayerId = p.Id,
                EmpireScore = empire,
                Cash = p.Cash,
                TrustScore = p.TrustScore,
                ReachedAt = now,
                UpdatedAt = now,
            };
            db.SeasonScores.Add(score);
        }
        else
        {
            if (empire > score.EmpireScore)
            {
                score.EmpireScore = empire;
                score.ReachedAt = now;
            }

            score.Cash = Math.Max(score.Cash, p.Cash);
            score.TrustScore = p.TrustScore;
            score.UpdatedAt = now;
        }

        if (empire > p.BestEmpireScore)
            p.BestEmpireScore = empire;
    }

    public async Task<LeaderboardDto> GetAsync(
        LeaderboardMode mode,
        Guid? viewerId,
        int take = DefaultPageSize,
        CancellationToken ct = default)
    {
        var season = await CurrentSeasonAsync(ct);

        var query = db.SeasonScores
            .Where(s => s.SeasonNumber == season.Number)
            .Include(s => s.Player);

        // Ties always break to whoever reached the score first.
        IOrderedQueryable<SeasonScore> ordered = mode switch
        {
            LeaderboardMode.Richest => query.OrderByDescending(s => s.Cash).ThenBy(s => s.ReachedAt),
            LeaderboardMode.MostTrusted => query.OrderByDescending(s => s.TrustScore).ThenBy(s => s.ReachedAt),
            _ => query.OrderByDescending(s => s.EmpireScore).ThenBy(s => s.ReachedAt),
        };

        var all = await ordered.ToListAsync(ct);

        var entries = all
            .Select((s, i) => Project(s, i + 1, mode))
            .ToList();

        var you = viewerId is { } id ? entries.FirstOrDefault(e => e.PlayerId == id.ToString()) : null;

        return new LeaderboardDto(
            mode,
            season.Number,
            season.StartedAt,
            season.EndsAt,
            entries.Take(take).ToList(),
            you);
    }

    private static LeaderboardEntryDto Project(SeasonScore s, int rank, LeaderboardMode mode)
    {
        var score = mode switch
        {
            LeaderboardMode.Richest => s.Cash,
            LeaderboardMode.MostTrusted => s.TrustScore,
            _ => s.EmpireScore,
        };

        return new LeaderboardEntryDto(
            rank,
            s.PlayerId.ToString(),
            s.Player?.DisplayName ?? "—",
            score,
            TrustMath.TierFor(s.TrustScore),
            s.TrustScore);
    }
}
