# LotteryDS ↔ TradeBot integration (research-only first slice)

This branch adds a deterministic JSON boundary between the existing projects.
No brokerage credentials or order-routing code are introduced.

## Setup

Clone both repositories and check out `feature/price-bounds-paper-integration`
in each. Point `LOTTERYDS_ROOT` at the LotteryDS checkout.

## Invoke the bridge

From the TradeBot-App checkout:

```sh
export LOTTERYDS_ROOT="/absolute/path/to/LotteryDS"
python3 src/TradeBot.App/Infrastructure/Core/lotteryds_price_bounds_bridge.py < candles.json
```

`candles.json` must contain:

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

Supply at least `lookback + horizon + 1` chronological OHLC rows.
The example shows the schema, not enough rows for an estimate.
The output contains `lower_bound`, `upper_bound`, `horizon_bars`,
`target_coverage`, and `observations`.

## Interpretation and next steps

This is a simple historical quantile baseline for *forward path extrema*.
It deliberately excludes incomplete future windows from its calibration sample.
It does **not** estimate reliable breakout probabilities or guarantee coverage.
Validate with rolling walk-forward testing against ATR and volatility baselines,
including commissions, spread, slippage, and realistic buying power.

Integration status: callable bridge only. It is **not yet** registered as a
.NET API endpoint, consumed by the React UI, or connected to an IBKR paper account.
Do not represent the existing in-app paper portfolio as broker-side executions.

For the later IBKR connector: require an explicitly configured paper account,
validate the account identifier and gateway port (TWS 7497 / Gateway 4002),
and reject any live endpoint or account. Run an independent pre-trade risk engine
and reconciliation loop before enabling autonomous paper orders.
