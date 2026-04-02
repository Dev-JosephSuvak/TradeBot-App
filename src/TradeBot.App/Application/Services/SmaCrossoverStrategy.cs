using TradeBot.App.Domain;

namespace TradeBot.App.Application.Services;

public sealed class SmaCrossoverStrategy
{
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

		var reason = $"SMA({shortWindow})={shortAverage:F2}, SMA({longWindow})={longAverage:F2}";

		return new TradeSignal(symbol, provider, action, latest.Close, shortAverage, longAverage, reason, DateTime.UtcNow);
	}
}
