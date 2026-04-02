export type CandleDto = {
  timestamp: string;
  open: number;
  high: number;
  low: number;
  close: number;
  volume: number;
};

export type PortfolioStateDto = {
  cash: number;
  position_quantity: number;
  entry_price: number;
  equity: number;
};

export type TradeDto = {
  timestamp: string;
  action: string;
  price: number;
  quantity: number;
  strategy_name: string;
  reason: string;
};

export type PortfolioExecutionResponseDto = {
  success: boolean;
  message: string;
  trade: TradeDto | null;
  final_portfolio: PortfolioStateDto;
  realized_pnl: number;
  unrealized_pnl: number;
};

export type MarketTickerDto = {
  ticker: string;
  name: string;
  asset_class: string;
  exchange: string;
  provider: string;
};

export type MarketUniverseDto = {
  as_of_utc: string;
  stocks: MarketTickerDto[];
  etfs: MarketTickerDto[];
  cryptos: MarketTickerDto[];
  total_count: number;
};

export type TradeExecutionRequest = {
  ticker: string;
  action: 'buy' | 'sell';
  quantity: number;
  price: number;
  reason?: string;
  strategy?: string;
};

export type ChartMode =
  | 'line'
  | 'candle'
  | 'range-line'
  | 'range-bar'
  | 'heikin-ashi'
  | 'kagi';