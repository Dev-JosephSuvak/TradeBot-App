using System.Diagnostics;
using System.Text.Json;
using TradeBot.App.Application.Contracts.Api;

namespace TradeBot.App.Application.Services;

/// <summary>Invokes the bundled, research-only Python price-bounds model.</summary>
public sealed class PriceBoundsService
{
    private readonly MarketIndexService _marketIndex;
    private readonly string _projectRoot;

    public PriceBoundsService(MarketIndexService marketIndex, string projectRoot)
    {
        _marketIndex = marketIndex;
        _projectRoot = projectRoot;
    }

    public async Task<JsonElement> PredictAsync(
        string ticker, int lookback, int horizon, double coverage, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(ticker) || ticker.Length > 32 ||
            ticker.Any(c => !(char.IsAsciiLetterOrDigit(c) || c is '.' or '-' or '_')))
            throw new ArgumentException("Invalid ticker.");
        if (lookback < 20 || lookback > 1000 || horizon < 1 || horizon > 60 ||
            coverage <= 0.5 || coverage >= 1)
            throw new ArgumentException("lookback must be 20-1000, horizon 1-60, coverage between 0.5 and 1.");

        // Historical quantile calibration needs lookback + horizon + 1 observations.
        var candles = await _marketIndex.GetCandlesAsync(ticker, lookback + horizon + 1, ct);
        if (candles.Count < lookback + horizon + 1)
            throw new InvalidOperationException("Insufficient historical candles for price bounds.");

        var script = Path.Combine(_projectRoot, "src", "TradeBot.App",
            "Infrastructure", "Core", "lotteryds_price_bounds_bridge.py");
        if (!File.Exists(script))
            throw new FileNotFoundException("Bundled price-bounds bridge not found.", script);

        var start = new ProcessStartInfo
        {
            FileName = Environment.GetEnvironmentVariable("TRADEBOT_PYTHON") ?? "python3",
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        start.ArgumentList.Add(script);
        using var process = new Process { StartInfo = start };
        process.Start();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(15));
        try
        {
            var payload = JsonSerializer.Serialize(new
            {
                candles, lookback, horizon, coverage
            });
            await process.StandardInput.WriteAsync(payload.AsMemory(), timeout.Token);
            process.StandardInput.Close();
            var outputTask = process.StandardOutput.ReadToEndAsync(timeout.Token);
            var errorTask = process.StandardError.ReadToEndAsync(timeout.Token);
            await process.WaitForExitAsync(timeout.Token);
            var output = await outputTask;
            var error = await errorTask;
            if (process.ExitCode != 0)
                throw new InvalidOperationException("Price-bounds process failed: " + error[..Math.Min(error.Length, 500)]);
            using var doc = JsonDocument.Parse(output);
            return doc.RootElement.Clone();
        }
        catch
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
            throw;
        }
    }
}
