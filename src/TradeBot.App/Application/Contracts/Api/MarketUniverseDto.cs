using System.Text.Json.Serialization;

namespace TradeBot.App.Application.Contracts.Api;

public sealed record MarketUniverseDto(
    [property: JsonPropertyName("as_of_utc")] string AsOfUtc,
    [property: JsonPropertyName("stocks")] IReadOnlyList<MarketTickerDto> Stocks,
    [property: JsonPropertyName("etfs")] IReadOnlyList<MarketTickerDto> Etfs,
    [property: JsonPropertyName("cryptos")] IReadOnlyList<MarketTickerDto> Cryptos,
    [property: JsonPropertyName("total_count")] int TotalCount);