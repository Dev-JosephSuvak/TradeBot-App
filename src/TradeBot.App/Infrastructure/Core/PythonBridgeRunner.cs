using System.Diagnostics;
using System.Text.Json;
using TradeBot.App.Domain;

namespace TradeBot.App.Infrastructure.Core;

public sealed class PythonBridgeRunner
{
	public async Task<IReadOnlyList<MarketCandle>> FetchCandlesAsync(
		string pythonExecutable,
		string scriptPath,
		string symbol,
		int lookbackDays,
		CancellationToken cancellationToken)
	{
		var startInfo = new ProcessStartInfo
		{
			FileName = pythonExecutable,
			RedirectStandardOutput = true,
			RedirectStandardError = true,
			UseShellExecute = false
		};

		startInfo.ArgumentList.Add(scriptPath);
		startInfo.ArgumentList.Add("--symbol");
		startInfo.ArgumentList.Add(symbol);
		startInfo.ArgumentList.Add("--lookback-days");
		startInfo.ArgumentList.Add(lookbackDays.ToString());

		using var process = new Process { StartInfo = startInfo };
		process.Start();

		var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
		var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);

		await process.WaitForExitAsync(cancellationToken);
		var stdout = await stdoutTask;
		var stderr = await stderrTask;

		if (process.ExitCode != 0)
		{
			throw new InvalidOperationException($"Yahoo Finance Python bridge failed: {stderr}".Trim());
		}

		if (string.IsNullOrWhiteSpace(stdout))
		{
			throw new InvalidOperationException("Yahoo Finance Python bridge returned empty output.");
		}

		using var document = JsonDocument.Parse(stdout);
		var root = document.RootElement;

		if (!root.TryGetProperty("candles", out var candlesElement))
		{
			var message = root.TryGetProperty("message", out var messageElement)
				? messageElement.GetString()
				: "Yahoo Finance Python bridge returned malformed JSON.";
			throw new InvalidOperationException(message);
		}

		var candles = new List<MarketCandle>();
		foreach (var candleElement in candlesElement.EnumerateArray())
		{
			candles.Add(new MarketCandle(
				candleElement.GetProperty("timestampUtc").GetDateTime(),
				candleElement.GetProperty("open").GetDecimal(),
				candleElement.GetProperty("high").GetDecimal(),
				candleElement.GetProperty("low").GetDecimal(),
				candleElement.GetProperty("close").GetDecimal(),
				candleElement.GetProperty("volume").GetDecimal()));
		}

		return candles;
	}
}
