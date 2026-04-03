using TradeBot.App.Application.Services;
using TradeBot.App.Domain;

namespace TradeBot.App.Tests;

public class UnitTest1
{
    [Fact]
    public void Evaluate_ReturnsBuy_WhenShortAverageExceedsLongAverage()
    {
        var strategy = new SmaCrossoverStrategy();
        var candles = Enumerable.Range(1, 25)
            .Select(index => new MarketCandle(
                DateTime.UtcNow.Date.AddDays(-25 + index),
                index,
                index + 1,
                index - 1,
                index,
                1000))
            .ToList();

        var signal = strategy.Evaluate("AAPL", "YahooFinancePython", candles, 5, 20);

        Assert.Equal(TradeSignalAction.Buy, signal.Action);
        Assert.Equal("AAPL", signal.Symbol);
    }

    [Fact]
    public void CreateDecision_NeverProducesLiveOrder()
    {
        var service = new PaperTradingService();
        var signal = new TradeSignal(
            "BTC-USD",
            "Binance",
            TradeSignalAction.Sell,
            65000m,
            64000m,
            64500m,
            "Test signal",
            DateTime.UtcNow);

        var decision = service.CreateDecision(signal);

        Assert.Equal("Simulated", decision.Status);
        Assert.Contains("No live brokerage or exchange execution", decision.Notes);
        Assert.Equal(1m, decision.Quantity);
    }

    [Fact]
    public void CreateDecision_PropagatesSignalReasonAndStrategy()
    {
        var service = new PaperTradingService();
        var signal = new TradeSignal(
            "AAPL",
            "YahooFinancePython",
            TradeSignalAction.Buy,
            180m,
            179m,
            175m,
            "sma_cross_up",
            DateTime.UtcNow,
            "sma_crossover",
            62.5m,
            1.25m);

        var decision = service.CreateDecision(signal);

        Assert.Equal("sma_cross_up", decision.Reason);
        Assert.Equal("sma_crossover", decision.StrategyName);
    }

    [Fact]
    public void ToTradeDto_UsesStopLossReason_ForRiskTriggeredExit()
    {
        var trade = new PortfolioTrade(
            "trade-1",
            "AAPL",
            PortfolioTradeAction.StopLossSell,
            2m,
            150m,
            300m,
            -10m,
            "Stop-loss triggered at 150.00",
            DateTime.UtcNow,
            "StopLoss");

        var dto = ContractMapper.ToTradeDto(trade);

        Assert.Equal("SELL", dto.Action);
        Assert.Equal("stop_loss", dto.Reason);
        Assert.Equal("StopLoss", dto.StrategyName);
    }
}