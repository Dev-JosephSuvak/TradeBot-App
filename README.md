# TradeBot App

## 50,000-foot view

`TradeBot App` is a paper-trading market analysis bot written in .NET 8. It pulls historical market data from multiple sources, runs a simple SMA crossover strategy, and emits simulated trade decisions without ever placing a live order.

The bot is designed around provider adapters:

- A **Python bridge** that invokes Yahoo Finance data collection from .NET
- A **Twelve Data** REST client for historical time series when an API key is present
- A **Polygon.io** REST client for aggregated bars when an API key is present
- A **Binance** REST client for public spot klines when available in your region

Every provider feeds a shared strategy pipeline, and every result is converted into a **paper trade only** decision and written to `src/TradeBot.App/output/latest-run.json`.

## Tech stack

- **Runtime:** .NET 8, C# 12
- **Python bridge:** Python 3, `yfinance` (optional but recommended)
- **Serialization:** `System.Text.Json`
- **Tests:** xUnit
- **External providers:** Yahoo Finance (via Python), Twelve Data, Polygon.io, Binance

## Key behaviors

- Never sends a live order
- Treats every execution as **simulation only**
- Works without API keys for Yahoo bridge fallback mode
- Accepts real API keys for Twelve Data and Polygon via environment variables or `appsettings.json`
- Handles unavailable providers gracefully and records why they failed

## Project layout

- `src/TradeBot.App/Program.cs` — entry point and composition root
- `src/TradeBot.App/Services/` — providers, strategy, and paper-trading orchestration
- `src/TradeBot.App/Infrastructure/PythonBridgeRunner.cs` — .NET-to-Python process bridge
- `src/TradeBot.App/Python/yahoo_bridge.py` — Python Yahoo Finance fetcher with synthetic fallback
- `src/TradeBot.App/appsettings.json` — local configuration
- `tests/TradeBot.App.Tests/` — strategy and safety tests
- `PROCESS.md` — architecture notes and conversation log

## Configuration

Environment variables override local config when present:

- `TRADEBOT_PYTHON`
- `TRADEBOT_OUTPUT_DIR`
- `TWELVE_DATA_API_KEY`
- `TWELVE_DATA_ENABLED`
- `POLYGON_API_KEY`
- `POLYGON_ENABLED`
- `BINANCE_ENABLED`

If you want live Yahoo Finance downloads instead of the deterministic fallback, install the Python dependency from `requirements.txt`.

## How to run

Install the optional Python dependency for real Yahoo Finance historical pulls:

```zsh
python3 -m pip install -r requirements.txt
```

Build the solution:

```zsh
dotnet build TradeBotApp.sln
```

Run tests:

```zsh
dotnet test TradeBotApp.sln --no-build
```

Run the bot:

```zsh
dotnet run --project src/TradeBot.App/TradeBot.App.csproj
```

## What the bot outputs

- Console summaries for each provider + symbol pair
- A machine-readable report at `src/TradeBot.App/output/latest-run.json`
- Simulated actions such as `Buy`, `Sell`, or `Hold`
- Unavailable provider notes, such as missing API keys or regional API restrictions

## Notes

- `Binance` is integrated, but public access can still be region-restricted and may return HTTP `451` depending on your location.
- `Twelve Data` and `Polygon.io` integrations are implemented but disabled by default until API keys are provided.
- Yahoo Finance is always accessed through the Python bridge to satisfy the cross-runtime requirement.
