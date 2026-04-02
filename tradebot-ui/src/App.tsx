import { useCallback, useEffect, useMemo, useState } from 'react';
import TradingChart from './components/TradingChart';
import {
  executeTrade,
  getCandles,
  getMarketUniverse,
  getPortfolioState,
  getTrades,
} from './api';
import type {
  CandleDto,
  ChartMode,
  MarketTickerDto,
  PortfolioStateDto,
  TradeDto,
  TradeExecutionRequest,
} from './types';

const chartModes: ChartMode[] = [
  'line',
  'candle',
  'range-line',
  'range-bar',
  'heikin-ashi',
  'kagi',
];

const lookbackOptions = [30, 60, 90, 180, 365];

const modeLabels: Record<ChartMode, string> = {
  line: 'Line',
  candle: 'Candle',
  'range-line': 'Range Line',
  'range-bar': 'Range Bar',
  'heikin-ashi': 'Heikin Ashi',
  kagi: 'Kagi',
};

function App() {
  const [ticker, setTicker] = useState('AAPL');
  const [lookbackDays, setLookbackDays] = useState(60);
  const [mode, setMode] = useState<ChartMode>('line');

  const [candles, setCandles] = useState<CandleDto[]>([]);
  const [portfolio, setPortfolio] = useState<PortfolioStateDto | null>(null);
  const [trades, setTrades] = useState<TradeDto[]>([]);
  const [universe, setUniverse] = useState<MarketTickerDto[]>([]);

  const [loadingChart, setLoadingChart] = useState(false);
  const [loadingPortfolio, setLoadingPortfolio] = useState(false);
  const [loadingUniverse, setLoadingUniverse] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const [tradeAction, setTradeAction] = useState<'buy' | 'sell'>('buy');
  const [tradeQuantity, setTradeQuantity] = useState(1);
  const [tradePrice, setTradePrice] = useState(0);
  const [tradeReason, setTradeReason] = useState('UI execution');

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

  const refreshChart = useCallback(async () => {
    setLoadingChart(true);
    setError(null);
    try {
      const nextCandles = await getCandles(ticker, lookbackDays);
      setCandles(nextCandles);
      if (nextCandles.length > 0) {
        setTradePrice(nextCandles[nextCandles.length - 1].close);
      }
    } catch (exception) {
      setError(exception instanceof Error ? exception.message : 'Failed to load candles');
    } finally {
      setLoadingChart(false);
    }
  }, [lookbackDays, ticker]);

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

  useEffect(() => {
    void refreshChart();
  }, [refreshChart]);

  useEffect(() => {
    void refreshPortfolio();
    void refreshUniverse();
  }, [refreshPortfolio, refreshUniverse]);

  const execute = async () => {
    setError(null);

    const request: TradeExecutionRequest = {
      ticker,
      action: tradeAction,
      quantity: tradeQuantity,
      price: tradePrice,
      reason: tradeReason,
      strategy: 'ManualUi',
    };

    try {
      await executeTrade(request);
      await refreshPortfolio();
      await refreshChart();
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
      { label: 'Position Quantity', value: portfolio.position_quantity.toFixed(4) },
      { label: 'Entry Price', value: portfolio.entry_price.toFixed(4) },
      { label: 'Equity', value: portfolio.equity.toFixed(2) },
    ];
  }, [portfolio]);

  return (
    <main className="app-shell">
      <header className="top-bar">
        <div>
          <h1>TradeBot Portfolio UI</h1>
          <p>Chart market data, execute paper trades, and review portfolio state.</p>
        </div>
      </header>

      <section className="panel controls-grid">
        <div className="control-group">
          <label htmlFor="ticker">Ticker</label>
          <select id="ticker" value={ticker} onChange={(event) => setTicker(event.target.value)}>
            {universe.length === 0 ? <option value="AAPL">AAPL</option> : null}
            {universe.map((item) => (
              <option key={`${item.asset_class}-${item.ticker}`} value={item.ticker}>
                {item.ticker} — {item.asset_class}
              </option>
            ))}
          </select>
        </div>

        <div className="control-group">
          <label htmlFor="lookback">Lookback Days</label>
          <select
            id="lookback"
            value={lookbackDays}
            onChange={(event) => setLookbackDays(Number(event.target.value))}
          >
            {lookbackOptions.map((option) => (
              <option key={option} value={option}>
                {option}
              </option>
            ))}
          </select>
        </div>

        <div className="control-group">
          <label htmlFor="mode">Chart View</label>
          <select id="mode" value={mode} onChange={(event) => setMode(event.target.value as ChartMode)}>
            {chartModes.map((chartMode) => (
              <option key={chartMode} value={chartMode}>
                {modeLabels[chartMode]}
              </option>
            ))}
          </select>
        </div>

        <div className="control-group button-row">
          <button type="button" onClick={() => void refreshChart()} disabled={loadingChart}>
            {loadingChart ? 'Refreshing…' : 'Refresh Chart'}
          </button>
          <button type="button" onClick={() => void refreshUniverse()} disabled={loadingUniverse}>
            {loadingUniverse ? 'Loading…' : 'Refresh Tickers'}
          </button>
        </div>
      </section>

      {error ? <section className="error-banner">{error}</section> : null}

      <TradingChart candles={candles} mode={mode} />

      <section className="panel">
        <div className="panel-header">
          <h2>Portfolio Summary</h2>
          <button type="button" onClick={() => void refreshPortfolio()} disabled={loadingPortfolio}>
            {loadingPortfolio ? 'Refreshing…' : 'Refresh Portfolio'}
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
      </section>

      <section className="panel trade-grid">
        <div>
          <h2>Execute Paper Trade</h2>
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

            <div className="control-group">
              <label htmlFor="trade-reason">Reason</label>
              <input
                id="trade-reason"
                type="text"
                value={tradeReason}
                onChange={(event) => setTradeReason(event.target.value)}
              />
            </div>
          </div>
          <button type="button" onClick={() => void execute()}>
            Execute
          </button>
        </div>

        <div>
          <h2>Executed Trades</h2>
          <div className="table-wrap">
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
      </section>
    </main>
  );
}

export default App;