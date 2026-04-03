using System.Text.Json.Serialization;

namespace TradeBot.App.Application.Contracts.Api;

public sealed record TradeDto(
    [property: JsonPropertyName("timestamp")] string Timestamp,
    [property: JsonPropertyName("action")] string Action,
    [property: JsonPropertyName("price")] double Price,
    [property: JsonPropertyName("quantity")] double Quantity,
    [property: JsonPropertyName("strategy_name")] string StrategyName,
    [property: JsonPropertyName("reason")] string Reason);
