using System.Globalization;
using System.Text.Json;
using TradeBot.App.Application.Contracts;
using TradeBot.App.Application.Providers;
using TradeBot.App.Domain;
using TradeBot.App.Infrastructure.Settings;

namespace TradeBot.App.Application.Services;

public sealed class BinanceProvider : IMarketDataProvider
{
	private readonly HttpClient _httpClient;
	private readonly TradeBotSettings _settings;

	public BinanceProvider(HttpClient httpClient, TradeBotSettings settings)
	{
		_httpClient = httpClient;
		_settings = settings;
	}

	public string Name => "Binance";

	public bool IsEnabled => _settings.Binance.Enabled;

	public async Task<MarketDataResponse> GetHistoricalDataAsync(string symbol, int lookbackDays, CancellationToken cancellationToken)
	{
		if (!IsEnabled)
		{
			return MarketDataResponse.Disabled(Name, symbol, "Provider disabled.");
		}

		var normalized = NormalizeSymbol(symbol);
		if (string.IsNullOrWhiteSpace(normalized))
		{
			return MarketDataResponse.Disabled(Name, symbol, "Symbol is not supported for Binance spot market lookup.");
		}

		var requestUri = $"https://api.binance.com/api/v3/klines?symbol={Uri.EscapeDataString(normalized)}&interval=1d&limit={lookbackDays}";
		using var response = await _httpClient.GetAsync(requestUri, cancellationToken);
		var payload = await response.Content.ReadAsStringAsync(cancellationToken);

		if (!response.IsSuccessStatusCode)
		{
			return MarketDataResponse.Disabled(Name, symbol, $"HTTP {(int)response.StatusCode}: {payload}");
		}

		using var document = JsonDocument.Parse(payload);
		if (document.RootElement.ValueKind != JsonValueKind.Array)
		{
			return MarketDataResponse.Disabled(Name, symbol, "Binance returned an unexpected payload.");
		}

		var candles = new List<MarketCandle>();
		foreach (var entry in document.RootElement.EnumerateArray())
		{
			candles.Add(new MarketCandle(
				DateTimeOffset.FromUnixTimeMilliseconds(entry[0].GetInt64()).UtcDateTime,
				decimal.Parse(entry[1].GetString()!, CultureInfo.InvariantCulture),
				decimal.Parse(entry[2].GetString()!, CultureInfo.InvariantCulture),
				decimal.Parse(entry[3].GetString()!, CultureInfo.InvariantCulture),
				decimal.Parse(entry[4].GetString()!, CultureInfo.InvariantCulture),
				decimal.Parse(entry[5].GetString()!, CultureInfo.InvariantCulture)));
		}

		return MarketDataResponse.Ok(Name, symbol, candles, $"Fetched {candles.Count} candles from Binance.");
	}

	private static string NormalizeSymbol(string symbol)
	{
		if (symbol.EndsWith("-USD", StringComparison.OrdinalIgnoreCase))
		{
			return symbol.Replace("-USD", "USDT", StringComparison.OrdinalIgnoreCase);
		}

		if (symbol.Contains('/'))
		{
			return symbol.Replace("/", string.Empty, StringComparison.OrdinalIgnoreCase).Replace("USD", "USDT", StringComparison.OrdinalIgnoreCase);
		}

		return symbol.EndsWith("USDT", StringComparison.OrdinalIgnoreCase) ? symbol.ToUpperInvariant() : string.Empty;
	}
}
