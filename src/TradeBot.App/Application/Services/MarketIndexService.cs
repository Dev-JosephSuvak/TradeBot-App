using TradeBot.App.Application.Contracts;
using TradeBot.App.Application.Contracts.Api;
using TradeBot.App.Infrastructure.Settings;

namespace TradeBot.App.Application.Services;

/// <summary>
/// Application service for the market-index API.
/// Resolves the best available provider for a given ticker, fetches candles,
/// and maps them to the public <see cref="CandleDto"/> shape.
/// </summary>
public sealed class MarketIndexService
{
    private readonly IReadOnlyList<IMarketDataProvider> _providers;
    private readonly TradeBotSettings _settings;

    public MarketIndexService(
        IReadOnlyList<IMarketDataProvider> providers,
        TradeBotSettings settings)
    {
        _providers = providers;
        _settings  = settings;
    }

    /// <summary>
    /// Returns candles for <paramref name="ticker"/> from the first enabled provider
    /// that succeeds.  Always falls back to the simulated provider so the endpoint
    /// never returns an empty response.
    /// </summary>
    /// <param name="ticker">Symbol, e.g. "AAPL", "BTC-USD".</param>
    /// <param name="lookbackDays">How many candles / days to return (default: settings value).</param>
    public async Task<IReadOnlyList<CandleDto>> GetCandlesAsync(
        string ticker,
        int? lookbackDays,
        CancellationToken ct)
    {
        int days = lookbackDays ?? _settings.LookbackDays;
        string symbol = ticker.ToUpperInvariant();

        foreach (var provider in _providers)
        {
            if (!provider.IsEnabled) continue;

            try
            {
                var response = await provider.GetHistoricalDataAsync(symbol, days, ct);
                if (response.Success && response.Candles.Count > 0)
                    return MapToDto(response.Candles);
            }
            catch
            {
                // Try next provider
            }
        }

        // Should never reach here if SimulatedMarketDataProvider is last in the list.
        return Array.Empty<CandleDto>();
    }

    private static IReadOnlyList<CandleDto> MapToDto(
        IReadOnlyList<TradeBot.App.Domain.MarketCandle> candles) =>
        candles.Select(c => new CandleDto(
            Timestamp : c.TimestampUtc.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            Open      : (double)c.Open,
            High      : (double)c.High,
            Low       : (double)c.Low,
            Close     : (double)c.Close,
            Volume    : (double)c.Volume
        )).ToList();
}
