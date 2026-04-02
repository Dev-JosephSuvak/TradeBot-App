using TradeBot.App.Application.Contracts;
using TradeBot.App.Application.Providers;
using TradeBot.App.Infrastructure.Core;
using TradeBot.App.Infrastructure.Settings;

namespace TradeBot.App.Application.Services;

public sealed class YahooFinancePythonBridgeProvider : IMarketDataProvider
{
	private readonly TradeBotSettings _settings;
	private readonly PythonBridgeRunner _pythonBridgeRunner;
	private readonly string _baseDirectory;

	public YahooFinancePythonBridgeProvider(TradeBotSettings settings, PythonBridgeRunner pythonBridgeRunner, string baseDirectory)
	{
		_settings = settings;
		_pythonBridgeRunner = pythonBridgeRunner;
		_baseDirectory = baseDirectory;
	}

	public string Name => "YahooFinancePython";

	public bool IsEnabled => true;

	public async Task<MarketDataResponse> GetHistoricalDataAsync(string symbol, int lookbackDays, CancellationToken cancellationToken)
	{
		try
		{
			var scriptPath = Path.Combine(_baseDirectory, _settings.YahooBridgeScriptRelativePath);
			var candles = await _pythonBridgeRunner.FetchCandlesAsync(
				_settings.PythonExecutable,
				scriptPath,
				NormalizeSymbol(symbol),
				lookbackDays,
				cancellationToken);

			return MarketDataResponse.Ok(Name, symbol, candles, $"Fetched {candles.Count} candles through Python bridge.");
		}
		catch (Exception exception)
		{
			return MarketDataResponse.Disabled(Name, symbol, exception.Message);
		}
	}

	private static string NormalizeSymbol(string symbol) => symbol.Contains('/') ? symbol.Replace('/', '-') : symbol;
}
