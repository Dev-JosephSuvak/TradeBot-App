using TradeBot.App.Application.Contracts.Api;
using TradeBot.App.Domain;

namespace TradeBot.App.Application.Contracts.Simulation;

public interface IEngine
{
    SimulationResultDto Run(IReadOnlyList<MarketCandle> candles);
}