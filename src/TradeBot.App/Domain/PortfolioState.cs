namespace TradeBot.App.Domain;

public sealed record PortfolioState(
    decimal StartingBalanceUsd,
    decimal CashBalanceUsd,
    decimal RealizedPnl,
    decimal UnrealizedPnl,
    decimal Equity,
    IReadOnlyList<PortfolioPosition> Positions,
    IReadOnlyList<PortfolioTrade> TradeHistory,
    DateTime UpdatedAtUtc)
{
    public static PortfolioState New(decimal startingBalance) => new(
        StartingBalanceUsd: startingBalance,
        CashBalanceUsd: startingBalance,
        RealizedPnl: 0m,
        UnrealizedPnl: 0m,
        Equity: startingBalance,
        Positions: [],
        TradeHistory: [],
        UpdatedAtUtc: DateTime.UtcNow);
}
