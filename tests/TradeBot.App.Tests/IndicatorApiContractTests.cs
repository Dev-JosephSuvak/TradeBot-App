using System.Text.Json;
using TradeBot.App.Application.Contracts.Api;

namespace TradeBot.App.Tests;

public class IndicatorApiContractTests
{
    [Fact]
    public void IndicatorResponseDto_Deserializes_SelectedPeriodAndCandles_ForChartApi()
    {
        const string json = """
        {
          "ticker": "AAPL",
          "rsi": 51.42,
          "avgLowRSI": 33.10,
          "avgHighRSI": 71.45,
          "period": 1,
          "periodType": "weeks",
          "selectedPeriodKey": "1w",
          "crossover": 2.3456,
          "trendDirection": "Bullish",
          "periodMetrics": [
            {
              "periodKey": "1w",
              "rsi": 51.42,
              "avgLowRSI": 33.10,
              "avgHighRSI": 71.45,
              "averageGain": 1.12,
              "averageLoss": 0.42,
              "relativeStrength": 2.66,
              "shortMovingAverage": 201.12,
              "longMovingAverage": 198.77,
              "crossover": 2.3456,
              "trendDirection": "Bullish",
              "volumeTraded": 1200000,
              "dataPoints": 7
            }
          ],
          "candles": [
            {
              "timestamp": "2026-03-30T13:30:00Z",
              "open": 198.1,
              "high": 202.5,
              "low": 197.4,
              "close": 201.8,
              "volume": 1203000
            },
            {
              "timestamp": "2026-03-31T13:30:00Z",
              "open": 201.8,
              "high": 204.2,
              "low": 200.6,
              "close": 203.4,
              "volume": 1100400
            }
          ]
        }
        """;

        var response = JsonSerializer.Deserialize<IndicatorResponseDto>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        Assert.NotNull(response);
        Assert.Equal("AAPL", response!.Ticker);
        Assert.Equal("1w", response.SelectedPeriodKey);
        Assert.Equal(2, response.Candles.Count);
        Assert.Equal("2026-03-30T13:30:00Z", response.Candles[0].Timestamp);
        Assert.Equal(202.5, response.Candles[0].High);
        Assert.Single(response.PeriodMetrics);
        Assert.Equal("1w", response.PeriodMetrics[0].PeriodKey);
    }

    [Fact]
    public void IndicatorResponseDto_Serializes_CandlesForChartConsumers()
    {
        var response = new IndicatorResponseDto(
            Ticker: "MSFT",
            Rsi: 48.21,
            AvgLowRsi: 28.2,
            AvgHighRsi: 69.8,
            Period: 1,
            PeriodType: "months",
            SelectedPeriodKey: "1mo",
            Crossover: -1.102,
            TrendDirection: "Bearish",
            PeriodMetrics:
            [
                new IndicatorPeriodMetricsDto(
                    PeriodKey: "1mo",
                    Rsi: 48.21,
                    AvgLowRsi: 28.2,
                    AvgHighRsi: 69.8,
                    AverageGain: 0.88,
                    AverageLoss: 0.91,
                    RelativeStrength: 0.96,
                    ShortMovingAverage: 301.11,
                    LongMovingAverage: 302.21,
                    Crossover: -1.102,
                    TrendDirection: "Bearish",
                    VolumeTraded: 2300000,
                    DataPoints: 31)
            ],
            Candles:
            [
                new CandleDto("2026-03-01T14:30:00Z", 300.2, 304.0, 298.8, 301.1, 1500000),
                new CandleDto("2026-03-02T14:30:00Z", 301.1, 303.2, 299.9, 300.4, 1450000)
            ]);

        var json = JsonSerializer.Serialize(response);

        Assert.Contains("\"selectedPeriodKey\":\"1mo\"", json);
        Assert.Contains("\"candles\":[", json);
        Assert.Contains("\"timestamp\":\"2026-03-01T14:30:00Z\"", json);
    }
}