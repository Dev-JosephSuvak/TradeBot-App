"""Run: python3 -m unittest discover -s tests -p 'test_price_bounds.py'"""
import importlib.util
from pathlib import Path
import unittest

MODULE = Path(__file__).resolve().parents[1] / "python" / "prediction" / "price_bounds.py"
spec = importlib.util.spec_from_file_location("price_bounds", MODULE)
price_bounds = importlib.util.module_from_spec(spec)
spec.loader.exec_module(price_bounds)


class PriceBoundsTests(unittest.TestCase):
    def test_bounds_contain_last_close_for_flat_history(self):
        rows = [{"high": 100, "low": 100, "close": 100} for _ in range(150)]
        result = price_bounds.estimate(rows, lookback=100, horizon=5)
        self.assertEqual(result["lower_bound"], 100)
        self.assertEqual(result["upper_bound"], 100)
        self.assertEqual(result["observations"], 100)

    def test_does_not_use_last_unfinished_window_as_history(self):
        rows = [{"high": 100, "low": 100, "close": 100} for _ in range(150)]
        rows[-1] = {"high": 10000, "low": 1, "close": 100}
        result = price_bounds.estimate(rows, lookback=100, horizon=5)
        self.assertEqual(result["upper_bound"], 100)
        self.assertEqual(result["lower_bound"], 100)

    def test_rejects_short_history(self):
        with self.assertRaises(ValueError):
            price_bounds.estimate([{"high": 1, "low": 1, "close": 1}] * 10)


if __name__ == "__main__":
    unittest.main()
