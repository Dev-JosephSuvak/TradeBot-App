using TradeBot.App.Application.Contracts.Simulation;
using TradeBot.App.Domain;

namespace TradeBot.App.Application.Services;

public sealed class SmaCrossoverStrategy : IStrategy
{
	public TradeSignal GenerateSignal(IReadOnlyList<MarketCandle> history, PortfolioState state)
	{
		return Evaluate(
			state.Positions.FirstOrDefault()?.Ticker ?? "SIM",
			"SmaCrossover",
			history,
			5,
			20);
	}

	public TradeSignal Evaluate(string symbol, string provider, IReadOnlyList<MarketCandle> candles, int shortWindow, int longWindow)
	{
		if (candles.Count < longWindow)
		{
			throw new InvalidOperationException($"Need at least {longWindow} candles to compute the strategy.");
		}

		var ordered = candles.OrderBy(candle => candle.TimestampUtc).ToList();
		var latest = ordered[^1];
		var shortAverage = ordered.TakeLast(shortWindow).Average(candle => candle.Close);
		var longAverage = ordered.TakeLast(longWindow).Average(candle => candle.Close);

		var action = shortAverage > longAverage
			? TradeSignalAction.Buy
			: shortAverage < longAverage
				? TradeSignalAction.Sell
				: TradeSignalAction.Hold;

		var reason = action switch
		{
			TradeSignalAction.Buy => "sma_cross_up",
			TradeSignalAction.Sell => "sma_cross_down",
			_ => "hold"
		};

		return new TradeSignal(symbol, provider, action, latest.Close, shortAverage, longAverage, reason, DateTime.UtcNow, "sma_crossover", null, shortAverage - longAverage);
	}
}
