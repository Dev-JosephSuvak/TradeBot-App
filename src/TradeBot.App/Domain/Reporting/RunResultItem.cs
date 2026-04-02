using TradeBot.App.Domain;

namespace TradeBot.App.Domain.Reporting;

public sealed record RunResultItem(
    string Provider,
    string Symbol,
    string Status,
    int Candles,
    string Message,
    TradeSignal? Signal,
    PaperTradeDecision? PaperTrade,
    string? Error);
