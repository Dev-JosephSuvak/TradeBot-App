namespace TradeBot.App.Domain;

public enum TradeSignalAction
{
    Hold,
    Buy,
    Sell
}

public sealed record TradeSignal(
    string Symbol,
    string Provider,
    TradeSignalAction Action,
    decimal LatestClose,
    decimal ShortAverage,
    decimal LongAverage,
    string Reason,
    DateTime GeneratedAtUtc,
    string StrategyName = "sma_crossover",
    decimal? Rsi = null,
    decimal? Mac = null);
