using TradeBot.App.Domain;

namespace TradeBot.App.Application.Contracts.Portfolio;

public sealed record PortfolioExecutionResult(
    bool Success,
    string Message,
    PortfolioTrade? Trade,
    PortfolioState State);
