import { useCallback, useEffect, useMemo, useState } from 'react';
import TradingChart from './components/TradingChart';
import {
  executeTrade,
  getIndicators,
  getMarketUniverse,
  getPortfolioState,
  getTrades,
} from './api';
import type {
  CandleDto,
  ChartTimeframe,
  IndicatorResponseDto,
  MarketTickerDto,
  PortfolioStateDto,
  TradeDto,
  TradeExecutionRequest,
} from './types';
import { getMarketSessionInfo } from './utils/tradingHours';
import {
  simulateOrderFillTime,
  formatPendingOrder,
  type PendingOrder,
  type OrderType,
} from './utils/orderSimulator';

const timeframeConfig: Record<ChartTimeframe, { label: string; period: number; periodType: string }> = {
  '5m': { label: '5m', period: 5, periodType: 'minutes' },
  '15m': { label: '15m', period: 15, periodType: 'minutes' },
  '1h': { label: '1h', period: 1, periodType: 'hours' },
  '1d': { label: 'Day', period: 1, periodType: 'days' },
  '1w': { label: 'Week', period: 1, periodType: 'weeks' },
  '1mo': { label: 'Month', period: 1, periodType: 'months' },
  '1y': { label: 'Year', period: 1, periodType: 'years' },
  all: { label: 'All', period: 1, periodType: 'all' },
};

function App() {
  const tradeTypeOptions: Array<{ value: OrderType; label: string }> = [
    { value: 'market', label: 'Market' },
    { value: 'limit', label: 'Limit' },
    { value: 'stop-loss', label: 'Stop Loss' },
    { value: 'take-profit', label: 'Take Profit' },
    { value: 'trailing-stop', label: 'Trailing Stop' },
  ];

  const [ticker, setTicker] = useState('AAPL');
  const [timeframe, setTimeframe] = useState<ChartTimeframe>('1d');
  const [candles, setCandles] = useState<CandleDto[]>([]);
  const [portfolio, setPortfolio] = useState<PortfolioStateDto | null>(null);
  const [trades, setTrades] = useState<TradeDto[]>([]);
  const [universe, setUniverse] = useState<MarketTickerDto[]>([]);
  const [indicators, setIndicators] = useState<IndicatorResponseDto | null>(null);

  const [loadingChart, setLoadingChart] = useState(false);
  const [loadingPortfolio, setLoadingPortfolio] = useState(false);
  const [loadingUniverse, setLoadingUniverse] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const [tradeAction, setTradeAction] = useState<'buy' | 'sell'>('buy');
  const [tradeType, setTradeType] = useState<OrderType>('market');
  const [tradeQuantity, setTradeQuantity] = useState(1);
  const [tradePrice, setTradePrice] = useState(0);
  const [pendingOrders, setPendingOrders] = useState<PendingOrder[]>([]);

  const marketSession = getMarketSessionInfo();
  const marketOpen = marketSession.isOpen;
  const selectedTimeframe = timeframeConfig[timeframe];

  const refreshPortfolio = useCallback(async () => {
    setLoadingPortfolio(true);
    try {
      const [portfolioState, tradeHistory] = await Promise.all([
        getPortfolioState(),
        getTrades(100),
      ]);
      setPortfolio(portfolioState);
      setTrades(tradeHistory);
    } finally {
      setLoadingPortfolio(false);
    }
  }, []);

  const refreshIndicators = useCallback(async () => {
    if (!ticker) return;
    setLoadingChart(true);
    setError(null);
    try {
      const response = await getIndicators(ticker, {
        period: selectedTimeframe.period,
        periodType: selectedTimeframe.periodType,
        shortWindow: 12,
        longWindow: 26,
      });

      setIndicators(response);
      setCandles(response.candles);
      if (response.candles.length > 0) {
        setTradePrice(response.candles[response.candles.length - 1].close);
      }
    } catch (ex) {
      setError(ex instanceof Error ? ex.message : 'Failed to refresh chart data');
    } finally {
      setLoadingChart(false);
    }
  }, [selectedTimeframe.period, selectedTimeframe.periodType, ticker]);

  const refreshUniverse = useCallback(async () => {
    setLoadingUniverse(true);
    try {
      const data = await getMarketUniverse('', 200);
      const flattened = [...data.stocks, ...data.etfs, ...data.cryptos];
      setUniverse(flattened);
    } finally {
      setLoadingUniverse(false);
    }
  }, []);

  // Auto-refresh every 30 seconds
  useEffect(() => {
    void refreshIndicators();

    const interval = setInterval(() => {
      void refreshIndicators();
    }, 30000);

    return () => clearInterval(interval);
  }, [refreshIndicators]);

  useEffect(() => {
    void refreshPortfolio();
    void refreshUniverse();
  }, [refreshPortfolio, refreshUniverse]);

  // Process pending orders: simulate fill times
  useEffect(() => {
    const timers: NodeJS.Timeout[] = [];

    pendingOrders.forEach((order) => {
      if (order.status === 'filled') return;

      if (!order.estimatedFillTime) {
        // Calculate fill time based on simulation
        const currentPrice = candles.length > 0 ? candles[candles.length - 1].close : 0;
        const resistanceHigh = indicators?.avgHighRSI ?? 70;
        const resistanceLow = indicators?.avgLowRSI ?? 30;

        const { fillTimeMs, status } = simulateOrderFillTime(
          order.orderType,
          currentPrice,
          order.price,
          order.action,
          resistanceLow,
          resistanceHigh,
        );

        const filled = status === 'filled';
        const fillTime = filled ? new Date(Date.now() + fillTimeMs) : undefined;

        setPendingOrders((prev) =>
          prev.map((o) => (o.id === order.id ? { ...o, estimatedFillTime: fillTime } : o)),
        );

        // Set timer to mark as filled if applicable
        if (filled) {
          const timer = setTimeout(() => {
            setPendingOrders((prev) =>
              prev.map((o) => (o.id === order.id ? { ...o, status: 'filled' } : o)),
            );
          }, fillTimeMs);

          timers.push(timer);
        }
      }
    });

    return () => timers.forEach(clearTimeout);
  }, [pendingOrders, candles, indicators]);

  const execute = async () => {
    if (!marketOpen) {
      setError(`Market is closed. ${marketSession.message}`);
      return;
    }

    setError(null);

    const request: TradeExecutionRequest = {
      ticker,
      action: tradeAction,
      quantity: tradeQuantity,
      price: tradePrice,
      reason: tradeType,
      strategy: 'ManualUi',
    };

    try {
      await executeTrade(request);

      // Create a pending order for tracking
      const newOrder: PendingOrder = {
        id: `${Date.now()}-${Math.random()}`,
        ticker,
        action: tradeAction,
        quantity: tradeQuantity,
        price: tradePrice,
        orderType: tradeType,
        status: 'pending',
        reason: tradeType,
        strategy: 'ManualUi',
        createdAt: new Date(),
      };

      setPendingOrders((prev) => [...prev, newOrder]);

      await refreshPortfolio();
      await refreshIndicators();
    } catch (exception) {
      setError(exception instanceof Error ? exception.message : 'Trade execution failed');
    }
  };

  const summaryCards = useMemo(() => {
    if (!portfolio) {
      return [];
    }

    return [
      { label: 'Cash', value: portfolio.cash.toFixed(2) },
      { label: 'Position Qty', value: portfolio.position_quantity.toFixed(4) },
      { label: 'Entry Price', value: portfolio.entry_price.toFixed(4) },
      { label: 'Equity', value: portfolio.equity.toFixed(2) },
    ];
  }, [portfolio]);

  const indicatorCards = useMemo(() => {
    if (!indicators) {
      return [];
    }

    return [
      { label: 'Timeframe', value: timeframeConfig[timeframe].label },
      { label: 'RSI', value: indicators.rsi.toFixed(2) },
      { label: 'Avg High RSI', value: indicators.avgHighRSI.toFixed(2) },
      { label: 'Avg Low RSI', value: indicators.avgLowRSI.toFixed(2) },
      { label: 'Trend', value: indicators.trendDirection },
      { label: 'Crossover', value: indicators.crossover.toFixed(4) },
      { label: 'Series', value: indicators.selectedPeriodKey },
    ];
  }, [indicators, timeframe]);

  return (
    <main className="app-shell">
      <header className="top-bar">
        <div>
          <h1>TradeBot</h1>
          <p>Real-time market data, live indicators, and paper trading with order simulation.</p>
          <span style={{ marginLeft: 'auto' }}>
            <span
              style={{
                color: marketOpen ? '#22c55e' : '#f97316',
              }}
            >
              ● {marketSession.message}
            </span>
          </span>
        </div>
      </header>

      {error ? <section className="error-banner">{error}</section> : null}

      {/* Portfolio & Trade History Split Box */}
      <section className="panel trade-grid" style={{ marginBottom: '1.5rem' }}>
        {/* Portfolio Summary */}
        <div>
          <div className="panel-header">
            <h2>Portfolio Summary</h2>
            <button type="button" onClick={() => void refreshPortfolio()} disabled={loadingPortfolio}>
              {loadingPortfolio ? 'Refreshing…' : 'Refresh'}
            </button>
          </div>
          <div className="cards-grid">
            {summaryCards.map((card) => (
              <article className="value-card" key={card.label}>
                <span>{card.label}</span>
                <strong>{card.value}</strong>
              </article>
            ))}
          </div>
        </div>

        {/* Trade History */}
        <div className="top-split-right">
          <div className="top-split-history">
            <h2>Trade History</h2>
            <div className="table-wrap">
              {pendingOrders.length > 0 && (
                <>
                  <h3>Pending Orders</h3>
                  <div style={{ marginBottom: '1rem' }}>
                    {pendingOrders
                      .filter((o) => o.status === 'pending')
                      .map((order) => (
                        <div key={order.id} style={{ padding: '0.5rem', background: '#1f2937', marginBottom: '0.5rem', borderRadius: '0.25rem' }}>
                          {formatPendingOrder(order)}
                        </div>
                      ))}
                  </div>
                </>
              )}

              <h3>Executed Trades</h3>
              <table>
                <thead>
                  <tr>
                    <th>Timestamp</th>
                    <th>Action</th>
                    <th>Qty</th>
                    <th>Price</th>
                    <th>Strategy</th>
                    <th>Reason</th>
                  </tr>
                </thead>
                <tbody>
                  {trades.length === 0 ? (
                    <tr>
                      <td colSpan={6}>No trades yet.</td>
                    </tr>
                  ) : (
                    trades.map((trade) => (
                      <tr key={`${trade.timestamp}-${trade.action}-${trade.price}`}>
                        <td>{new Date(trade.timestamp).toLocaleString()}</td>
                        <td>{trade.action}</td>
                        <td>{trade.quantity.toFixed(4)}</td>
                        <td>{trade.price.toFixed(4)}</td>
                        <td>{trade.strategy_name}</td>
                        <td>{trade.reason}</td>
                      </tr>
                    ))
                  )}
                </tbody>
              </table>
            </div>
          </div>

          <div className="top-split-execute">
            <h2>Execute Trade</h2>
            <div className="form-grid">
              <div className="control-group">
                <label htmlFor="trade-action">Action</label>
                <select
                  id="trade-action"
                  value={tradeAction}
                  onChange={(event) => setTradeAction(event.target.value as 'buy' | 'sell')}
                >
                  <option value="buy">Buy</option>
                  <option value="sell">Sell</option>
                </select>
              </div>

              <div className="control-group">
                <label htmlFor="trade-type">Order Type</label>
                <select
                  id="trade-type"
                  value={tradeType}
                  onChange={(event) => setTradeType(event.target.value as OrderType)}
                >
                  {tradeTypeOptions.map((option) => (
                    <option key={option.value} value={option.value}>
                      {option.label}
                    </option>
                  ))}
                </select>
              </div>

              <div className="control-group">
                <label htmlFor="trade-qty">Quantity</label>
                <input
                  id="trade-qty"
                  type="number"
                  min="0"
                  step="0.0001"
                  value={tradeQuantity}
                  onChange={(event) => setTradeQuantity(Number(event.target.value))}
                />
              </div>

              <div className="control-group">
                <label htmlFor="trade-price">Price</label>
                <input
                  id="trade-price"
                  type="number"
                  min="0"
                  step="0.0001"
                  value={tradePrice}
                  onChange={(event) => setTradePrice(Number(event.target.value))}
                />
              </div>
            </div>
            <button
              type="button"
              onClick={() => void execute()}
              disabled={!marketOpen || loadingChart}
              style={{ opacity: !marketOpen ? 0.5 : 1, cursor: !marketOpen ? 'not-allowed' : 'pointer' }}
            >
              {!marketOpen ? 'Market Closed' : 'Execute'}
            </button>
          </div>
        </div>
      </section>

      <section className="panel controls-grid">
        <div className="control-group">
          <select id="ticker" value={ticker} onChange={(event) => setTicker(event.target.value)}>
            {universe.length === 0 ? <option value="AAPL">AAPL</option> : null}
            {universe.map((item) => (
              <option key={`${item.asset_class}-${item.ticker}`} value={item.ticker}>
                {item.ticker} — {item.asset_class}
              </option>
            ))}
          </select>
        </div>
      </section>

      {/* Chart - timeframe bounded with overlays */}
      <TradingChart
        candles={candles}
        timeframe={timeframe}
        onTimeframeChange={setTimeframe}
        indicator={indicators}
        portfolio={portfolio}
        isLoading={loadingChart}
      />

      {/* Market Indicators Panel */}
      <section className="panel">
        <h2>Market Indicators</h2>
        <div className="cards-grid">
          {indicatorCards.map((card) => (
            <article className="value-card" key={card.label}>
              <span>{card.label}</span>
              <strong>{card.value}</strong>
            </article>
          ))}
        </div>
      </section>

    </main>
  );
}

export default App;