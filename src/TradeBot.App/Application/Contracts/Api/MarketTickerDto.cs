using System.Text.Json.Serialization;

namespace TradeBot.App.Application.Contracts.Api;

public sealed record MarketTickerDto(
    [property: JsonPropertyName("ticker")] string Ticker,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("asset_class")] string AssetClass,
    [property: JsonPropertyName("exchange")] string Exchange,
    [property: JsonPropertyName("provider")] string Provider);