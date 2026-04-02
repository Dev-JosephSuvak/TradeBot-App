using TradeBot.App.Application.Contracts;
using TradeBot.App.Application.Contracts.Persistence;
using TradeBot.App.Domain;
using TradeBot.App.Domain.Reporting;
using TradeBot.App.Infrastructure.Settings;

namespace TradeBot.App.Application.Services;

public sealed class TradeBotRunner
{
	private readonly IReadOnlyList<IMarketDataProvider> _providers;
	private readonly SmaCrossoverStrategy _strategy;
	private readonly PaperTradingService _paperTradingService;
	private readonly TradeBotSettings _settings;
	private readonly IRunReportStore _runReportStore;

	public TradeBotRunner(
		IReadOnlyList<IMarketDataProvider> providers,
		SmaCrossoverStrategy strategy,
		PaperTradingService paperTradingService,
		TradeBotSettings settings,
		IRunReportStore runReportStore)
	{
		_providers = providers;
		_strategy = strategy;
		_paperTradingService = paperTradingService;
		_settings = settings;
		_runReportStore = runReportStore;
	}

	public async Task<int> RunAsync(CancellationToken cancellationToken)
	{
		var results = new List<RunResultItem>();

		foreach (var symbol in _settings.Symbols)
		{
			foreach (var provider in _providers)
			{
				var response = await provider.GetHistoricalDataAsync(symbol, _settings.LookbackDays, cancellationToken);

				if (!response.Success)
				{
					results.Add(new RunResultItem(
						provider.Name,
						symbol,
						"Unavailable",
						0,
						response.Message,
						null,
						null,
						null));
					continue;
				}

				try
				{
					var signal = _strategy.Evaluate(symbol, provider.Name, response.Candles, _settings.ShortWindow, _settings.LongWindow);
					var decision = _paperTradingService.CreateDecision(signal);

					results.Add(new RunResultItem(
						provider.Name,
						symbol,
						decision.Status,
						response.Candles.Count,
						response.Message,
						signal,
						decision,
						null));

					Console.WriteLine($"[{provider.Name}] {symbol} -> {signal.Action} @ {signal.LatestClose:F2} ({signal.Reason})");
				}
				catch (Exception exception)
				{
					results.Add(new RunResultItem(
						provider.Name,
						symbol,
						"StrategyError",
						response.Candles.Count,
						"Strategy evaluation failed.",
						null,
						null,
						exception.Message));
				}
			}
		}

		var report = new RunReport(DateTime.UtcNow, "paper-trading-only", results);
		var persistenceResults = await _runReportStore.PersistAsync(report, cancellationToken);

		foreach (var result in persistenceResults)
		{
			var status = result.Success ? "OK" : "FAILED";
			Console.WriteLine($"Persistence[{result.Target}] {status}: {result.Message}");
		}

		Console.WriteLine("Run complete.");
		return 0;
	}
}
