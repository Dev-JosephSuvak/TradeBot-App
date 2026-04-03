using TradeBot.App.Domain;

namespace TradeBot.App.Application.Contracts.Simulation;

public interface IStrategy
{
    TradeSignal GenerateSignal(IReadOnlyList<MarketCandle> history, PortfolioState state);
}
