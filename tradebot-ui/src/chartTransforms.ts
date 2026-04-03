import type { UTCTimestamp } from 'lightweight-charts';
import type { CandleDto } from './types';

export type PricePoint = { time: UTCTimestamp; value: number };
export type CandlePoint = {
  time: UTCTimestamp;
  open: number;
  high: number;
  low: number;
  close: number;
};
export type HistogramPoint = { time: UTCTimestamp; value: number; color?: string };

export function toUtcTimestamp(iso: string): UTCTimestamp {
  return Math.floor(new Date(iso).getTime() / 1000) as UTCTimestamp;
}

export function toLineSeries(candles: CandleDto[]): PricePoint[] {
  return candles.map((candle) => ({
    time: toUtcTimestamp(candle.timestamp),
    value: candle.close,
  }));
}

export function toCandlestickSeries(candles: CandleDto[]): CandlePoint[] {
  return candles.map((candle) => ({
    time: toUtcTimestamp(candle.timestamp),
    open: candle.open,
    high: candle.high,
    low: candle.low,
    close: candle.close,
  }));
}

export function toRangeLineSeries(candles: CandleDto[]): PricePoint[] {
  return candles.map((candle) => ({
    time: toUtcTimestamp(candle.timestamp),
    value: candle.high - candle.low,
  }));
}

export function toRangeBarSeries(candles: CandleDto[]): HistogramPoint[] {
  return candles.map((candle) => {
    const range = candle.high - candle.low;
    const bullish = candle.close >= candle.open;

    return {
      time: toUtcTimestamp(candle.timestamp),
      value: range,
      color: bullish ? '#21c17a' : '#f14f4f',
    };
  });
}

export function toHeikinAshiSeries(candles: CandleDto[]): CandlePoint[] {
  if (candles.length === 0) {
    return [];
  }

  const result: CandlePoint[] = [];
  let previousHaOpen = (candles[0].open + candles[0].close) / 2;
  let previousHaClose = (candles[0].open + candles[0].high + candles[0].low + candles[0].close) / 4;

  for (let index = 0; index < candles.length; index++) {
    const candle = candles[index];
    const haClose = (candle.open + candle.high + candle.low + candle.close) / 4;
    const haOpen = index === 0 ? previousHaOpen : (previousHaOpen + previousHaClose) / 2;
    const haHigh = Math.max(candle.high, haOpen, haClose);
    const haLow = Math.min(candle.low, haOpen, haClose);

    result.push({
      time: toUtcTimestamp(candle.timestamp),
      open: haOpen,
      high: haHigh,
      low: haLow,
      close: haClose,
    });

    previousHaOpen = haOpen;
    previousHaClose = haClose;
  }

  return result;
}

export function toKagiSeries(candles: CandleDto[], reversalPercent = 0.02): PricePoint[] {
  if (candles.length === 0) {
    return [];
  }

  const points: PricePoint[] = [];
  let direction: 'up' | 'down' = candles.length > 1 && candles[1].close >= candles[0].close ? 'up' : 'down';
  let pivot = candles[0].close;

  points.push({ time: toUtcTimestamp(candles[0].timestamp), value: pivot });

  for (let index = 1; index < candles.length; index++) {
    const candle = candles[index];
    const price = candle.close;
    const reversalAmount = Math.max(pivot * reversalPercent, 0.0001);

    if (direction === 'up') {
      if (price >= pivot) {
        pivot = price;
        points.push({ time: toUtcTimestamp(candle.timestamp), value: pivot });
      } else if (pivot - price >= reversalAmount) {
        direction = 'down';
        pivot = price;
        points.push({ time: toUtcTimestamp(candle.timestamp), value: pivot });
      }
    } else {
      if (price <= pivot) {
        pivot = price;
        points.push({ time: toUtcTimestamp(candle.timestamp), value: pivot });
      } else if (price - pivot >= reversalAmount) {
        direction = 'up';
        pivot = price;
        points.push({ time: toUtcTimestamp(candle.timestamp), value: pivot });
      }
    }
  }

  return points;
}