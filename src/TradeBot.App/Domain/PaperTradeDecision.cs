namespace TradeBot.App.Domain;

public sealed record PaperTradeDecision(
    string Symbol,
    string Provider,
    TradeSignalAction Action,
    decimal Quantity,
    decimal ReferencePrice,
    string Status,
    string Notes,
    DateTime RecordedAtUtc);
