using TradeBot.App.Application.Providers;

namespace TradeBot.App.Application.Contracts;

public interface IMarketDataProvider
{
	string Name { get; }
	bool IsEnabled { get; }
	Task<MarketDataResponse> GetHistoricalDataAsync(string symbol, int lookbackDays, CancellationToken cancellationToken);
}
