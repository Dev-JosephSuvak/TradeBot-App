# Options quant engine — implementation notes

The current research module is `python/prediction/options_analytics.py`.
Run tests: `python3 -m unittest discover -s tests -p 'test_options_analytics.py'`.

## Implemented (paper/research only)

- European Black–Scholes theoretical price, delta, gamma, theta/day, vega, rho.
- European implied volatility inversion via bisection.
- Long-option exits evaluated using **bid**, not last or midpoint.
- Explicit configurable stop, profit target, trailing drawdown, time-to-expiry,
  thesis-invalidation, and spread-review signals.
- No order submission or broker connectivity. Signals are not guaranteed fills.

## Critical constraints before broker integration

- Most U.S. equity options are American-style; implement an American pricing
  model (e.g. binomial tree) and exercise/assignment handling before using
  theoretical value as an execution signal. Futures options require their
  applicable model and contract specifications.
- Broker Greeks and IV are often computed with different assumptions.
  Preserve timestamps, underlying reference prices, and quote sources.
- Use actual contract multiplier, tick size, commissions, bid/ask spread,
  exchange fees, and realistic margin/buying power.
- Long options can lose the entire premium. A stop order cannot cap losses
  in gaps or illiquid markets. Short options and multi-leg spreads need
  **position-level** exit policies and assignment risk.
- The existing price-bounds module is not calibrated to 90% realized coverage.
  Validate forecasts on held-out walk-forward periods before linking to exits.
- Hard reject any simulated position whose realistic maximum loss or margin
  exceeds the $400 starting paper-capital envelope.
- Never allow an LLM to bypass the independent risk engine.

## Next implementation milestones

1. Market data contracts for options chains, Greeks, quote timestamps, and
   contract identifiers from IBKR paper gateway.
2. Scenario repricing across underlying price, volatility, and elapsed time.
3. Volatility-aware adaptive thresholds and empirical risk/reward calibration.
4. Position-level strategy support: long calls/puts, vertical spreads, and
   defined-risk futures strategies only after realistic buying-power checks.
5. Walk-forward replay with fill assumptions, slippage and fees.
6. API endpoints, dashboard, order state machine, reconciliation, kill switch.

These are engineering and research features, not profitability guarantees.
