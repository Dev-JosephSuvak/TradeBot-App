#!/usr/bin/env python3
"""Research-only price bounds from OHLC candles. No brokerage orders.

Input CSV: timestamp,open,high,low,close (timestamp optional).
For each historical origin t, calculate the highest high and lowest low over
(t, t+horizon]. Only completed future windows enter calibration; no leakage.
"""
import argparse
import csv
import json
import math
from pathlib import Path


def quantile(values, probability):
    ordered = sorted(values)
    if not ordered:
        raise ValueError("No observations")
    x = (len(ordered) - 1) * probability
    lo = math.floor(x)
    hi = math.ceil(x)
    return ordered[lo] * (hi - x) + ordered[hi] * (x - lo) if lo != hi else ordered[lo]


def estimate(rows, lookback=100, horizon=5, coverage=0.90):
    if not (0 < coverage < 1):
        raise ValueError("coverage must be between 0 and 1")
    if horizon < 1 or lookback < 20:
        raise ValueError("horizon >= 1 and lookback >= 20 required")
    data = []
    for row in rows:
        high, low, close = (float(row[k]) for k in ("high", "low", "close"))
        if not all(math.isfinite(x) and x > 0 for x in (high, low, close)) or low > high:
            raise ValueError("Invalid OHLC values")
        data.append((high, low, close))
    if len(data) < lookback + horizon + 1:
        raise ValueError("Insufficient history for requested lookback and horizon")
    highs, lows = [], []
    # The last usable origin is len(data)-horizon-1: its outcome is known.
    for t in range(max(0, len(data)-horizon-lookback), len(data)-horizon):
        future = data[t+1:t+1+horizon]
        highs.append(max(r[0] for r in future) / data[t][2] - 1)
        lows.append(min(r[1] for r in future) / data[t][2] - 1)
    alpha = (1 - coverage) / 2
    close = data[-1][2]
    lower_return = quantile(lows, alpha)
    upper_return = quantile(highs, 1-alpha)
    return {
        "method": "historical_forward_extremes_quantile_baseline",
        "horizon_bars": horizon,
        "target_coverage": coverage,
        "observations": len(highs),
        "last_close": close,
        "lower_bound": round(close * (1 + lower_return), 6),
        "upper_bound": round(close * (1 + upper_return), 6),
        "caveat": "Not calibrated to guaranteed coverage; validate walk-forward out of sample."
    }


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--csv", required=True)
    parser.add_argument("--lookback", type=int, default=100)
    parser.add_argument("--horizon", type=int, default=5)
    parser.add_argument("--coverage", type=float, default=0.90)
    args = parser.parse_args()
    with Path(args.csv).open(newline="") as f:
        result = estimate(csv.DictReader(f), args.lookback, args.horizon, args.coverage)
    print(json.dumps(result))


if __name__ == "__main__":
    main()
