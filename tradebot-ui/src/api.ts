import type {
  CandleDto,
  MarketUniverseDto,
  PortfolioExecutionResponseDto,
  PortfolioStateDto,
  TradeDto,
  TradeExecutionRequest,
} from './types';

async function fetchJson<T>(url: string, init?: RequestInit): Promise<T> {
  const response = await fetch(url, {
    headers: {
      'Content-Type': 'application/json',
    },
    ...init,
  });

  if (!response.ok) {
    const text = await response.text();
    throw new Error(text || `Request failed (${response.status})`);
  }

  return (await response.json()) as T;
}

export function getCandles(ticker: string, lookbackDays: number): Promise<CandleDto[]> {
  const query = new URLSearchParams({ lookbackDays: String(lookbackDays) }).toString();
  return fetchJson<CandleDto[]>(`/api/candles/${encodeURIComponent(ticker)}?${query}`);
}

export function getMarketUniverse(search = '', limitPerCategory = 200): Promise<MarketUniverseDto> {
  const query = new URLSearchParams({
    search,
    limitPerCategory: String(limitPerCategory),
  }).toString();

  return fetchJson<MarketUniverseDto>(`/api/market-universe/tickers?${query}`);
}

export function getPortfolioState(): Promise<PortfolioStateDto> {
  return fetchJson<PortfolioStateDto>('/api/portfolio');
}

export function getTrades(limit = 100): Promise<TradeDto[]> {
  return fetchJson<TradeDto[]>(`/api/portfolio/trades?limit=${limit}`);
}

export function executeTrade(request: TradeExecutionRequest): Promise<PortfolioExecutionResponseDto> {
  return fetchJson<PortfolioExecutionResponseDto>('/api/portfolio/execute', {
    method: 'POST',
    body: JSON.stringify(request),
  });
}