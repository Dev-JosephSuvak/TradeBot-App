namespace TradeBot.App.Domain;

public enum PortfolioTradeAction
{
    Buy,
    Sell,
    StopLossSell
}

public sealed record PortfolioTrade(
    string TradeId,
    string Ticker,
    PortfolioTradeAction Action,
    decimal Quantity,
    decimal Price,
    decimal Notional,
    decimal RealizedPnl,
    string Reason,
    DateTime ExecutedAtUtc,
    string StrategyName = "ManualUi");
