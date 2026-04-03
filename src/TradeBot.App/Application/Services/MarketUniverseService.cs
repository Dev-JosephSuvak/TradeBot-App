using System.Globalization;
using System.Text.Json;
using TradeBot.App.Application.Contracts.Api;
using TradeBot.App.Infrastructure.Settings;

namespace TradeBot.App.Application.Services;

public sealed class MarketUniverseService
{
    private static readonly HttpClient HttpClient = new() { Timeout = TimeSpan.FromSeconds(20) };
    private readonly TradeBotSettings _settings;

    public MarketUniverseService(TradeBotSettings settings)
    {
        _settings = settings;
    }

    public async Task<MarketUniverseDto> GetUniverseAsync(string? search, int? limitPerCategory, CancellationToken ct)
    {
        var normalizedSearch = string.IsNullOrWhiteSpace(search)
            ? null
            : search.Trim().ToUpperInvariant();

        var stocks = new Dictionary<string, MarketTickerDto>(StringComparer.OrdinalIgnoreCase);
        var etfs = new Dictionary<string, MarketTickerDto>(StringComparer.OrdinalIgnoreCase);

        await TryLoadNasdaqUniverseAsync(stocks, etfs, ct);
        ApplyConfiguredSymbolFallback(stocks, etfs);

        var cryptos = await LoadCryptoUniverseAsync(ct);

        var filteredStocks = FilterAndLimit(stocks.Values, normalizedSearch, limitPerCategory);
        var filteredEtfs = FilterAndLimit(etfs.Values, normalizedSearch, limitPerCategory);
        var filteredCryptos = FilterAndLimit(cryptos, normalizedSearch, limitPerCategory);

        return new MarketUniverseDto(
            AsOfUtc: DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
            Stocks: filteredStocks,
            Etfs: filteredEtfs,
            Cryptos: filteredCryptos,
            TotalCount: filteredStocks.Count + filteredEtfs.Count + filteredCryptos.Count);
    }

    private async Task TryLoadNasdaqUniverseAsync(
        IDictionary<string, MarketTickerDto> stocks,
        IDictionary<string, MarketTickerDto> etfs,
        CancellationToken ct)
    {
        var nasdaqListedUrl = "https://www.nasdaqtrader.com/dynamic/SymDir/nasdaqlisted.txt";
        var otherListedUrl = "https://www.nasdaqtrader.com/dynamic/SymDir/otherlisted.txt";

        try
        {
            var nasdaqContent = await HttpClient.GetStringAsync(nasdaqListedUrl, ct);
            ParseNasdaqPipeFile(nasdaqContent, stocks, etfs, "NASDAQ", "NASDAQTrader");
        }
        catch
        {
        }

        try
        {
            var otherContent = await HttpClient.GetStringAsync(otherListedUrl, ct);
            ParseNasdaqPipeFile(otherContent, stocks, etfs, "US", "NASDAQTrader");
        }
        catch
        {
        }
    }

    private static void ParseNasdaqPipeFile(
        string fileContent,
        IDictionary<string, MarketTickerDto> stocks,
        IDictionary<string, MarketTickerDto> etfs,
        string defaultExchange,
        string provider)
    {
        var lines = fileContent
            .Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries)
            .Where(line => !line.StartsWith("File Creation Time", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (lines.Count < 2)
        {
            return;
        }

        var header = lines[0].Split('|');
        var symbolIndex = FindHeaderIndex(header, "Symbol", "ACT Symbol", "NASDAQ Symbol");
        var nameIndex = FindHeaderIndex(header, "Security Name");
        var etfIndex = FindHeaderIndex(header, "ETF");
        var testIssueIndex = FindHeaderIndex(header, "Test Issue");
        var exchangeIndex = FindHeaderIndex(header, "Exchange");

        if (symbolIndex < 0)
        {
            return;
        }

        for (var index = 1; index < lines.Count; index++)
        {
            var columns = lines[index].Split('|');
            if (columns.Length <= symbolIndex)
            {
                continue;
            }

            var symbol = columns[symbolIndex].Trim().ToUpperInvariant();
            if (string.IsNullOrWhiteSpace(symbol))
            {
                continue;
            }

            var isTestIssue = testIssueIndex >= 0 && columns.Length > testIssueIndex
                && string.Equals(columns[testIssueIndex].Trim(), "Y", StringComparison.OrdinalIgnoreCase);
            if (isTestIssue)
            {
                continue;
            }

            var name = nameIndex >= 0 && columns.Length > nameIndex
                ? columns[nameIndex].Trim()
                : symbol;

            var exchange = exchangeIndex >= 0 && columns.Length > exchangeIndex
                ? columns[exchangeIndex].Trim()
                : defaultExchange;

            var isEtf = etfIndex >= 0 && columns.Length > etfIndex
                && string.Equals(columns[etfIndex].Trim(), "Y", StringComparison.OrdinalIgnoreCase);

            var ticker = new MarketTickerDto(
                Ticker: symbol,
                Name: string.IsNullOrWhiteSpace(name) ? symbol : name,
                AssetClass: isEtf ? "ETF" : "STOCK",
                Exchange: string.IsNullOrWhiteSpace(exchange) ? defaultExchange : exchange,
                Provider: provider);

            if (isEtf)
            {
                etfs[symbol] = ticker;
            }
            else
            {
                stocks[symbol] = ticker;
            }
        }
    }

    private async Task<IReadOnlyList<MarketTickerDto>> LoadCryptoUniverseAsync(CancellationToken ct)
    {
        var cryptos = new Dictionary<string, MarketTickerDto>(StringComparer.OrdinalIgnoreCase);

        try
        {
            using var response = await HttpClient.GetAsync("https://api.binance.com/api/v3/exchangeInfo", ct);
            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: ct);

            if (document.RootElement.TryGetProperty("symbols", out var symbols)
                && symbols.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in symbols.EnumerateArray())
                {
                    if (!item.TryGetProperty("status", out var statusElement)
                        || !string.Equals(statusElement.GetString(), "TRADING", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    if (!item.TryGetProperty("baseAsset", out var baseAssetElement)
                        || !item.TryGetProperty("quoteAsset", out var quoteAssetElement))
                    {
                        continue;
                    }

                    var baseAsset = baseAssetElement.GetString()?.Trim().ToUpperInvariant();
                    var quoteAsset = quoteAssetElement.GetString()?.Trim().ToUpperInvariant();
                    if (string.IsNullOrWhiteSpace(baseAsset) || string.IsNullOrWhiteSpace(quoteAsset))
                    {
                        continue;
                    }

                    var ticker = $"{baseAsset}-{quoteAsset}";
                    cryptos[ticker] = new MarketTickerDto(
                        Ticker: ticker,
                        Name: $"{baseAsset}/{quoteAsset}",
                        AssetClass: "CRYPTO",
                        Exchange: "Binance",
                        Provider: "Binance");
                }
            }
        }
        catch
        {
        }

        foreach (var symbol in _settings.Symbols)
        {
            if (!symbol.Contains('-', StringComparison.Ordinal))
            {
                continue;
            }

            var upper = symbol.ToUpperInvariant();
            cryptos.TryAdd(upper, new MarketTickerDto(
                Ticker: upper,
                Name: upper,
                AssetClass: "CRYPTO",
                Exchange: "Configured",
                Provider: "Settings"));
        }

        return cryptos.Values
            .OrderBy(item => item.Ticker, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private void ApplyConfiguredSymbolFallback(
        IDictionary<string, MarketTickerDto> stocks,
        IDictionary<string, MarketTickerDto> etfs)
    {
        foreach (var symbol in _settings.Symbols)
        {
            if (symbol.Contains('-', StringComparison.Ordinal))
            {
                continue;
            }

            var upper = symbol.ToUpperInvariant();
            if (!stocks.ContainsKey(upper) && !etfs.ContainsKey(upper))
            {
                stocks[upper] = new MarketTickerDto(
                    Ticker: upper,
                    Name: upper,
                    AssetClass: "STOCK",
                    Exchange: "Configured",
                    Provider: "Settings");
            }
        }
    }

    private static List<MarketTickerDto> FilterAndLimit(
        IEnumerable<MarketTickerDto> source,
        string? search,
        int? limitPerCategory)
    {
        var filtered = source
            .Where(item => search is null
                           || item.Ticker.Contains(search, StringComparison.OrdinalIgnoreCase)
                           || item.Name.Contains(search, StringComparison.OrdinalIgnoreCase))
            .OrderBy(item => item.Ticker, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (limitPerCategory is > 0)
        {
            filtered = filtered.Take(limitPerCategory.Value).ToList();
        }

        return filtered;
    }

    private static int FindHeaderIndex(string[] headers, params string[] names)
    {
        for (var index = 0; index < headers.Length; index++)
        {
            var candidate = headers[index].Trim();
            if (names.Any(name => string.Equals(candidate, name, StringComparison.OrdinalIgnoreCase)))
            {
                return index;
            }
        }

        return -1;
    }
}