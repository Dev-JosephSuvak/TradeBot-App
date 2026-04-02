using System.Globalization;
using System.Text.Json;
using TradeBot.App.Application.Contracts;
using TradeBot.App.Application.Providers;
using TradeBot.App.Domain;
using TradeBot.App.Infrastructure.Settings;

namespace TradeBot.App.Application.Services;

public sealed class TwelveDataProvider : IMarketDataProvider
{
	private readonly HttpClient _httpClient;
	private readonly TradeBotSettings _settings;

	public TwelveDataProvider(HttpClient httpClient, TradeBotSettings settings)
	{
		_httpClient = httpClient;
		_settings = settings;
	}

	public string Name => "TwelveData";

	public bool IsEnabled => _settings.TwelveData.Enabled && !string.IsNullOrWhiteSpace(_settings.TwelveData.ApiKey);

	public async Task<MarketDataResponse> GetHistoricalDataAsync(string symbol, int lookbackDays, CancellationToken cancellationToken)
	{
		if (!IsEnabled)
		{
			return MarketDataResponse.Disabled(Name, symbol, "Provider disabled or missing API key.");
		}

		var requestUri = $"https://api.twelvedata.com/time_series?symbol={Uri.EscapeDataString(symbol)}&interval=1day&outputsize={lookbackDays}&apikey={Uri.EscapeDataString(_settings.TwelveData.ApiKey)}&format=JSON";
		using var response = await _httpClient.GetAsync(requestUri, cancellationToken);
		var payload = await response.Content.ReadAsStringAsync(cancellationToken);

		if (!response.IsSuccessStatusCode)
		{
			return MarketDataResponse.Disabled(Name, symbol, $"HTTP {(int)response.StatusCode}: {payload}");
		}

		using var document = JsonDocument.Parse(payload);
		if (document.RootElement.TryGetProperty("status", out var statusElement) && statusElement.GetString() == "error")
		{
			var message = document.RootElement.TryGetProperty("message", out var messageElement)
				? messageElement.GetString() ?? "Unknown Twelve Data error."
				: "Unknown Twelve Data error.";
			return MarketDataResponse.Disabled(Name, symbol, message);
		}

		if (!document.RootElement.TryGetProperty("values", out var valuesElement))
		{
			return MarketDataResponse.Disabled(Name, symbol, "Twelve Data returned no historical values.");
		}

		var candles = valuesElement
			.EnumerateArray()
			.Select(entry => new MarketCandle(
				DateTime.SpecifyKind(DateTime.Parse(entry.GetProperty("datetime").GetString()!), DateTimeKind.Utc),
				decimal.Parse(entry.GetProperty("open").GetString()!, CultureInfo.InvariantCulture),
				decimal.Parse(entry.GetProperty("high").GetString()!, CultureInfo.InvariantCulture),
				decimal.Parse(entry.GetProperty("low").GetString()!, CultureInfo.InvariantCulture),
				decimal.Parse(entry.GetProperty("close").GetString()!, CultureInfo.InvariantCulture),
				decimal.Parse(entry.GetProperty("volume").GetString() ?? "0", CultureInfo.InvariantCulture)))
			.OrderBy(candle => candle.TimestampUtc)
			.ToList();

		return MarketDataResponse.Ok(Name, symbol, candles, $"Fetched {candles.Count} candles from Twelve Data.");
	}
}
