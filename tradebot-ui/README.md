# TradeBot UI

React dashboard for the `.NET` TradeBot API.

## Features

- Multi-view charting for one ticker:
  - `Line`
  - `Candle`
  - `Range Line`
  - `Range Bar`
  - `Heikin Ashi`
  - `Kagi`
- Portfolio summary cards
- Trade execution form (paper trades)
- Executed trade history table
- Market ticker universe selector (Stocks, ETFs, Cryptos)

## Run

Start backend API first:

```zsh
dotnet run --project ../src/TradeBot.App/TradeBot.App.csproj
```

Then run frontend:

```zsh
npm install
npm run dev
```

Open: `http://localhost:5173`

## API endpoints used

- `GET /api/candles/{ticker}`
- `GET /api/market-universe/tickers`
- `GET /api/portfolio`
- `GET /api/portfolio/trades`
- `POST /api/portfolio/execute`

## Notes

- Vite dev server proxies `/api/*` requests to `http://localhost:5100` via `vite.config.ts`.
- Backend CORS policy allows `http://localhost:5173`.
