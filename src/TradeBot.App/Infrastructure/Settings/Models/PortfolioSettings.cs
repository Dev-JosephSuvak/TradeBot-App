namespace TradeBot.App.Infrastructure.Settings;

public sealed record PortfolioSettings
{
    public decimal StartingBalanceUsd { get; init; } = 10000m;
    public decimal MaxPositionSizePercent { get; init; } = 0.20m;
    public decimal StopLossPercent { get; init; } = 0.05m;
    public int CacheTtlMinutes { get; init; } = 5;
    public string DefaultTradeStrategy { get; init; } = "ManualUi";
}
