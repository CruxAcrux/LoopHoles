using System.Text.Json.Serialization;
using Loopholes.Api;
using Loopholes.Infrastructure;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Loopholes")
    ?? "Data Source=loopholes.db";

builder.Services.AddLoopholes(connectionString);
builder.Services.AddScoped<PlayerContext>();
builder.Services.AddHostedService<MarketTickerService>();

builder.Services.ConfigureHttpJsonOptions(options =>
{
    // Enums travel as names so the Godot client's own enums line up by name, not ordinal.
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
    options.SerializerOptions.PropertyNamingPolicy = null;
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<LoopholesDbContext>();
    await db.Database.MigrateAsync();
}

app.UseMiddleware<GameRuleExceptionMiddleware>();
app.UseMiddleware<BearerTokenMiddleware>();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapLoopholes();

app.Run();

/// <summary>Exposed so the integration tests can boot the real app via WebApplicationFactory.</summary>
public partial class Program;
