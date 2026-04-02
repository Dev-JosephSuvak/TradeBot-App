namespace TradeBot.App.Application.Contracts.Persistence;

public sealed record PortfolioStoreResult(string Target, bool Success, string Message);
