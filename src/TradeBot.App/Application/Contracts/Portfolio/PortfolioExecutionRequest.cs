namespace TradeBot.App.Application.Contracts.Portfolio;

public sealed record PortfolioExecutionRequest(
    string Ticker,
    string Action,
    decimal Quantity,
    decimal Price,
    string? Reason = null,
    string? Strategy = null);
