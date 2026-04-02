using TradeBot.App.Application.Contracts;
using TradeBot.App.Application.Services;
using TradeBot.App.Infrastructure.Core;
using TradeBot.App.Infrastructure.DataAccess.Persistence;
using TradeBot.App.Infrastructure.Settings;

var projectRoot = ResolveProjectRoot();
var settings = TradeBotSettingsLoader.Load(projectRoot);
var httpClient = new HttpClient
{
	Timeout = TimeSpan.FromSeconds(20)
};

var providers = new List<IMarketDataProvider>
{
	new YahooFinancePythonBridgeProvider(settings, new PythonBridgeRunner(), projectRoot),
	new TwelveDataProvider(httpClient, settings),
	new PolygonProvider(httpClient, settings),
	new BinanceProvider(httpClient, settings)
};

var runReportStore = new CompositeRunReportStore(
    new FileRunReportStore(projectRoot, settings),
    new FirestoreRunReportStore(settings));

var runner = new TradeBotRunner(
    providers,
    new SmaCrossoverStrategy(),
    new PaperTradingService(),
    settings,
    runReportStore);

return await runner.RunAsync(CancellationToken.None);

static string ResolveProjectRoot()
{
    var current = new DirectoryInfo(AppContext.BaseDirectory);
    DirectoryInfo? fallback = null;

    while (current is not null)
    {
        if (File.Exists(Path.Combine(current.FullName, "TradeBot.App.csproj")))
        {
            return current.FullName;
        }

        if (fallback is null && File.Exists(Path.Combine(current.FullName, "appsettings.json")))
        {
            fallback = current;
        }

        current = current.Parent;
    }

    return fallback?.FullName ?? Directory.GetCurrentDirectory();
}
