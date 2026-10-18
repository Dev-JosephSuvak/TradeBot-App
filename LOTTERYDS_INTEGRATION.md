# Price-bounds integration — consolidated into TradeBot-App

The original research idea comes from LotteryDS, but the runnable predictor and
its tests now live entirely in TradeBot-App. LotteryDS is not a runtime dependency.

## Files

- `python/prediction/price_bounds.py`: OHLC forward-extrema quantile baseline
- `tests/test_price_bounds.py`: Python unit tests
- `src/TradeBot.App/Infrastructure/Core/lotteryds_price_bounds_bridge.py`:
  stdin JSON -> local predictor -> stdout JSON

## Run

From the TradeBot-App repository root:

```sh
python3 -m unittest discover -s tests -p test_price_bounds.py
python3 src/TradeBot.App/Infrastructure/Core/lotteryds_price_bounds_bridge.py < candles.json
```

Input JSON shape:

```json
{
  "horizon": 5,
  "lookback": 100,
  "coverage": 0.90,
  "candles": [
    {"timestamp": "2026-01-01", "open": 100, "high": 102, "low": 99, "close": 101}
  ]
}
```

The example shows the schema only; supply at least
`lookback + horizon + 1` chronologically ordered candles.

This model is a historical quantile *baseline*, not a calibrated probability
forecast or trade recommendation. Test out-of-sample coverage, transaction costs,
spread, slippage and account buying power before using it for strategies.

## Status

Code is committed but tests and end-to-end operation have not yet been run.
The predictor is exposed as a read-only .NET endpoint:\n\n`GET /api/price-bounds/AAPL?lookback=100&horizon=5&coverage=0.9`\n\nThe market-data service can fall back to simulated candles, so do not treat\nits output as verified live-market data. The endpoint is not yet wired into\nReact or connected to IBKR. No live or paper brokerage orders are enabled. All brokerage
integrations must default to paper-only with account verification and a kill switch.
