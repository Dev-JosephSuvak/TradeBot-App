namespace TradeBot.App.Domain;

public sealed record PortfolioPosition(
    string Ticker,
    decimal Quantity,
    decimal AverageEntryPrice,
    decimal LastPrice,
    DateTime OpenedAtUtc,
    DateTime UpdatedAtUtc)
{
    public decimal MarketValue => Quantity * LastPrice;
    public decimal UnrealizedPnl => Quantity * (LastPrice - AverageEntryPrice);
    public decimal StopLossPrice(decimal stopLossPercent) => AverageEntryPrice * (1 - stopLossPercent);
}
