using System.Text.Json.Serialization;

namespace TradeBot.App.Application.Contracts.Api;

public sealed record SignalDto(
    [property: JsonPropertyName("action")] string Action,
    [property: JsonPropertyName("reason")] string Reason,
    [property: JsonPropertyName("strategy_name")] string StrategyName,
    [property: JsonPropertyName("rsi")] double? Rsi = null,
    [property: JsonPropertyName("mac")] double? Mac = null);
