namespace TradeBot.App.Application.Contracts.Persistence;

public sealed record RunReportStoreResult(
    string Target,
    bool Success,
    string Message);
