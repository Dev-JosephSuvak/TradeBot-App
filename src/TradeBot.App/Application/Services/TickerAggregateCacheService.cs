using System.Collections.Concurrent;
using TradeBot.App.Application.Contracts.Portfolio;
using TradeBot.App.Infrastructure.Settings;

namespace TradeBot.App.Application.Services;

public sealed class TickerAggregateCacheService
{
    private readonly ConcurrentDictionary<string, TickerAggregateSnapshot> _cache = new();
    private readonly MarketIndexService _marketIndexService;
    private readonly TradeBotSettings _settings;

    public TickerAggregateCacheService(MarketIndexService marketIndexService, TradeBotSettings settings)
    {
        _marketIndexService = marketIndexService;
        _settings = settings;
    }

    public async Task<TickerAggregateSnapshot> GetOrRefreshAsync(string ticker, CancellationToken cancellationToken)
    {
        var key = ticker.ToUpperInvariant();
        if (_cache.TryGetValue(key, out var cached) && cached.ExpiresAtUtc > DateTime.UtcNow)
        {
            return cached with { FromCache = true };
        }

        var candles = await _marketIndexService.GetCandlesAsync(key, 5, cancellationToken);
        if (candles.Count == 0)
        {
            var emptySnapshot = new TickerAggregateSnapshot(
                Ticker: key,
                LastPrice: 0m,
                AverageClose: 0m,
                High: 0m,
                Low: 0m,
                TotalVolume: 0m,
                CandleCount: 0,
                AsOfUtc: DateTime.UtcNow,
                ExpiresAtUtc: DateTime.UtcNow.AddMinutes(_settings.Portfolio.CacheTtlMinutes),
                FromCache: false);

            _cache[key] = emptySnapshot;
            return emptySnapshot;
        }

        var snapshot = new TickerAggregateSnapshot(
            Ticker: key,
            LastPrice: (decimal)candles[^1].Close,
            AverageClose: (decimal)candles.Average(c => c.Close),
            High: (decimal)candles.Max(c => c.High),
            Low: (decimal)candles.Min(c => c.Low),
            TotalVolume: (decimal)candles.Sum(c => c.Volume),
            CandleCount: candles.Count,
            AsOfUtc: DateTime.UtcNow,
            ExpiresAtUtc: DateTime.UtcNow.AddMinutes(_settings.Portfolio.CacheTtlMinutes),
            FromCache: false);

        _cache[key] = snapshot;
        return snapshot;
    }
}
