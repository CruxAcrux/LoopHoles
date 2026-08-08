using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Threading.Tasks;
using Godot;

// Godot.HttpClient and System.Net.Http.HttpClient are both in scope here. The
// alias keeps it unambiguous which one this file means: the engine's, whose
// Method enum names the verb for HttpRequest.Request.
using GodotHttp = Godot.HttpClient;

namespace Loopholes.Client.Net;

/// <summary>
/// The only thing in the game that knows the server exists.
///
/// Built on Godot's own <see cref="HttpRequest"/> node rather than
/// <c>System.Net.Http.HttpClient</c> on purpose: <c>await ToSignal(...)</c> resumes
/// on the main thread, so a caller can touch the scene tree straight after
/// awaiting without marshalling. It is also the transport Godot actually supports
/// across every export target.
///
/// Every method here is a thin, typed wrapper. No game rule is evaluated on this
/// side — the client asks, the server decides, and a refusal comes back as an
/// <see cref="ApiException"/> carrying the server's reason code.
/// </summary>
public sealed partial class LoopholesApi : Node
{
    /// <summary>Seconds before a request is abandoned. Mobile networks stall; the UI must not.</summary>
    public const double RequestTimeoutSeconds = 15;

    public string BaseUrl { get; set; } = "http://127.0.0.1:5147/api/v1";

    /// <summary>Bearer token from <see cref="RegisterAsync"/>. Null until the device registers.</summary>
    public string? Token { get; set; }

    // --- Auth ------------------------------------------------------------

    public async Task<AuthResponse> RegisterAsync(string deviceId, string displayName)
    {
        var response = await PostAsync(
            "/auth/register",
            new RegisterRequest(deviceId, displayName),
            ApiJsonContext.Default.RegisterRequest,
            ApiJsonContext.Default.AuthResponse);

        Token = response.Token;
        return response;
    }

    // --- Player ----------------------------------------------------------

    public Task<PlayerStateDto> GetStateAsync() =>
        GetAsync("/me", ApiJsonContext.Default.PlayerStateDto);

    // --- Jobs ------------------------------------------------------------

    public Task<JobBoardDto> GetJobsAsync() =>
        GetAsync("/jobs", ApiJsonContext.Default.JobBoardDto);

    public Task<ActiveJobDto> StartJobAsync(string jobId) =>
        PostAsync("/jobs/start", new StartJobRequest(jobId),
            ApiJsonContext.Default.StartJobRequest, ApiJsonContext.Default.ActiveJobDto);

    public Task<JobResultDto> CollectJobAsync(string activeJobId) =>
        PostAsync("/jobs/collect", new CollectJobRequest(activeJobId),
            ApiJsonContext.Default.CollectJobRequest, ApiJsonContext.Default.JobResultDto);

    // --- Market ----------------------------------------------------------

    public Task<MarketQuoteDto> GetMarketAsync() =>
        GetAsync("/market", ApiJsonContext.Default.MarketQuoteDto);

    public Task<TradeResultDto> BuyAsync(string goodId, int quantity) =>
        PostAsync("/market/buy", new TradeRequest(goodId, quantity),
            ApiJsonContext.Default.TradeRequest, ApiJsonContext.Default.TradeResultDto);

    public Task<TradeResultDto> SellAsync(string goodId, int quantity) =>
        PostAsync("/market/sell", new TradeRequest(goodId, quantity),
            ApiJsonContext.Default.TradeRequest, ApiJsonContext.Default.TradeResultDto);

    public Task<InventoryDto> GetInventoryAsync() =>
        GetAsync("/inventory", ApiJsonContext.Default.InventoryDto);

    // --- Travel ----------------------------------------------------------

    public Task<List<TravelOptionDto>> GetTravelOptionsAsync() =>
        GetAsync("/travel/options", ApiJsonContext.Default.ListTravelOptionDto);

    public Task<TravelResultDto> StartTravelAsync(Transport transport, string destination) =>
        PostAsync("/travel/start", new StartTravelRequest(transport, destination),
            ApiJsonContext.Default.StartTravelRequest, ApiJsonContext.Default.TravelResultDto);

    // --- Driver's licence ------------------------------------------------

    public Task<PlayerStateDto> StartStudyAsync() =>
        PostAsync("/license/study", new EmptyRequest(),
            ApiJsonContext.Default.EmptyRequest, ApiJsonContext.Default.PlayerStateDto);

    public Task<ExamResultDto> TakeExamAsync(bool cheat) =>
        PostAsync("/license/exam", new TakeExamRequest(cheat),
            ApiJsonContext.Default.TakeExamRequest, ApiJsonContext.Default.ExamResultDto);

    // --- Audit freeze ----------------------------------------------------

    public Task<FreezeActionResultDto> FilePaperworkAsync() =>
        PostAsync("/freeze/paperwork", new EmptyRequest(),
            ApiJsonContext.Default.EmptyRequest, ApiJsonContext.Default.FreezeActionResultDto);

    public Task<FreezeActionResultDto> HireLawyerAsync(bool partial) =>
        PostAsync("/freeze/lawyer", new ClearFreezeRequest(partial),
            ApiJsonContext.Default.ClearFreezeRequest, ApiJsonContext.Default.FreezeActionResultDto);

    // --- Leaderboard, stats, wardrobe ------------------------------------

    public Task<LeaderboardDto> GetLeaderboardAsync(LeaderboardMode mode) =>
        GetAsync($"/leaderboard?mode={mode}", ApiJsonContext.Default.LeaderboardDto);

    public Task<StatsDto> GetStatsAsync() =>
        GetAsync("/stats", ApiJsonContext.Default.StatsDto);

    public Task<WardrobeDto> GetWardrobeAsync() =>
        GetAsync("/wardrobe", ApiJsonContext.Default.WardrobeDto);

    public Task<WardrobeDto> BuyCosmeticAsync(string cosmeticId) =>
        PostAsync("/wardrobe/buy", new CosmeticRequest(cosmeticId),
            ApiJsonContext.Default.CosmeticRequest, ApiJsonContext.Default.WardrobeDto);

    public Task<WardrobeDto> EquipCosmeticAsync(string cosmeticId) =>
        PostAsync("/wardrobe/equip", new CosmeticRequest(cosmeticId),
            ApiJsonContext.Default.CosmeticRequest, ApiJsonContext.Default.WardrobeDto);

    // --- Plumbing --------------------------------------------------------

    private async Task<TResponse> GetAsync<TResponse>(string path, JsonTypeInfo<TResponse> responseInfo)
    {
        var (status, body) = await SendAsync(GodotHttp.Method.Get, path, null);
        return Deserialize(status, body, responseInfo);
    }

    private async Task<TResponse> PostAsync<TRequest, TResponse>(
        string path,
        TRequest request,
        JsonTypeInfo<TRequest> requestInfo,
        JsonTypeInfo<TResponse> responseInfo)
    {
        var payload = JsonSerializer.Serialize(request, requestInfo);
        var (status, body) = await SendAsync(GodotHttp.Method.Post, path, payload);
        return Deserialize(status, body, responseInfo);
    }

    private static TResponse Deserialize<TResponse>(int status, string body, JsonTypeInfo<TResponse> info)
    {
        if (status >= 400)
            throw ToApiException(status, body);

        TResponse? value;
        try
        {
            value = JsonSerializer.Deserialize(body, info);
        }
        catch (JsonException ex)
        {
            // A shape the client doesn't understand means the wire contract drifted.
            throw new ApiException("malformed_response", $"Could not read the server's reply: {ex.Message}", status);
        }

        return value ?? throw new ApiException("malformed_response", "The server returned an empty body.", status);
    }

    private static ApiException ToApiException(int status, string body)
    {
        try
        {
            var error = JsonSerializer.Deserialize(body, ApiJsonContext.Default.ErrorDto);
            if (error is not null)
                return new ApiException(error.Code, error.Message, status);
        }
        catch (JsonException)
        {
            // Fall through — a proxy or crash page won't be shaped like ErrorDto.
        }

        return new ApiException("http_error", $"The server returned {status}.", status);
    }

    /// <summary>
    /// Issues one request on its own short-lived <see cref="HttpRequest"/> child, so
    /// calls never queue behind each other, and resumes on the main thread.
    /// </summary>
    private async Task<(int Status, string Body)> SendAsync(GodotHttp.Method method, string path, string? body)
    {
        var request = new HttpRequest { Timeout = RequestTimeoutSeconds, UseThreads = true };
        AddChild(request);

        try
        {
            var headers = new List<string> { "Content-Type: application/json", "Accept: application/json" };
            if (!string.IsNullOrEmpty(Token))
                headers.Add($"Authorization: Bearer {Token}");

            var error = request.Request(BaseUrl + path, headers.ToArray(), method, body ?? string.Empty);
            if (error != Error.Ok)
                throw new ApiException(ApiException.TransportCode, $"Could not reach {BaseUrl}: {error}.");

            var signal = await ToSignal(request, HttpRequest.SignalName.RequestCompleted);

            var result = (HttpRequest.Result)(long)signal[0];
            var status = (int)(long)signal[1];
            var payload = signal[3].AsByteArray();

            if (result != HttpRequest.Result.Success)
                throw new ApiException(ApiException.TransportCode, DescribeTransport(result));

            return (status, Encoding.UTF8.GetString(payload));
        }
        finally
        {
            request.QueueFree();
        }
    }

    private static string DescribeTransport(HttpRequest.Result result) => result switch
    {
        HttpRequest.Result.CantConnect => "Can't reach the server. Is it running?",
        HttpRequest.Result.CantResolve => "Can't resolve the server address.",
        HttpRequest.Result.ConnectionError => "The connection dropped.",
        HttpRequest.Result.Timeout => "The server took too long to answer.",
        HttpRequest.Result.TlsHandshakeError => "Couldn't establish a secure connection.",
        _ => $"Network error ({result}).",
    };
}
