using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Loopholes.Contracts;
using Loopholes.Infrastructure;
using Loopholes.Infrastructure.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Loopholes.Tests;

/// <summary>
/// Boots the real API against an in-memory SQLite database, with the clock and the
/// dice under the test's control. Everything else — middleware, endpoints,
/// services, migrations — is exactly what ships.
/// </summary>
public sealed class LoopholesApp : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");

    public FakeClock Clock { get; } = new();

    public FixedRandom Random { get; } = new();

    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = null,
        Converters = { new JsonStringEnumConverter() },
    };

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        _connection.Open();

        builder.UseEnvironment("Testing");
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<LoopholesDbContext>>();
            services.RemoveAll<LoopholesDbContext>();
            services.AddDbContext<LoopholesDbContext>(o => o.UseSqlite(_connection));

            services.RemoveAll<IClock>();
            services.AddSingleton<IClock>(Clock);

            services.RemoveAll<IRandomSource>();
            services.AddSingleton<IRandomSource>(Random);

            // The background ticker would race the test's hand-driven clock.
            services.RemoveAll<IHostedService>();
        });
    }

    /// <summary>Creates a player and returns a client already carrying their bearer token.</summary>
    public async Task<HttpClient> NewPlayerAsync(string name = "Test Holdings")
    {
        var client = CreateClient();
        var deviceId = Guid.NewGuid().ToString();

        var response = await client.PostAsJsonAsync("/api/v1/auth/register", new RegisterRequest(deviceId, name), Json);
        response.EnsureSuccessStatusCode();

        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>(Json);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.Token);
        return client;
    }

    /// <summary>Applies a change directly to the stored player — the setup shortcut for states the API can't reach quickly.</summary>
    public async Task EditPlayerAsync(HttpClient client, Action<Domain.Player> edit)
    {
        var state = await client.GetFromJsonAsync<PlayerStateDto>("/api/v1/me", Json);
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LoopholesDbContext>();

        var player = await db.Players.FirstAsync(p => p.Id == Guid.Parse(state!.PlayerId));
        edit(player);
        await db.SaveChangesAsync();
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing) _connection.Dispose();
    }
}

public static class HttpExtensions
{
    public static async Task<T> ReadAsync<T>(this HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode} {response.StatusCode}: {body}");
        return JsonSerializer.Deserialize<T>(body, LoopholesApp.Json)!;
    }

    public static async Task<ErrorDto> ReadErrorAsync(this HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        Assert.False(response.IsSuccessStatusCode, $"Expected a rejection but got {(int)response.StatusCode}: {body}");
        return JsonSerializer.Deserialize<ErrorDto>(body, LoopholesApp.Json)!;
    }

    public static Task<HttpResponseMessage> PostJsonAsync<T>(this HttpClient client, string url, T value) =>
        client.PostAsJsonAsync(url, value, LoopholesApp.Json);
}
