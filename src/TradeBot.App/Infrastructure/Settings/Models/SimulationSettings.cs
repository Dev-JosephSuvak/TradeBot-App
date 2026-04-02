namespace TradeBot.App.Infrastructure.Settings;

/// <summary>
/// Controls the realistic simulated price-data generator used when no live provider is available.
/// </summary>
public sealed record SimulationSettings
{
    /// <summary>Starting price for the random walk (USD).</summary>
    public double SeedPrice { get; init; } = 150.0;

    /// <summary>Daily volatility as a fraction of price (e.g. 0.02 = 2 %).</summary>
    public double DailyVolatility { get; init; } = 0.02;

    /// <summary>Slight upward drift applied each candle (e.g. 0.0003 = 0.03 %).</summary>
    public double DriftPerCandle { get; init; } = 0.0003;

    /// <summary>
    /// Candle interval in minutes. Default 1440 = daily.
    /// Use smaller values (e.g. 60) for intraday simulation.
    /// </summary>
    public int IntervalMinutes { get; init; } = 1440;
}
