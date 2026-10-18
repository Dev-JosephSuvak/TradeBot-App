#!/usr/bin/env python3
"""TradeBot -> LotteryDS price-bound bridge (read-only; never places orders).

Input stdin JSON: {"candles":[{"timestamp":"...","open":...,"high":...,
"low":...,"close":...}], "horizon":5, "lookback":100, "coverage":0.9}
The predictor is bundled in TradeBot-App/python/prediction.
"""
import importlib.util
import json
import os
from pathlib import Path
import sys


def main():
    module_path = Path(__file__).resolve().parents[4] / "python" / "prediction" / "price_bounds.py"
    if not module_path.is_file():
        raise FileNotFoundError(f"Bundled predictor not found: {module_path}")
    spec = importlib.util.spec_from_file_location("lotteryds_price_bounds", module_path)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    payload = json.load(sys.stdin)
    result = module.estimate(
        payload["candles"],
        lookback=int(payload.get("lookback", 100)),
        horizon=int(payload.get("horizon", 5)),
        coverage=float(payload.get("coverage", 0.9)),
    )
    print(json.dumps(result))


if __name__ == "__main__":
    try:
        main()
    except Exception as exc:
        print(json.dumps({"error": str(exc)}), file=sys.stderr)
        sys.exit(1)
