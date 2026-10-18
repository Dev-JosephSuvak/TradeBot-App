"""Unit tests for deterministic option analytics."""
import importlib.util
from pathlib import Path
import unittest

path = Path(__file__).resolve().parents[1] / "python" / "prediction" / "options_analytics.py"
spec = importlib.util.spec_from_file_location("options_analytics", path)
mod = importlib.util.module_from_spec(spec)
import sys
sys.modules[spec.name] = mod
spec.loader.exec_module(mod)


class OptionAnalyticsTests(unittest.TestCase):
    def test_put_call_parity_no_dividends(self):
        from math import exp
        c = mod.black_scholes(100, 100, 0.5, 0.04, 0.25, "call")
        p = mod.black_scholes(100, 100, 0.5, 0.04, 0.25, "put")
        self.assertAlmostEqual(c["price"]-p["price"], 100-100*exp(-0.04*0.5), places=6)
        self.assertGreater(c["delta"], 0)
        self.assertLess(p["delta"], 0)
        self.assertGreater(c["gamma"], 0)

    def test_implied_volatility_roundtrip(self):
        quote = mod.black_scholes(100, 105, 0.25, 0.03, 0.30, "call")["price"]
        self.assertAlmostEqual(mod.implied_volatility(quote, 100, 105, 0.25, 0.03, "call"), 0.30, places=5)

    def test_stop_loss_priority(self):
        p = mod.ExitInputs(2, 1.2, 1.3, 3, 10, 0.5, -0.03, 95, 105, 100, "call")
        self.assertEqual(mod.exit_decision(p)["reason"], "STOP_LOSS")

    def test_profit_target(self):
        p = mod.ExitInputs(2, 3.1, 3.2, 3.2, 10, 0.5, -0.03, 95, 105, 100, "call")
        self.assertEqual(mod.exit_decision(p)["reason"], "TAKE_PROFIT")

    def test_trailing_exit(self):
        p = mod.ExitInputs(2, 2.4, 2.5, 3.1, 10, 0.5, -0.03, 95, 105, 100, "call")
        self.assertEqual(mod.exit_decision(p)["reason"], "TRAILING_EXIT")

    def test_expiry_exit(self):
        p = mod.ExitInputs(2, 2, 2.1, 2.1, 1, 0.5, -0.03, 95, 105, 100, "call")
        self.assertEqual(mod.exit_decision(p)["reason"], "EXPIRY_EXIT")

    def test_wide_spread_review(self):
        p = mod.ExitInputs(2, 2, 3, 2, 10, 0.5, -0.03, 95, 105, 100, "call")
        self.assertEqual(mod.exit_decision(p)["reason"], "WIDE_SPREAD_REVIEW")


if __name__ == "__main__":
    unittest.main()
