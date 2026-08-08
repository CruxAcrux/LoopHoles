using System.Text.Json;
using System.Text.Json.Serialization;

namespace Loopholes.Client.Net;

/// <summary>
/// Source-generated serialization for every type that crosses the wire.
///
/// This is not an optimisation: an iOS export is AOT-compiled, and the
/// reflection-based serializer would throw at runtime on device while working
/// perfectly in the editor. Generating the metadata at build time means a missing
/// registration is a compile error instead of a crash after shipping.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.Unspecified,
    DefaultIgnoreCondition = JsonIgnoreCondition.Never)]
[JsonSerializable(typeof(RegisterRequest))]
[JsonSerializable(typeof(AuthResponse))]
[JsonSerializable(typeof(PlayerStateDto))]
[JsonSerializable(typeof(JobBoardDto))]
[JsonSerializable(typeof(StartJobRequest))]
[JsonSerializable(typeof(CollectJobRequest))]
[JsonSerializable(typeof(ActiveJobDto))]
[JsonSerializable(typeof(JobResultDto))]
[JsonSerializable(typeof(MarketQuoteDto))]
[JsonSerializable(typeof(TradeRequest))]
[JsonSerializable(typeof(TradeResultDto))]
[JsonSerializable(typeof(InventoryDto))]
[JsonSerializable(typeof(List<TravelOptionDto>))]
[JsonSerializable(typeof(StartTravelRequest))]
[JsonSerializable(typeof(TravelResultDto))]
[JsonSerializable(typeof(TakeExamRequest))]
[JsonSerializable(typeof(ExamResultDto))]
[JsonSerializable(typeof(ClearFreezeRequest))]
[JsonSerializable(typeof(FreezeActionResultDto))]
[JsonSerializable(typeof(LeaderboardDto))]
[JsonSerializable(typeof(StatsDto))]
[JsonSerializable(typeof(WardrobeDto))]
[JsonSerializable(typeof(CosmeticRequest))]
[JsonSerializable(typeof(ErrorDto))]
[JsonSerializable(typeof(EmptyRequest))]
internal sealed partial class ApiJsonContext : JsonSerializerContext;

/// <summary>Body for endpoints that take an action but no arguments.</summary>
public sealed record EmptyRequest;
