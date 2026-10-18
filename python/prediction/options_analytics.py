"""Deterministic European-option analytics and adaptive exit policy.

Research / paper-trading only. Prices are per underlying unit; multiply by the
contract multiplier for dollar P&L. American options require another model.
"""
from dataclasses import dataclass
from math import erf, exp, isfinite, log, sqrt
from statistics import NormalDist


def _cdf(x):
    return (1 + erf(x / sqrt(2))) / 2


def _pdf(x):
    return exp(-x * x / 2) / sqrt(2 * 3.141592653589793)


def black_scholes(spot, strike, years, rate, volatility, right, dividend_yield=0.0):
    """European option theoretical value and per-unit Greeks.

    Theta is per calendar day; vega and rho are per 1.00 absolute change
    (multiply by .01 for a 1 percentage-point IV/rate move).
    """
    if not all(isfinite(v) for v in (spot, strike, years, rate, volatility, dividend_yield)):
        raise ValueError("Nonfinite pricing input")
    if spot <= 0 or strike <= 0 or years <= 0 or volatility <= 0:
        raise ValueError("spot, strike, years and volatility must be positive")
    if right not in ("call", "put"):
        raise ValueError("right must be call or put")
    d1 = (log(spot / strike) + (rate - dividend_yield + .5 * volatility**2) * years) / (volatility * sqrt(years))
    d2 = d1 - volatility * sqrt(years)
    dq, dr = exp(-dividend_yield * years), exp(-rate * years)
    if right == "call":
        price = spot*dq*_cdf(d1) - strike*dr*_cdf(d2)
        delta = dq*_cdf(d1)
        theta = (-spot*dq*_pdf(d1)*volatility/(2*sqrt(years))
                 - rate*strike*dr*_cdf(d2) + dividend_yield*spot*dq*_cdf(d1))
        rho = strike*years*dr*_cdf(d2)
    else:
        price = strike*dr*_cdf(-d2) - spot*dq*_cdf(-d1)
        delta = dq*(_cdf(d1)-1)
        theta = (-spot*dq*_pdf(d1)*volatility/(2*sqrt(years))
                 + rate*strike*dr*_cdf(-d2) - dividend_yield*spot*dq*_cdf(-d1))
        rho = -strike*years*dr*_cdf(-d2)
    gamma = dq*_pdf(d1)/(spot*volatility*sqrt(years))
    vega = spot*dq*_pdf(d1)*sqrt(years)
    return dict(price=price, delta=delta, gamma=gamma, theta_day=theta/365,
                vega=vega, rho=rho)


def implied_volatility(market_price, spot, strike, years, rate, right, dividend_yield=0.0):
    """Bisection IV with no-arbitrage European price bounds."""
    if not isfinite(market_price) or market_price < 0:
        raise ValueError("Invalid market price")
    discount_spot = spot*exp(-dividend_yield*years)
    discount_strike = strike*exp(-rate*years)
    lower = max(0, discount_spot-discount_strike) if right == "call" else max(0, discount_strike-discount_spot)
    upper = discount_spot if right == "call" else discount_strike
    if market_price <= lower or market_price >= upper:
        raise ValueError("Market price outside strictly invertible European bounds")
    lo, hi = 1e-6, 5.0
    for _ in range(100):
        mid = (lo+hi)/2
        p = black_scholes(spot, strike, years, rate, mid, right, dividend_yield)["price"]
        if p < market_price:
            lo = mid
        else:
            hi = mid
    return (lo+hi)/2


@dataclass(frozen=True)
class ExitInputs:
    entry_price: float
    bid: float
    ask: float
    peak_bid: float
    days_to_expiry: float
    delta: float
    theta_per_day: float
    predicted_underlying_lower: float
    predicted_underlying_upper: float
    underlying_spot: float
    right: str
    spread_limit_fraction: float = 0.20
    stop_loss_fraction: float = 0.30
    profit_target_fraction: float = 0.50
    trailing_drawdown_fraction: float = 0.20
    min_days_to_expiry: float = 2.0


def exit_decision(p: ExitInputs):
    """Long-option exit triggers. Assumes executable liquidation at bid.

    Stop is a *signal*, not a guaranteed fill or capped loss.
    Exit decisions never place orders. Hard loss, expiry, profit and trailing
    checks take priority over a forecast; wide spreads flag manual review.
    """
    nums = (p.entry_price,p.bid,p.ask,p.peak_bid,p.days_to_expiry,p.delta,
            p.theta_per_day,p.predicted_underlying_lower,
            p.predicted_underlying_upper,p.underlying_spot)
    if not all(isfinite(x) for x in nums):
        raise ValueError("Nonfinite exit input")
    if p.entry_price <= 0 or p.bid < 0 or p.ask < p.bid or p.peak_bid < p.bid or p.underlying_spot <= 0:
        raise ValueError("Invalid option quote or position")
    if p.right not in ("call","put"):
        raise ValueError("Invalid right")
    if not (0 < p.stop_loss_fraction < 1 and p.profit_target_fraction > 0
            and 0 < p.trailing_drawdown_fraction < 1):
        raise ValueError("Invalid risk fractions")
    pnl_fraction = p.bid/p.entry_price-1
    spread_fraction = (p.ask-p.bid)/max((p.ask+p.bid)/2, 1e-9)
    forecast_favorable = (p.predicted_underlying_upper > p.underlying_spot if p.right == "call"
                          else p.predicted_underlying_lower < p.underlying_spot)
    # Conservative deterministic priority; broker risk checks still required.
    if pnl_fraction <= -p.stop_loss_fraction:
        reason = "STOP_LOSS"
    elif p.days_to_expiry <= p.min_days_to_expiry:
        reason = "EXPIRY_EXIT"
    elif pnl_fraction >= p.profit_target_fraction:
        reason = "TAKE_PROFIT"
    elif p.peak_bid >= p.entry_price*(1+p.profit_target_fraction/2) and p.bid <= p.peak_bid*(1-p.trailing_drawdown_fraction):
        reason = "TRAILING_EXIT"
    elif not forecast_favorable and p.theta_per_day < 0:
        reason = "THESIS_INVALIDATED"
    elif spread_fraction > p.spread_limit_fraction:
        reason = "WIDE_SPREAD_REVIEW"
    else:
        reason = "HOLD"
    return {"action": "EXIT_SIGNAL" if reason in ("STOP_LOSS","EXPIRY_EXIT","TAKE_PROFIT","TRAILING_EXIT","THESIS_INVALIDATED") else "REVIEW" if reason=="WIDE_SPREAD_REVIEW" else "HOLD",
            "reason":reason,"unrealized_return":round(pnl_fraction,6),
            "spread_fraction":round(spread_fraction,6),
            "estimated_liquidation_price":p.bid,
            "warning":"Stop signals do not guarantee execution price or prevent gaps."}
