import type {
  CandleDto,
  IndicatorResponseDto,
  MarketUniverseDto,
  PortfolioExecutionResponseDto,
  PortfolioStateDto,
  SignalDto,
  SimulationResultDto,
  TradeDto,
  TradeExecutionRequest,
} from './types';

function looksLikeHtml(text: string): boolean {
  const trimmed = text.trimStart().toLowerCase();
  return trimmed.startsWith('<!doctype html') || trimmed.startsWith('<html');
}

function isJsonResponse(contentType: string | null): boolean {
  return (contentType ?? '').toLowerCase().includes('application/json');
}

async function fetchJson<T>(url: string, init?: RequestInit): Promise<T> {
  const hasBody = init?.body !== undefined;
  const headers = new Headers(init?.headers);
  headers.set('Accept', 'application/json');
  if (hasBody && !headers.has('Content-Type')) {
    headers.set('Content-Type', 'application/json');
  }

  const response = await fetch(url, {
    ...init,
    headers,
  });

  const contentType = response.headers.get('content-type');
  const bodyText = await response.text();
  const isJson = isJsonResponse(contentType);

  if (!response.ok) {
    if (isJson) {
      try {
        const payload = JSON.parse(bodyText) as { error?: string; message?: string };
        throw new Error(payload.error ?? payload.message ?? `Request failed (${response.status})`);
      } catch {
        throw new Error(bodyText || `Request failed (${response.status})`);
      }
    }

    throw new Error(bodyText || `Request failed (${response.status})`);
  }

  if (!isJson || looksLikeHtml(bodyText)) {
    throw new Error(
      `Expected JSON API response from ${url}, but received non-JSON content. ` +
        'This usually means the request hit the UI fallback page instead of the API route.',
    );
  }

  try {
    return JSON.parse(bodyText) as T;
  } catch {
    throw new Error(`Invalid JSON response from ${url}.`);
  }
}

export function getCandles(ticker: string, lookbackDays: number): Promise<CandleDto[]> {
  const query = new URLSearchParams({ lookbackDays: String(lookbackDays) }).toString();
  return fetchJson<CandleDto[]>(`/api/candles/${encodeURIComponent(ticker)}?${query}`);
}

export function getSignal(ticker: string): Promise<SignalDto> {
  return fetchJson<SignalDto>(`/api/signals/${encodeURIComponent(ticker)}`);
}

export function getIndicators(
  ticker: string,
  params: {
    period: number;
    periodType: string;
    shortWindow?: number;
    longWindow?: number;
  },
): Promise<IndicatorResponseDto> {
  const query = new URLSearchParams({
    period: String(params.period),
    periodType: params.periodType,
  });

  if (params.shortWindow !== undefined) {
    query.set('shortWindow', String(params.shortWindow));
  }

  if (params.longWindow !== undefined) {
    query.set('longWindow', String(params.longWindow));
  }

  return fetchJson<IndicatorResponseDto>(`/api/indicators/${encodeURIComponent(ticker)}?${query.toString()}`);
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

export function getSimulationResult(): Promise<SimulationResultDto> {
  return fetchJson<SimulationResultDto>('/api/portfolio/simulation-result');
}

export function executeTrade(request: TradeExecutionRequest): Promise<PortfolioExecutionResponseDto> {
  return fetchJson<PortfolioExecutionResponseDto>('/api/portfolio/execute', {
    method: 'POST',
    body: JSON.stringify(request),
  });
}