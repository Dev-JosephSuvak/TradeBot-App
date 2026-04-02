from __future__ import annotations

import argparse
import json
import math
import sys
from datetime import UTC, datetime, timedelta
from typing import Any


def build_parser() -> argparse.ArgumentParser:
	parser = argparse.ArgumentParser(description="Yahoo Finance historical data bridge")
	parser.add_argument("--symbol", required=True)
	parser.add_argument("--lookback-days", type=int, default=30)
	return parser


def try_fetch_yfinance(symbol: str, lookback_days: int) -> dict[str, Any] | None:
	try:
		import yfinance as yf  # type: ignore
	except Exception:
		return None

	ticker = yf.Ticker(symbol)
	history = ticker.history(period=f"{lookback_days + 5}d", interval="1d", auto_adjust=False)
	if history.empty:
		return None

	candles: list[dict[str, Any]] = []
	for index, row in history.tail(lookback_days).iterrows():
		timestamp = index.to_pydatetime()
		if timestamp.tzinfo is None:
			timestamp = timestamp.replace(tzinfo=UTC)
		else:
			timestamp = timestamp.astimezone(UTC)

		candles.append(
			{
				"timestampUtc": timestamp.isoformat().replace("+00:00", "Z"),
				"open": round(float(row["Open"]), 4),
				"high": round(float(row["High"]), 4),
				"low": round(float(row["Low"]), 4),
				"close": round(float(row["Close"]), 4),
				"volume": round(float(row.get("Volume", 0.0)), 4),
			}
		)

	return {"provider": "YahooFinance", "mode": "yfinance", "candles": candles}


def build_synthetic_series(symbol: str, lookback_days: int) -> dict[str, Any]:
	base_price = 100 + (sum(ord(char) for char in symbol) % 70)
	today = datetime.now(tz=UTC).replace(hour=0, minute=0, second=0, microsecond=0)
	candles: list[dict[str, Any]] = []

	for offset in range(lookback_days):
		timestamp = today - timedelta(days=lookback_days - offset)
		trend = offset * 0.85
		cycle = math.sin(offset / 3.0) * 2.75
		close = round(base_price + trend + cycle, 4)
		open_price = round(close - 0.9, 4)
		high = round(close + 1.25, 4)
		low = round(close - 1.4, 4)
		volume = round(1_000_000 + (offset * 1250), 4)
		candles.append(
			{
				"timestampUtc": timestamp.isoformat().replace("+00:00", "Z"),
				"open": open_price,
				"high": high,
				"low": low,
				"close": close,
				"volume": volume,
			}
		)

	return {
		"provider": "YahooFinance",
		"mode": "synthetic-fallback",
		"message": "yfinance unavailable or returned no data; synthetic candles generated for local testing.",
		"candles": candles,
	}


def main() -> int:
	args = build_parser().parse_args()
	payload = try_fetch_yfinance(args.symbol, args.lookback_days) or build_synthetic_series(args.symbol, args.lookback_days)
	json.dump(payload, sys.stdout)
	return 0


if __name__ == "__main__":
	raise SystemExit(main())
