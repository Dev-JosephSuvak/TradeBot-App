using System.Text.Json.Serialization;

namespace TradeBot.App.Application.Contracts.Api;

public sealed record IndicatorResponseDto(
    [property: JsonPropertyName("ticker")] string Ticker,
    [property: JsonPropertyName("rsi")] double Rsi,
    [property: JsonPropertyName("avgLowRSI")] double AvgLowRsi,
    [property: JsonPropertyName("avgHighRSI")] double AvgHighRsi,
    [property: JsonPropertyName("period")] int Period,
    [property: JsonPropertyName("periodType")] string PeriodType,
    [property: JsonPropertyName("selectedPeriodKey")] string SelectedPeriodKey,
    [property: JsonPropertyName("crossover")] double Crossover,
    [property: JsonPropertyName("trendDirection")] string TrendDirection,
    [property: JsonPropertyName("periodMetrics")] IReadOnlyList<IndicatorPeriodMetricsDto> PeriodMetrics,
    [property: JsonPropertyName("candles")] IReadOnlyList<CandleDto> Candles);
