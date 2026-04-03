using System.Text.Json.Serialization;

namespace TradeBot.App.Application.Contracts.Api;

public sealed record PortfolioStateDto(
    [property: JsonPropertyName("cash")] double Cash,
    [property: JsonPropertyName("position_quantity")] double PositionQuantity,
    [property: JsonPropertyName("entry_price")] double EntryPrice,
    [property: JsonPropertyName("equity")] double Equity);
