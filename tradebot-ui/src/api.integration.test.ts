import assert from 'node:assert/strict';
import { describe, it } from 'node:test';

const baseUrl = process.env.TRADEBOT_FE_API_BASE_URL ?? 'http://localhost:5173';
const tickerInputs = ['APPL', 'AAPL'] as const;

type JsonResponse = {
  status: number;
  body: unknown;
  contentType: string;
};

async function fetchJson(path: string, init?: RequestInit): Promise<JsonResponse> {
  const response = await fetch(`${baseUrl}${path}`, {
    headers: {
      Accept: 'application/json',
      ...(init?.headers ?? {}),
    },
    ...init,
  });

  const contentType = response.headers.get('content-type') ?? '';
  const text = await response.text();

  let body: unknown = text;
  if (contentType.toLowerCase().includes('application/json')) {
    body = text.length > 0 ? JSON.parse(text) : null;
  }

  return {
    status: response.status,
    body,
    contentType,
  };
}

describe('frontend API integration', () => {
  for (const ticker of tickerInputs) {
    it(`GET /api/candles/${ticker} returns candles`, async () => {
      const result = await fetchJson(`/api/candles/${encodeURIComponent(ticker)}?lookbackDays=30`);

      assert.equal(result.status, 200);
      assert.ok(result.contentType.toLowerCase().includes('application/json'));
      assert.ok(Array.isArray(result.body));
      assert.ok(result.body.length > 0);

      const firstCandle = result.body[0] as Record<string, unknown>;
      assert.equal(typeof firstCandle.timestamp, 'string');
      assert.equal(typeof firstCandle.open, 'number');
      assert.equal(typeof firstCandle.close, 'number');
    });

    it(`GET /api/signals/${ticker} returns signal`, async () => {
      const result = await fetchJson(`/api/signals/${encodeURIComponent(ticker)}`);

      assert.equal(result.status, 200);
      assert.ok(result.contentType.toLowerCase().includes('application/json'));
      assert.equal(typeof result.body, 'object');
      assert.ok(result.body !== null);

      const signal = result.body as Record<string, unknown>;
      assert.equal(typeof signal.action, 'string');
      assert.equal(typeof signal.reason, 'string');
      assert.equal(typeof signal.strategy_name, 'string');
    });

    it(`GET /api/indicators/${ticker} returns indicators payload`, async () => {
      const result = await fetchJson(`/api/indicators/${encodeURIComponent(ticker)}?period=1&periodType=days`);

      assert.equal(result.status, 200);
      assert.ok(result.contentType.toLowerCase().includes('application/json'));
      assert.equal(typeof result.body, 'object');
      assert.ok(result.body !== null);

      const indicators = result.body as Record<string, unknown>;
      assert.equal(indicators.ticker, ticker);
      assert.equal(typeof indicators.period, 'number');
      assert.ok(Array.isArray(indicators.periodMetrics));
      assert.ok(Array.isArray(indicators.candles));
      assert.ok((indicators.candles as unknown[]).length > 0);
    });

    it(`POST /api/portfolio/execute accepts ${ticker} and returns result payload`, async () => {
      const result = await fetchJson('/api/portfolio/execute', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify({
          ticker,
          action: 'buy',
          quantity: 1,
          price: 100,
          reason: 'integration_test',
          strategy: 'integration_test',
        }),
      });

      assert.ok([200, 400].includes(result.status));
      assert.ok(result.contentType.toLowerCase().includes('application/json'));
      assert.equal(typeof result.body, 'object');
      assert.ok(result.body !== null);

      const execution = result.body as Record<string, unknown>;
      assert.equal(typeof execution.success, 'boolean');
      assert.equal(typeof execution.message, 'string');
      assert.equal(typeof execution.realized_pnl, 'number');
      assert.equal(typeof execution.unrealized_pnl, 'number');
      assert.equal(typeof execution.final_portfolio, 'object');
    });
  }

  it('GET /api/market-universe/tickers returns grouped tickers', async () => {
    const result = await fetchJson('/api/market-universe/tickers?search=APPL&limitPerCategory=50');

    assert.equal(result.status, 200);
    assert.ok(result.contentType.toLowerCase().includes('application/json'));
    assert.equal(typeof result.body, 'object');
    assert.ok(result.body !== null);

    const universe = result.body as Record<string, unknown>;
    assert.ok(Array.isArray(universe.stocks));
    assert.ok(Array.isArray(universe.etfs));
    assert.ok(Array.isArray(universe.cryptos));
    assert.equal(typeof universe.total_count, 'number');
  });

  it('GET /api/portfolio returns portfolio state', async () => {
    const result = await fetchJson('/api/portfolio');

    assert.equal(result.status, 200);
    assert.ok(result.contentType.toLowerCase().includes('application/json'));
    assert.equal(typeof result.body, 'object');
    assert.ok(result.body !== null);

    const portfolio = result.body as Record<string, unknown>;
    assert.equal(typeof portfolio.cash, 'number');
    assert.equal(typeof portfolio.equity, 'number');
  });

  it('GET /api/portfolio/trades returns trades array', async () => {
    const result = await fetchJson('/api/portfolio/trades?limit=10');

    assert.equal(result.status, 200);
    assert.ok(result.contentType.toLowerCase().includes('application/json'));
    assert.ok(Array.isArray(result.body));
  });

  it('GET /api/portfolio/simulation-result returns simulation summary', async () => {
    const result = await fetchJson('/api/portfolio/simulation-result');

    assert.equal(result.status, 200);
    assert.ok(result.contentType.toLowerCase().includes('application/json'));
    assert.equal(typeof result.body, 'object');
    assert.ok(result.body !== null);

    const summary = result.body as Record<string, unknown>;
    assert.ok(Array.isArray(summary.trades));
    assert.equal(typeof summary.final_portfolio, 'object');
  });
});
