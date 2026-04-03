# TradeBot App

## 50,000-foot view

`TradeBot App` is a paper-trading market analysis app written in .NET 8. It exposes a Web API for market candles and indicators, pulls data from multiple providers, and can also run a batch paper-trading workflow without ever placing a live order.

The bot is designed around provider adapters:

- A **Python bridge** that invokes Yahoo Finance data collection from .NET
- A **Twelve Data** REST client for historical time series when an API key is present
- A **Polygon.io** REST client for aggregated bars when an API key is present
- A **Binance** REST client for public spot klines when available in your region

Every provider feeds a shared strategy pipeline, and every result is converted into a **paper trade only** decision and written to `src/TradeBot.App/output/latest-run.json` when batch mode is enabled.

## Tech stack

- **Runtime:** .NET 8, C# 12
- **Web API:** ASP.NET Core minimal API + Swagger (`Swashbuckle.AspNetCore`)
- **Python bridge:** Python 3, `yfinance`, `pandas`, optional `TA-Lib`
- **Serialization:** `System.Text.Json`
- **Tests:** xUnit
- **External providers:** Yahoo Finance (via Python), Twelve Data, Polygon.io, Binance
- **Persistence:** Firebase/Firestore (primary) with local JSON fallback

## Key behaviors

- Never sends a live order
- Treats every execution as **simulation only**
- Maintains a paper portfolio with starting balance, positions, trade history, and P&L
- Enforces risk controls: max position size and stop-loss
- Caches ticker aggregates for 5 minutes for UI responsiveness
- Works without API keys for Yahoo bridge fallback mode
- Accepts real API keys for Twelve Data and Polygon via environment variables or `appsettings.json`
- Handles unavailable providers gracefully and records why they failed

## Project layout

- `src/TradeBot.App/Program.cs` — API + batch composition root
- `src/TradeBot.App/Application/` — contracts, providers, strategy, and services
- `src/TradeBot.App/Infrastructure/Core/` — Python bridge runners + Python scripts
- `src/TradeBot.App/Infrastructure/Settings/` — settings loader and models
- `src/TradeBot.App/Infrastructure/DataAccess/Persistence/` — file + Firestore run-report stores
- `src/TradeBot.App/appsettings.json` — local configuration
- `tests/TradeBot.App.Tests/` — strategy and safety tests
- `tradebot-ui/` — React dashboard for charts, portfolio management, and executed trades
- `compose.yaml` — master run pipeline for API + UI containers
- `.dockerignore` — shared Docker build exclusions
- `PROCESS.md` — architecture notes and conversation log

## Contract file map

- `Candle`
	- `src/TradeBot.App/Application/Contracts/Api/CandleDto.cs`
	- `src/TradeBot.App/Application/Services/MarketIndexService.cs`
	- `src/TradeBot.App/Domain/MarketCandle.cs`
- `Signal`
	- `src/TradeBot.App/Application/Contracts/Api/SignalDto.cs`
	- `src/TradeBot.App/Application/Services/ContractMapper.cs`
	- `src/TradeBot.App/Application/Services/SmaCrossoverStrategy.cs`
	- `src/TradeBot.App/Domain/TradeSignal.cs`
- `Trade`
	- `src/TradeBot.App/Application/Contracts/Api/TradeDto.cs`
	- `src/TradeBot.App/Application/Services/ContractMapper.cs`
	- `src/TradeBot.App/Application/Services/PaperPortfolioService.cs`
	- `src/TradeBot.App/Domain/PortfolioTrade.cs`
- `PortfolioState`
	- `src/TradeBot.App/Application/Contracts/Api/PortfolioStateDto.cs`
	- `src/TradeBot.App/Application/Services/ContractMapper.cs`
	- `src/TradeBot.App/Application/Services/PaperPortfolioService.cs`
	- `src/TradeBot.App/Domain/PortfolioState.cs`
- `Strategy`
	- `src/TradeBot.App/Application/Contracts/Simulation/IStrategy.cs`
	- `src/TradeBot.App/Application/Services/SmaCrossoverStrategy.cs`
- `Engine`
	- `src/TradeBot.App/Application/Contracts/Simulation/IEngine.cs`
	- `src/TradeBot.App/Application/Services/SimulationEngine.cs`
- `SimulationResult`
	- `src/TradeBot.App/Application/Contracts/Api/SimulationResultDto.cs`
	- `src/TradeBot.App/Application/Services/ContractMapper.cs`
	- `src/TradeBot.App/Application/Services/SimulationEngine.cs`
	- `src/TradeBot.App/Program.cs`

## Configuration

Environment variables override local config when present:

- `TRADEBOT_PYTHON`
- `TRADEBOT_INDICATORS_BRIDGE`
- `TRADEBOT_OUTPUT_DIR`
- `FIREBASE_PORTFOLIO_COLLECTION_NAME`
- `PORTFOLIO_STARTING_BALANCE_USD`
- `PORTFOLIO_MAX_POSITION_SIZE_PERCENT`
- `PORTFOLIO_STOP_LOSS_PERCENT`
- `PORTFOLIO_CACHE_TTL_MINUTES`
- `PORTFOLIO_DEFAULT_TRADE_STRATEGY`
- `TWELVE_DATA_API_KEY`
- `TWELVE_DATA_ENABLED`
- `POLYGON_API_KEY`
- `POLYGON_ENABLED`
- `BINANCE_ENABLED`

If you want live Yahoo Finance downloads and richer indicator math, install dependencies from `requirements.txt`.

## Runtime globals

The app can populate runtime environment variables from a single globals file.

- Pass a file explicitly with `--globals /absolute/path/to/file.json`
- If no globals file is passed, the app automatically looks for `variables.json`
- Existing process environment variables still take precedence over file-provided values

Example:

```zsh
dotnet run --project src/TradeBot.App/TradeBot.App.csproj -- --globals '/Users/you/path/to/globals.json'
```

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

Run the API:

```zsh
dotnet run --project src/TradeBot.App/TradeBot.App.csproj
```

Run the React UI (in a second terminal):

```zsh
cd tradebot-ui
npm install
npm run dev
```

The UI runs on `http://localhost:5173` and proxies `/api/*` requests to `http://localhost:5100`.

## Master run process

Run the full environment with Docker Compose:

```zsh
docker compose up --build
```

Endpoints when compose is running:

- UI: `http://localhost:5173`
- API: `http://localhost:5100`
- Swagger: `http://localhost:5100`

Stop the stack:

```zsh
docker compose down
```

Container files:

- `compose.yaml`
- `src/TradeBot.App/Dockerfile`
- `tradebot-ui/Dockerfile`
- `tradebot-ui/Caddyfile`

## UI features

- Chart modes: `Line`, `Candle`, `Range Line`, `Range Bar`, `Heikin Ashi`, `Kagi`
- Ticker selection from `/api/market-universe/tickers`
- Portfolio summary cards from `/api/portfolio`
- Trade execution form posting to `/api/portfolio/execute`
- Executed trades table from `/api/portfolio/trades`

## API endpoints

- `GET /api/candles/{ticker}?lookbackDays=60`
	- Returns ISO-8601 OHLCV candles
	- Falls back to simulated candles when live providers are unavailable
- `GET /api/signals/{ticker}`
	- Returns normalized signal contract: `action`, `reason`, `strategy_name`, `rsi`, `mac`
	- Uses configured SMA windows from app settings
- `GET /api/indicators/{ticker}?period=15&periodType=days&shortWindow=5&longWindow=20`
	- Returns dynamic RSI + moving-average crossover output
	- Includes `avgLowRSI`, `avgHighRSI`, `relativeStrength`, `trendDirection`, and `volumeTraded`
	- Computes across these period buckets: `5m`, `15m`, `1h`, `1d`, `1mo`, `1y`, `5y`, `max`
- `GET /api/market-universe/tickers?search=&limitPerCategory=`
	- Returns grouped ticker universe for `stocks`, `etfs`, and `cryptos`
	- Retrieves tickers from public sources with configured-symbol fallback
- `GET /api/portfolio`
	- Returns spec-shaped portfolio state: `cash`, `position_quantity`, `entry_price`, `equity`
- `POST /api/portfolio/execute`
	- Executes simulated buy/sell trades with risk checks
	- Simplified UI payload: `ticker`, `action`, `quantity`, `price`, optional `reason`, optional `strategy`
	- Response includes `trade`, `final_portfolio`, `realized_pnl`, and `unrealized_pnl`
- `GET /api/portfolio/positions`
	- Returns open positions
- `GET /api/portfolio/trades?limit=100`
	- Returns latest trade history entries shaped as `timestamp`, `action`, `price`, `quantity`, `strategy_name`, `reason`
- `GET /api/portfolio/simulation-result`
	- Returns spec-shaped simulation output: `trades`, `final_portfolio`, `realized_pnl`, `unrealized_pnl`
- `GET /api/ticker-cache/{ticker}`
	- Returns cached aggregate snapshot (5-minute TTL default)
	- Refreshes from market data when cache expired and applies stop-loss checks

## Swagger contract coverage

Swagger now publishes explicit schema models for these contract DTOs:

- `CandleDto`
- `SignalDto`
- `TradeDto`
- `PortfolioStateDto`
- `PortfolioExecutionResponseDto`
- `SimulationResultDto`
- `IndicatorResponseDto`
- `IndicatorPeriodMetricsDto`
- `MarketTickerDto`
- `MarketUniverseDto`

Example call:

```zsh
curl "http://localhost:5100/api/indicators/AAPL?period=15&periodType=days&shortWindow=5&longWindow=20"
```

## What the bot outputs

- JSON API responses for candles and indicators
- Trade records including timestamp, price, quantity, notional, reason, and triggering strategy
- Console summaries for each provider + symbol pair in batch mode
- Console trade logs for every executed portfolio trade and stop-loss exit
- A machine-readable report at `src/TradeBot.App/output/latest-run.json`
- Simulated actions such as `Buy`, `Sell`, or `Hold`
- Contract-shaped portfolio actions such as `BUY`, `SELL`, or `HOLD`
- Unavailable provider notes, such as missing API keys or regional API restrictions

## Notes

- `Binance` is integrated, but public access can still be region-restricted and may return HTTP `451` depending on your location.
- `Twelve Data` and `Polygon.io` integrations are implemented but disabled by default until API keys are provided.

- Yahoo Finance and indicator analytics are accessed through Python bridges to satisfy the cross-runtime requirement, but utilize the inherent security and structure provided by a .NET environment.

- The API container installs `python3`, `yfinance`, `pandas`, and `numpy`; `TA-Lib` remains optional and the indicator bridge already falls back when it is unavailable.

-Further work required to upload this to Firebase. It prefers a Firebase connection first and falls back to the JSON requirement as specified in the notes with absence of the environmental variable being set. 
