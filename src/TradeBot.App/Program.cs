using Microsoft.AspNetCore.Mvc;
using System.Globalization;
using TradeBot.App.Application.Contracts;
using TradeBot.App.Application.Contracts.Api;
using TradeBot.App.Application.Contracts.Persistence;
using TradeBot.App.Application.Contracts.Portfolio;
using TradeBot.App.Application.Contracts.Simulation;
using TradeBot.App.Application.Services;
using TradeBot.App.Infrastructure.Core;
using TradeBot.App.Infrastructure.DataAccess.Persistence;
using TradeBot.App.Infrastructure.Settings;

// ── Resolve project root ──────────────────────────────────────────────────────
var projectRoot = ResolveProjectRoot();
var (globalsFilePath, appArgs) = ResolveGlobalsFile(args, projectRoot);

if (!string.IsNullOrWhiteSpace(globalsFilePath)
    && RuntimeGlobalsLoader.ApplyFromFile(globalsFilePath, out var globalsMessage))
{
    Console.WriteLine($"[Config] {globalsMessage}");
}

var settings    = TradeBotSettingsLoader.Load(projectRoot);

// ── Build shared provider list ────────────────────────────────────────────────
var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };

var providers = new List<IMarketDataProvider>
{
    new YahooFinancePythonBridgeProvider(settings, new PythonBridgeRunner(), projectRoot),
    new TwelveDataProvider(httpClient, settings),
    new PolygonProvider(httpClient, settings),
    new BinanceProvider(httpClient, settings),
    // Always-available fallback — never disabled, uses configurable GBM model.
    new SimulatedMarketDataProvider(settings.Simulation),
};

var portfolioStore = new FirebaseFirstPortfolioStore(
    new FirestorePortfolioStore(settings),
    new FilePortfolioStore(projectRoot, settings));

// ── Build the web application ──────────────────────────────────────────────────
var builder = WebApplication.CreateBuilder(appArgs);

builder.Services.AddSingleton(settings);
builder.Services.AddSingleton(projectRoot);
builder.Services.AddSingleton<IReadOnlyList<IMarketDataProvider>>(providers);
builder.Services.AddSingleton<IPortfolioStore>(portfolioStore);
builder.Services.AddSingleton<IStrategy, SmaCrossoverStrategy>();
builder.Services.AddSingleton<IEngine, SimulationEngine>();
builder.Services.AddSingleton<MarketIndexService>();
builder.Services.AddSingleton<PriceBoundsService>();
builder.Services.AddSingleton<MarketUniverseService>();
builder.Services.AddSingleton<TickerAggregateCacheService>();
builder.Services.AddSingleton<PaperPortfolioService>();
builder.Services.AddSingleton<PythonIndicatorsBridgeRunner>();
builder.Services.AddSingleton<MarketIndicatorsService>();
builder.Services.AddSingleton<SmaCrossoverStrategy>();
builder.Services.AddSingleton<SimulationEngine>();
builder.Services.AddCors(options =>
{
    options.AddPolicy("TradeBotUi", policy =>
    {
        policy
            .WithOrigins("http://localhost:5173")
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "TradeBot Market Index API", Version = "v1" });
});

var app = builder.Build();

app.UseCors("TradeBotUi");

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "TradeBot Market Index API v1");
    c.RoutePrefix = string.Empty; // Swagger at root "/"
});

// ── Market Index Endpoint ──────────────────────────────────────────────────────
// GET /api/candles/{ticker}?lookbackDays=60
// Returns an array of Candle objects for the requested ticker symbol.
app.MapGet("/api/candles/{ticker}", async (
    string ticker,
    [FromQuery] int? lookbackDays,
    MarketIndexService marketIndex,
    CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(ticker))
        return Results.BadRequest(new { error = "ticker is required." });

    var candles = await marketIndex.GetCandlesAsync(ticker, lookbackDays, ct);
    return TypedResults.Ok(candles);
})
.WithName("GetCandles")
.WithSummary("Get OHLCV candles for a symbol")
.WithDescription(
    "Returns an array of candle (OHLCV) objects for the given ticker. " +
    "Tries live providers in order; falls back to simulated data if none are available.")
.Produces<IReadOnlyList<CandleDto>>(200)
.Produces(400);

// Research-only prediction: never submits brokerage orders.
app.MapGet("/api/price-bounds/{ticker}", async (
    string ticker,
    [FromQuery] int? lookback,
    [FromQuery] int? horizon,
    [FromQuery] double? coverage,
    PriceBoundsService priceBounds,
    CancellationToken ct) =>
{
    try
    {
        var result = await priceBounds.PredictAsync(
            ticker, lookback ?? 100, horizon ?? 5, coverage ?? 0.90, ct);
        return Results.Ok(result);
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
    catch (OperationCanceledException) when (ct.IsCancellationRequested)
    {
        return Results.StatusCode(499);
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "Price-bounds prediction failed for {Ticker}", ticker);
        return Results.Problem("Price-bounds prediction unavailable.", statusCode: 503);
    }
})
.WithName("GetPriceBounds")
.WithSummary("Research-only statistical price bounds; no trade execution");

app.MapGet("/api/signals/{ticker}", async (
    string ticker,
    MarketIndexService marketIndex,
    SmaCrossoverStrategy strategy,
    TradeBotSettings tradeBotSettings,
    CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(ticker))
        return Results.BadRequest(new { error = "ticker is required." });

    var lookback = Math.Max(tradeBotSettings.LongWindow + 5, tradeBotSettings.LookbackDays);
    var candles = await marketIndex.GetCandlesAsync(ticker, lookback, ct);

    if (candles.Count < tradeBotSettings.LongWindow)
        return Results.BadRequest(new { error = $"Need at least {tradeBotSettings.LongWindow} candles to compute a signal." });

    var history = candles.Select(candle => new TradeBot.App.Domain.MarketCandle(
        TimestampUtc: DateTime.Parse(candle.Timestamp, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal),
        Open: (decimal)candle.Open,
        High: (decimal)candle.High,
        Low: (decimal)candle.Low,
        Close: (decimal)candle.Close,
        Volume: (decimal)candle.Volume)).ToList();

    var signal = strategy.Evaluate(
        symbol: ticker.ToUpperInvariant(),
        provider: "MarketIndexService",
        candles: history,
        shortWindow: tradeBotSettings.ShortWindow,
        longWindow: tradeBotSettings.LongWindow);

    return TypedResults.Ok(ContractMapper.ToSignalDto(signal));
})
.WithName("GetSignal")
.WithSummary("Get latest trading signal for a ticker")
.WithDescription("Computes SMA crossover signal using the configured short/long windows and returns the normalized signal contract.")
.Produces<SignalDto>(200)
.Produces(400);

app.MapGet("/api/indicators/{ticker}", async (
    string ticker,
    [FromQuery] int period,
    [FromQuery] string periodType,
    [FromQuery] int? shortWindow,
    [FromQuery] int? longWindow,
    MarketIndicatorsService indicatorsService,
    CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(ticker))
        return Results.BadRequest(new { error = "ticker is required." });

    if (period <= 0)
        return Results.BadRequest(new { error = "period must be greater than 0." });

    if (string.IsNullOrWhiteSpace(periodType))
        return Results.BadRequest(new { error = "periodType is required (e.g. days, months, years, minutes, hours)." });

    try
    {
        var response = await indicatorsService.GetIndicatorsAsync(
            ticker,
            period,
            periodType,
            shortWindow,
            longWindow,
            ct);

        return TypedResults.Ok(response);
    }
    catch (Exception ex)
    {
        return Results.Problem(ex.Message);
    }
})
.WithName("GetIndicators")
.WithSummary("Get dynamic MA crossover + RSI indicators")
.WithDescription(
    "Returns RSI, avg low/high RSI, crossover, trend direction, and volume for requested period plus multi-period metrics (5m, 15m, 1h, 1d, 1mo, 1y, 5y, max).")
.Produces<IndicatorResponseDto>(200)
.Produces(400)
.Produces(500);

app.MapGet("/api/market-universe/tickers", async (
    [FromQuery] string? search,
    [FromQuery] int? limitPerCategory,
    MarketUniverseService marketUniverseService,
    CancellationToken ct) =>
{
    var response = await marketUniverseService.GetUniverseAsync(search, limitPerCategory, ct);
    return TypedResults.Ok(response);
})
.WithName("GetMarketUniverseTickers")
.WithSummary("Get available stock, ETF, and crypto tickers")
.WithDescription("Retrieves market tickers grouped by Stocks, ETFs, and Cryptos from public sources with local configured-symbol fallback.")
.Produces<MarketUniverseDto>(200);

app.MapGet("/api/portfolio", async (
    PaperPortfolioService portfolioService,
    CancellationToken ct) =>
{
    var state = await portfolioService.GetStateAsync(ct);
    return TypedResults.Ok(ContractMapper.ToPortfolioStateDto(state));
})
.WithName("GetPortfolio")
.WithSummary("Get full paper portfolio state")
.Produces<PortfolioStateDto>(200);

app.MapGet("/api/portfolio/positions", async (
    PaperPortfolioService portfolioService,
    CancellationToken ct) =>
{
    var state = await portfolioService.GetStateAsync(ct);
    return TypedResults.Ok(state.Positions);
})
.WithName("GetPortfolioPositions")
.WithSummary("Get open portfolio positions")
.Produces(200);

app.MapGet("/api/portfolio/trades", async (
    [FromQuery] int? limit,
    PaperPortfolioService portfolioService,
    CancellationToken ct) =>
{
    var state = await portfolioService.GetStateAsync(ct);
    var take = Math.Max(1, limit ?? 100);
    var trades = state.TradeHistory.OrderByDescending(t => t.ExecutedAtUtc).Take(take).Select(ContractMapper.ToTradeDto).ToList();
    return TypedResults.Ok((IReadOnlyList<TradeDto>)trades);
})
.WithName("GetPortfolioTrades")
.WithSummary("Get portfolio trade history")
.Produces<IReadOnlyList<TradeDto>>(200);

app.MapPost("/api/portfolio/execute", async (
    PortfolioExecutionRequest request,
    PaperPortfolioService portfolioService,
    CancellationToken ct) =>
{
    var result = await portfolioService.ExecuteAsync(request, ct);
    var payload = new PortfolioExecutionResponseDto(
        Success: result.Success,
        Message: result.Message,
        Trade: result.Trade is null ? null : ContractMapper.ToTradeDto(result.Trade),
        FinalPortfolio: ContractMapper.ToPortfolioStateDto(result.State),
        RealizedPnl: (double)result.State.RealizedPnl,
        UnrealizedPnl: (double)result.State.UnrealizedPnl);

    return result.Success ? Results.Ok(payload) : Results.BadRequest(payload);
})
.WithName("ExecutePortfolioTrade")
.WithSummary("Execute a simulated portfolio trade with risk controls")
.Produces<PortfolioExecutionResponseDto>(200)
.Produces<PortfolioExecutionResponseDto>(400);

app.MapGet("/api/portfolio/simulation-result", async (
    PaperPortfolioService portfolioService,
    CancellationToken ct) =>
{
    var state = await portfolioService.GetStateAsync(ct);
    return TypedResults.Ok(ContractMapper.ToSimulationResultDto(state));
})
.WithName("GetSimulationResult")
.WithSummary("Get simulation result shaped to spec")
.Produces<SimulationResultDto>(200);

app.MapGet("/api/ticker-cache/{ticker}", async (
    string ticker,
    TickerAggregateCacheService tickerCache,
    PaperPortfolioService portfolioService,
    CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(ticker))
        return Results.BadRequest(new { error = "ticker is required." });

    var snapshot = await tickerCache.GetOrRefreshAsync(ticker, ct);
    var state = await portfolioService.RefreshTickerAndApplyStopLossAsync(snapshot.Ticker, snapshot.LastPrice, ct);

    return Results.Ok(new
    {
        ticker = snapshot.Ticker,
        snapshot,
        portfolioUpdatedAtUtc = state.UpdatedAtUtc,
        openPositions = state.Positions.Count
    });
})
.WithName("GetTickerCache")
.WithSummary("Get 5-minute cached ticker aggregate and apply stop-loss checks")
.Produces(200)
.Produces(400);

// ── Console runner (optional — set RUN_CONSOLE=true to execute) ───────────────
var runConsole = string.Equals(
    Environment.GetEnvironmentVariable("RUN_CONSOLE"), "true",
    StringComparison.OrdinalIgnoreCase);

if (runConsole)
{
    var runReportStore = new CompositeRunReportStore(
        new FileRunReportStore(projectRoot, settings),
        new FirestoreRunReportStore(settings));

    var runner = new TradeBotRunner(
        providers,
        new SmaCrossoverStrategy(),
        new PaperTradingService(),
        settings,
        runReportStore);

    var exitCode = await runner.RunAsync(CancellationToken.None);
    return exitCode;
}

// ── Start the API ─────────────────────────────────────────────────────────────
await app.RunAsync();
return 0;

// ── Helpers ───────────────────────────────────────────────────────────────────
static string ResolveProjectRoot()
{
    var current  = new DirectoryInfo(AppContext.BaseDirectory);
    DirectoryInfo? fallback = null;

    while (current is not null)
    {
        if (File.Exists(Path.Combine(current.FullName, "TradeBot.App.csproj")))
            return current.FullName;

        if (fallback is null && File.Exists(Path.Combine(current.FullName, "appsettings.json")))
            fallback = current;

        current = current.Parent!;
    }

    return fallback?.FullName ?? Directory.GetCurrentDirectory();
}

static (string? globalsFilePath, string[] remainingArgs) ResolveGlobalsFile(string[] args, string projectRoot)
{
    var remainingArgs = new List<string>();
    string? globalsPath = null;

    for (var index = 0; index < args.Length; index++)
    {
        var arg = args[index];

        if (arg.StartsWith("--globals=", StringComparison.OrdinalIgnoreCase))
        {
            globalsPath = arg.Split('=', 2)[1];
            continue;
        }

        if (string.Equals(arg, "--globals", StringComparison.OrdinalIgnoreCase))
        {
            if (index + 1 < args.Length)
            {
                globalsPath = args[index + 1];
                index++;
            }

            continue;
        }

        remainingArgs.Add(arg);
    }

    if (!string.IsNullOrWhiteSpace(globalsPath))
    {
        return (Path.GetFullPath(globalsPath), remainingArgs.ToArray());
    }

    var current = new DirectoryInfo(projectRoot);
    while (current is not null)
    {
        var candidate = Path.Combine(current.FullName, "variables.json");
        if (File.Exists(candidate))
        {
            return (candidate, remainingArgs.ToArray());
        }

        current = current.Parent;
    }

    return (null, remainingArgs.ToArray());
}

