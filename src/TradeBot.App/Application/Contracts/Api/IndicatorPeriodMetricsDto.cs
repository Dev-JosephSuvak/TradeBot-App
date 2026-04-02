using System.Text.Json.Serialization;

namespace TradeBot.App.Application.Contracts.Api;

public sealed record IndicatorPeriodMetricsDto(
    [property: JsonPropertyName("periodKey")] string PeriodKey,
    [property: JsonPropertyName("rsi")] double Rsi,
    [property: JsonPropertyName("avgLowRSI")] double AvgLowRsi,
    [property: JsonPropertyName("avgHighRSI")] double AvgHighRsi,
    [property: JsonPropertyName("averageGain")] double AverageGain,
    [property: JsonPropertyName("averageLoss")] double AverageLoss,
    [property: JsonPropertyName("relativeStrength")] double RelativeStrength,
    [property: JsonPropertyName("shortMovingAverage")] double ShortMovingAverage,
    [property: JsonPropertyName("longMovingAverage")] double LongMovingAverage,
    [property: JsonPropertyName("crossover")] double Crossover,
    [property: JsonPropertyName("trendDirection")] string TrendDirection,
    [property: JsonPropertyName("volumeTraded")] double VolumeTraded,
    [property: JsonPropertyName("dataPoints")] int DataPoints);
