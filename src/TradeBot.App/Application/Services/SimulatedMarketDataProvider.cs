using TradeBot.App.Application.Contracts;
using TradeBot.App.Application.Providers;
using TradeBot.App.Domain;
using TradeBot.App.Infrastructure.Settings;

namespace TradeBot.App.Application.Services;

/// <summary>
/// Generates realistic OHLCV candles using a Geometric Brownian Motion random walk.
/// Produces deterministic output for a given symbol seed so results are reproducible.
/// Volatility and drift are configurable via <see cref="SimulationSettings"/>.
/// </summary>
public sealed class SimulatedMarketDataProvider : IMarketDataProvider
{
    private readonly SimulationSettings _sim;

    public SimulatedMarketDataProvider(SimulationSettings sim)
    {
        _sim = sim;
    }

    public string Name => "Simulated";
    public bool IsEnabled => true;

    public Task<MarketDataResponse> GetHistoricalDataAsync(
        string symbol,
        int lookbackDays,
        CancellationToken cancellationToken)
    {
        var candles = GenerateCandles(symbol, lookbackDays);
        return Task.FromResult(
            MarketDataResponse.Ok(Name, symbol, candles,
                $"Generated {candles.Count} simulated candles for {symbol}"));
    }

    /// <summary>
    /// Generates <paramref name="count"/> candles for <paramref name="symbol"/> using a
    /// Geometric Brownian Motion model. The symbol name is hashed to produce a
    /// per-symbol seed so different tickers have different price series.
    /// </summary>
    public IReadOnlyList<MarketCandle> GenerateCandles(string symbol, int count)
    {
        // Seed the RNG from the symbol so results are reproducible per-ticker.
        int symbolSeed = symbol.Aggregate(0, (acc, c) => acc * 31 + c);
        var rng = new Random(symbolSeed);

        var candles = new List<MarketCandle>(count);
        double price = _sim.SeedPrice;
        var now = DateTime.UtcNow.Date;
        var interval = TimeSpan.FromMinutes(_sim.IntervalMinutes);
        // Start at the earliest candle in the lookback window.
        var startTime = now - interval * count;

        for (int i = 0; i < count; i++)
        {
            // GBM: price * exp((drift - 0.5 * vol²) + vol * N(0,1))
            double z = SampleNormal(rng);
            double logReturn = (_sim.DriftPerCandle - 0.5 * _sim.DailyVolatility * _sim.DailyVolatility)
                               + _sim.DailyVolatility * z;
            price *= Math.Exp(logReturn);

            // Synthesise realistic OHLC from the close price.
            double spread = price * _sim.DailyVolatility * 0.5;
            double open  = price * (1 + (rng.NextDouble() - 0.5) * 0.01);
            double high  = Math.Max(open, price) + Math.Abs(rng.NextDouble() * spread);
            double low   = Math.Min(open, price) - Math.Abs(rng.NextDouble() * spread);
            double volume = 1_000_000 + rng.NextDouble() * 9_000_000;

            candles.Add(new MarketCandle(
                TimestampUtc : startTime + interval * i,
                Open         : (decimal)Math.Round(open,  4),
                High         : (decimal)Math.Round(high,  4),
                Low          : (decimal)Math.Round(low,   4),
                Close        : (decimal)Math.Round(price, 4),
                Volume       : (decimal)Math.Round(volume, 2)));
        }

        return candles;
    }

    // Box-Muller transform → standard normal sample.
    private static double SampleNormal(Random rng)
    {
        double u1 = 1.0 - rng.NextDouble();
        double u2 = 1.0 - rng.NextDouble();
        return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2);
    }
}
