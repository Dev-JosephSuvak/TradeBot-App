using System.Text.Json.Serialization;

namespace TradeBot.App.Application.Contracts.Api;

/// <summary>
/// Public candle shape returned by the market-index API.
/// All float fields are <see cref="double"/> for JSON interop; timestamps are ISO 8601.
/// </summary>
public sealed record CandleDto(
    [property: JsonPropertyName("timestamp")] string Timestamp,
    [property: JsonPropertyName("open")] double Open,
    [property: JsonPropertyName("high")] double High,
    [property: JsonPropertyName("low")] double Low,
    [property: JsonPropertyName("close")] double Close,
    [property: JsonPropertyName("volume")] double Volume);
