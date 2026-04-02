using System.Globalization;
using System.Text.Json;
using TradeBot.App.Application.Contracts;
using TradeBot.App.Application.Providers;
using TradeBot.App.Domain;
using TradeBot.App.Infrastructure.Settings;

namespace TradeBot.App.Application.Services;

public sealed class PolygonProvider : IMarketDataProvider
{
	private readonly HttpClient _httpClient;
	private readonly TradeBotSettings _settings;

	public PolygonProvider(HttpClient httpClient, TradeBotSettings settings)
	{
		_httpClient = httpClient;
		_settings = settings;
	}

	public string Name => "Polygon";

	public bool IsEnabled => _settings.Polygon.Enabled && !string.IsNullOrWhiteSpace(_settings.Polygon.ApiKey);

	public async Task<MarketDataResponse> GetHistoricalDataAsync(string symbol, int lookbackDays, CancellationToken cancellationToken)
	{
		if (!IsEnabled)
		{
			return MarketDataResponse.Disabled(Name, symbol, "Provider disabled or missing API key.");
		}

		var normalized = NormalizeSymbol(symbol);
		var to = DateOnly.FromDateTime(DateTime.UtcNow);
		var from = to.AddDays(-lookbackDays);
		var requestUri = $"https://api.polygon.io/v2/aggs/ticker/{normalized}/range/1/day/{from:yyyy-MM-dd}/{to:yyyy-MM-dd}?adjusted=true&sort=asc&limit=500&apiKey={Uri.EscapeDataString(_settings.Polygon.ApiKey)}";
		using var response = await _httpClient.GetAsync(requestUri, cancellationToken);
		var payload = await response.Content.ReadAsStringAsync(cancellationToken);

		if (!response.IsSuccessStatusCode)
		{
			return MarketDataResponse.Disabled(Name, symbol, $"HTTP {(int)response.StatusCode}: {payload}");
		}

		using var document = JsonDocument.Parse(payload);
		if (!document.RootElement.TryGetProperty("results", out var resultsElement))
		{
			return MarketDataResponse.Disabled(Name, symbol, "Polygon returned no aggregated bars.");
		}

		var candles = resultsElement
			.EnumerateArray()
			.Select(entry => new MarketCandle(
				DateTimeOffset.FromUnixTimeMilliseconds(entry.GetProperty("t").GetInt64()).UtcDateTime,
				entry.GetProperty("o").GetDecimal(),
				entry.GetProperty("h").GetDecimal(),
				entry.GetProperty("l").GetDecimal(),
				entry.GetProperty("c").GetDecimal(),
				entry.TryGetProperty("v", out var volume) ? volume.GetDecimal() : 0m))
			.ToList();

		return MarketDataResponse.Ok(Name, symbol, candles, $"Fetched {candles.Count} candles from Polygon.");
	}

	private static string NormalizeSymbol(string symbol)
	{
		if (symbol.Contains("-USD", StringComparison.OrdinalIgnoreCase))
		{
			return $"X:{symbol.Replace("-USD", "USD", StringComparison.OrdinalIgnoreCase)}";
		}

		if (symbol.Contains('/'))
		{
			return $"X:{symbol.Replace("/", string.Empty, StringComparison.OrdinalIgnoreCase)}";
		}

		return symbol.ToUpperInvariant();
	}
}
