using TradeBot.App.Domain;

namespace TradeBot.App.Application.Providers;

public sealed class MarketDataResponse
{
	public bool Success { get; }
	public string ProviderName { get; }
	public string Symbol { get; }
	public IReadOnlyList<MarketCandle> Candles { get; }
	public string Message { get; }

	private MarketDataResponse(bool success, string providerName, string symbol, IReadOnlyList<MarketCandle> candles, string message)
	{
		Success = success;
		ProviderName = providerName;
		Symbol = symbol;
		Candles = candles;
		Message = message;
	}

	public static MarketDataResponse Ok(string providerName, string symbol, IReadOnlyList<MarketCandle> candles, string message)
		=> new(true, providerName, symbol, candles, message);

	public static MarketDataResponse Disabled(string providerName, string symbol, string message)
		=> new(false, providerName, symbol, new List<MarketCandle>(), message);
}
