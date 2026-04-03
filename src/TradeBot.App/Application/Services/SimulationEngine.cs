using TradeBot.App.Application.Contracts.Api;
using TradeBot.App.Application.Contracts.Simulation;
using TradeBot.App.Domain;
using TradeBot.App.Infrastructure.Settings;

namespace TradeBot.App.Application.Services;

public sealed class SimulationEngine : IEngine
{
    private readonly SmaCrossoverStrategy _strategy;
    private readonly TradeBotSettings _settings;

    public SimulationEngine(SmaCrossoverStrategy strategy, TradeBotSettings settings)
    {
        _strategy = strategy;
        _settings = settings;
    }

    public SimulationResultDto Run(IReadOnlyList<MarketCandle> candles)
    {
        var ordered = candles.OrderBy(candle => candle.TimestampUtc).ToList();
        var trades = new List<PortfolioTrade>();
        var cash = _settings.Portfolio.StartingBalanceUsd;
        decimal quantity = 0m;
        decimal entryPrice = 0m;

        for (var index = _settings.LongWindow; index <= ordered.Count; index++)
        {
            var history = ordered.Take(index).ToList();
            var currentPrice = history[^1].Close;
            var state = BuildState(cash, quantity, entryPrice, currentPrice, trades);
            var signal = _strategy.GenerateSignal(history, state);

            if (signal.Action == TradeSignalAction.Buy && quantity == 0m)
            {
                quantity = 1m;
                entryPrice = currentPrice;
                cash -= currentPrice;
                trades.Add(new PortfolioTrade($"sim-{Guid.NewGuid():N}", signal.Symbol, PortfolioTradeAction.Buy, 1m, currentPrice, currentPrice, 0m, signal.Reason, history[^1].TimestampUtc, signal.StrategyName));
            }
            else if (signal.Action == TradeSignalAction.Sell && quantity > 0m)
            {
                cash += quantity * currentPrice;
                var realized = quantity * (currentPrice - entryPrice);
                trades.Add(new PortfolioTrade($"sim-{Guid.NewGuid():N}", signal.Symbol, PortfolioTradeAction.Sell, quantity, currentPrice, quantity * currentPrice, realized, signal.Reason, history[^1].TimestampUtc, signal.StrategyName));
                quantity = 0m;
                entryPrice = 0m;
            }
        }

        var finalPrice = ordered.Count == 0 ? 0m : ordered[^1].Close;
        var finalState = BuildState(cash, quantity, entryPrice, finalPrice, trades);
        return ContractMapper.ToSimulationResultDto(finalState);
    }

    private PortfolioState BuildState(decimal cash, decimal quantity, decimal entryPrice, decimal currentPrice, IReadOnlyList<PortfolioTrade> trades)
    {
        var positions = quantity > 0m
            ? new[] { new PortfolioPosition("SIM", quantity, entryPrice, currentPrice, DateTime.UtcNow, DateTime.UtcNow) }
            : Array.Empty<PortfolioPosition>();

        var realized = trades.Sum(trade => trade.RealizedPnl);
        var unrealized = positions.Sum(position => position.UnrealizedPnl);
        var equity = cash + positions.Sum(position => position.MarketValue);

        return new PortfolioState(
            StartingBalanceUsd: _settings.Portfolio.StartingBalanceUsd,
            CashBalanceUsd: cash,
            RealizedPnl: realized,
            UnrealizedPnl: unrealized,
            Equity: equity,
            Positions: positions,
            TradeHistory: trades,
            UpdatedAtUtc: DateTime.UtcNow);
    }
}