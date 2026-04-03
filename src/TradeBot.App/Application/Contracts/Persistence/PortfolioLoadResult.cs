using TradeBot.App.Domain;

namespace TradeBot.App.Application.Contracts.Persistence;

public sealed record PortfolioLoadResult(
    bool Success,
    PortfolioState State,
    string Source,
    string Message);
