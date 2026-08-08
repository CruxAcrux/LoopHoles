using Loopholes.Contracts;
using Loopholes.Infrastructure.Services;

namespace Loopholes.Api;

/// <summary>Which player this request is acting as, resolved from the bearer token.</summary>
public sealed class PlayerContext
{
    public Guid? PlayerId { get; set; }

    public Guid Require() => PlayerId
        ?? throw new GameRuleException("unauthorized", "A bearer token is required.");
}

/// <summary>
/// Resolves <c>Authorization: Bearer &lt;token&gt;</c> to a player id. Anonymous
/// requests are allowed through so that public endpoints (the market board, the
/// leaderboard) still work; the endpoints that need a player call
/// <see cref="PlayerContext.Require"/>.
/// </summary>
public sealed class BearerTokenMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, AuthService auth, PlayerContext player)
    {
        var header = context.Request.Headers.Authorization.ToString();

        if (header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            var token = header["Bearer ".Length..].Trim();
            var resolved = await auth.ResolveAsync(token, context.RequestAborted);
            player.PlayerId = resolved?.Id;
        }

        await next(context);
    }
}

/// <summary>
/// Turns a broken game rule into a 4xx with a stable machine-readable code, so the
/// Godot client can branch on <c>insufficient_energy</c> rather than parse prose.
/// </summary>
public sealed class GameRuleExceptionMiddleware(RequestDelegate next, ILogger<GameRuleExceptionMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (GameRuleException ex)
        {
            var status = ex.Code switch
            {
                "unauthorized" => StatusCodes.Status401Unauthorized,
                "player_not_found" or "unknown_job" or "unknown_good" or "unknown_cosmetic" or "unknown_transport"
                    => StatusCodes.Status404NotFound,
                _ => StatusCodes.Status409Conflict,
            };

            logger.LogInformation("Rule rejected: {Code} — {Message}", ex.Code, ex.Message);

            context.Response.StatusCode = status;
            await context.Response.WriteAsJsonAsync(new ErrorDto(ex.Code, ex.Message));
        }
    }
}
