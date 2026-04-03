namespace TradeBot.App.Application.Contracts.Portfolio;

public sealed record TickerAggregateSnapshot(
    string Ticker,
    decimal LastPrice,
    decimal AverageClose,
    decimal High,
    decimal Low,
    decimal TotalVolume,
    int CandleCount,
    DateTime AsOfUtc,
    DateTime ExpiresAtUtc,
    bool FromCache);
