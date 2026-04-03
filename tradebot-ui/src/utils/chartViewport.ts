import type { CandleDto, ChartTimeframe } from '../types';

const timeframeWindowMs: Record<ChartTimeframe, number> = {
  '5m': 30 * 60 * 1000,
  '15m': 24 * 60 * 60 * 1000,
  '1h': 8 * 60 * 60 * 1000,
  '1d': 24 * 60 * 60 * 1000,
  '1w': 7 * 24 * 60 * 60 * 1000,
  '1mo': 31 * 24 * 60 * 60 * 1000,
  '1y': 366 * 24 * 60 * 60 * 1000,
  all: Number.POSITIVE_INFINITY,
};

const easternOffsetFormatter = new Intl.DateTimeFormat('en-US', {
  timeZone: 'America/New_York',
  timeZoneName: 'shortOffset',
  hour: '2-digit',
});

const easternDateFormatter = new Intl.DateTimeFormat('en-US', {
  timeZone: 'America/New_York',
  year: 'numeric',
  month: '2-digit',
  day: '2-digit',
});

function getEasternOffsetMinutes(utcDate: Date): number {
  const timeZoneName = easternOffsetFormatter
    .formatToParts(utcDate)
    .find((value) => value.type === 'timeZoneName')?.value;

  const match = timeZoneName?.match(/GMT([+-])(\d{1,2})(?::(\d{2}))?/);
  if (!match) {
    return 0;
  }

  const sign = match[1] === '-' ? -1 : 1;
  const hours = Number(match[2]);
  const minutes = Number(match[3] ?? '0');
  return sign * (hours * 60 + minutes);
}

function toUtcFromEastern(year: number, month: number, day: number, hour: number, minute: number): number {
  const utcGuess = new Date(Date.UTC(year, month - 1, day, hour, minute, 0));
  const offsetMinutes = getEasternOffsetMinutes(utcGuess);
  return utcGuess.getTime() - offsetMinutes * 60_000;
}

export function getTradingDayStartUtc(latestTimestampMs: number): number {
  const parts = easternDateFormatter.formatToParts(new Date(latestTimestampMs));
  const year = Number(parts.find((value) => value.type === 'year')?.value ?? '1970');
  const month = Number(parts.find((value) => value.type === 'month')?.value ?? '1');
  const day = Number(parts.find((value) => value.type === 'day')?.value ?? '1');

  return toUtcFromEastern(year, month, day, 9, 30);
}

export function isIntradayTimeframe(timeframe: ChartTimeframe): boolean {
  return timeframe === '5m' || timeframe === '15m' || timeframe === '1h';
}

export function sanitizeCandlesForChart(candles: CandleDto[]): CandleDto[] {
  if (candles.length === 0) {
    return [];
  }

  const normalized = candles
    .map((candle) => {
      const timestampMs = new Date(candle.timestamp).getTime();

      if (
        !Number.isFinite(timestampMs)
        || !Number.isFinite(candle.open)
        || !Number.isFinite(candle.high)
        || !Number.isFinite(candle.low)
        || !Number.isFinite(candle.close)
      ) {
        return null;
      }

      const high = Math.max(candle.high, candle.open, candle.close);
      const low = Math.min(candle.low, candle.open, candle.close);

      return {
        timeKey: Math.floor(timestampMs / 1000),
        timestampMs,
        candle: {
          timestamp: new Date(timestampMs).toISOString(),
          open: candle.open,
          high,
          low,
          close: candle.close,
          volume: Number.isFinite(candle.volume) ? candle.volume : 0,
        } satisfies CandleDto,
      };
    })
    .filter((entry): entry is NonNullable<typeof entry> => entry !== null)
    .sort((left, right) => left.timestampMs - right.timestampMs);

  if (normalized.length <= 1) {
    return normalized.map((entry) => entry.candle);
  }

  const deduplicated: CandleDto[] = [];
  let previousTimeKey: number | null = null;

  for (const entry of normalized) {
    if (entry.timeKey === previousTimeKey) {
      deduplicated[deduplicated.length - 1] = entry.candle;
      continue;
    }

    deduplicated.push(entry.candle);
    previousTimeKey = entry.timeKey;
  }

  return deduplicated;
}

export function clampCandlesByTimeframe(candles: CandleDto[], timeframe: ChartTimeframe): CandleDto[] {
  if (candles.length === 0) {
    return candles;
  }

  const latestTimestamp = new Date(candles[candles.length - 1].timestamp).getTime();

  if (timeframe === '15m' || timeframe === '1h') {
    const tradingDayStartUtc = getTradingDayStartUtc(latestTimestamp);
    for (let index = candles.length - 1; index >= 0; index--) {
      if (new Date(candles[index].timestamp).getTime() < tradingDayStartUtc) {
        const sessionStartIndex = Math.min(index + 1, candles.length - 1);
        const sessionCandles = candles.slice(sessionStartIndex);
        if (sessionCandles.length > 0) {
          return sessionCandles;
        }
        break;
      }
    }

    if (new Date(candles[0].timestamp).getTime() >= tradingDayStartUtc) {
      return candles;
    }
  }

  const windowMs = timeframeWindowMs[timeframe];
  if (!Number.isFinite(windowMs)) {
    return candles;
  }

  const lowerBound = latestTimestamp - windowMs;
  let startIndex = candles.length - 1;
  while (startIndex >= 0 && new Date(candles[startIndex].timestamp).getTime() >= lowerBound) {
    startIndex -= 1;
  }

  const filtered = candles.slice(startIndex + 1);

  return filtered.length > 0 ? filtered : candles.slice(-Math.min(20, candles.length));
}

export function downsampleCandlesForRender(candles: CandleDto[], maxBars = 12000): CandleDto[] {
  if (candles.length <= maxBars || maxBars < 2) {
    return candles;
  }

  const stride = Math.ceil(candles.length / maxBars);
  const reduced: CandleDto[] = [];

  for (let index = 0; index < candles.length; index += stride) {
    reduced.push(candles[index]);
  }

  const lastCandle = candles[candles.length - 1];
  if (reduced[reduced.length - 1]?.timestamp !== lastCandle.timestamp) {
    reduced.push(lastCandle);
  }

  return reduced;
}

export function computeResistanceBounds(candles: CandleDto[]): { resistanceHigh: number; resistanceLow: number } {
  if (candles.length === 0) {
    return { resistanceHigh: 0, resistanceLow: 0 };
  }

  const highs = candles.map((candle) => candle.high).sort((left, right) => left - right);
  const lows = candles.map((candle) => candle.low).sort((left, right) => left - right);

  const segmentSize = Math.max(1, Math.floor(candles.length * 0.3));
  const topHighs = highs.slice(-segmentSize);
  const bottomLows = lows.slice(0, segmentSize);

  const resistanceHigh = topHighs.reduce((sum, value) => sum + value, 0) / topHighs.length;
  const resistanceLow = bottomLows.reduce((sum, value) => sum + value, 0) / bottomLows.length;

  return { resistanceHigh, resistanceLow };
}

export function buildLogicalViewportRange(barCount: number): { from: number; to: number } {
  const leftPaddingBars = Math.max(0.5, Math.min(2, barCount * 0.05));
  const rightPaddingBars = Math.max(1.5, Math.min(3, barCount * 0.08));

  return {
    from: -leftPaddingBars,
    to: barCount - 1 + rightPaddingBars,
  };
}