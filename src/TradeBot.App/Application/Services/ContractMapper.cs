using TradeBot.App.Application.Contracts.Api;
using TradeBot.App.Domain;

namespace TradeBot.App.Application.Services;

public static class ContractMapper
{
    public static SignalDto ToSignalDto(TradeSignal signal) => new(
        Action: signal.Action.ToString().ToUpperInvariant(),
        Reason: signal.Reason,
        StrategyName: signal.StrategyName,
        Rsi: signal.Rsi is null ? null : (double)signal.Rsi.Value,
        Mac: signal.Mac is null ? null : (double)signal.Mac.Value);

    public static TradeDto ToTradeDto(PortfolioTrade trade) => new(
        Timestamp: trade.ExecutedAtUtc.ToString("O"),
        Action: NormalizeTradeAction(trade.Action),
        Price: (double)trade.Price,
        Quantity: (double)trade.Quantity,
        StrategyName: trade.StrategyName,
        Reason: trade.Action == PortfolioTradeAction.StopLossSell ? "stop_loss" : trade.Reason);

    public static PortfolioStateDto ToPortfolioStateDto(PortfolioState state)
    {
        var quantity = state.Positions.Sum(position => position.Quantity);
        var entryPrice = quantity == 0m
            ? 0m
            : state.Positions.Sum(position => position.Quantity * position.AverageEntryPrice) / quantity;

        return new PortfolioStateDto(
            Cash: (double)state.CashBalanceUsd,
            PositionQuantity: (double)quantity,
            EntryPrice: (double)entryPrice,
            Equity: (double)state.Equity);
    }

    public static SimulationResultDto ToSimulationResultDto(PortfolioState state) => new(
        Trades: state.TradeHistory.Select(ToTradeDto).ToList(),
        FinalPortfolio: ToPortfolioStateDto(state),
        RealizedPnl: (double)state.RealizedPnl,
        UnrealizedPnl: (double)state.UnrealizedPnl);

    private static string NormalizeTradeAction(PortfolioTradeAction action) => action switch
    {
        PortfolioTradeAction.Buy => "BUY",
        PortfolioTradeAction.Sell => "SELL",
        PortfolioTradeAction.StopLossSell => "SELL",
        _ => action.ToString().ToUpperInvariant()
    };
}
