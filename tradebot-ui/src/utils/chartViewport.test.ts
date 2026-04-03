import assert from 'node:assert/strict';
import { describe, it } from 'node:test';
import type { CandleDto } from '../types';
import {
  buildLogicalViewportRange,
  clampCandlesByTimeframe,
  computeResistanceBounds,
  downsampleCandlesForRender,
  isIntradayTimeframe,
  sanitizeCandlesForChart,
} from './chartViewport';

function createCandles(count: number, startIso: string, stepMinutes: number, basePrice = 100): CandleDto[] {
  const start = new Date(startIso).getTime();

  return Array.from({ length: count }, (_, index) => {
    const timestamp = new Date(start + index * stepMinutes * 60_000).toISOString();
    const open = basePrice + index;
    const close = open + 0.5;

    return {
      timestamp,
      open,
      high: close + 1,
      low: open - 1,
      close,
      volume: 1000 + index,
    };
  });
}

describe('chartViewport', () => {
  it('keeps 5m timeframe scoped to last 30 minutes of available data', () => {
    const candles = createCandles(24, '2026-04-03T13:00:00.000Z', 5);

    const visible = clampCandlesByTimeframe(candles, '5m');

    assert.equal(visible.length, 7);
    assert.equal(visible[0]?.timestamp, candles[17]?.timestamp);
    assert.equal(visible.at(-1)?.timestamp, candles.at(-1)?.timestamp);
  });

  it('keeps week timeframe limited to bars inside one calendar week window', () => {
    const candles = createCandles(14, '2026-03-20T13:30:00.000Z', 24 * 60);

    const visible = clampCandlesByTimeframe(candles, '1w');

    assert.equal(visible.length <= 8, true);
    assert.equal(
      new Date(visible[0]!.timestamp).getTime() >= new Date(candles.at(-1)!.timestamp).getTime() - 7 * 24 * 60 * 60 * 1000,
      true,
    );
  });

  it('computes resistance bounds from visible period highs and lows', () => {
    const candles: CandleDto[] = [
      { timestamp: '2026-04-01T13:30:00.000Z', open: 100, high: 110, low: 90, close: 105, volume: 1000 },
      { timestamp: '2026-04-02T13:30:00.000Z', open: 101, high: 114, low: 91, close: 106, volume: 1001 },
      { timestamp: '2026-04-03T13:30:00.000Z', open: 102, high: 118, low: 92, close: 107, volume: 1002 },
      { timestamp: '2026-04-04T13:30:00.000Z', open: 103, high: 122, low: 93, close: 108, volume: 1003 },
    ];

    const bounds = computeResistanceBounds(candles);

    assert.equal(bounds.resistanceHigh, 122);
    assert.equal(bounds.resistanceLow, 90);
  });

  it('marks only sub-day selections as intraday timeframes', () => {
    assert.equal(isIntradayTimeframe('5m'), true);
    assert.equal(isIntradayTimeframe('15m'), true);
    assert.equal(isIntradayTimeframe('1h'), true);
    assert.equal(isIntradayTimeframe('1d'), false);
    assert.equal(isIntradayTimeframe('1w'), false);
  });

  it('builds a logical viewport range that keeps data centered with padding', () => {
    assert.deepEqual(buildLogicalViewportRange(10), { from: -0.5, to: 10.5 });
    assert.deepEqual(buildLogicalViewportRange(40), { from: -2, to: 42 });
  });

  it('sanitizes invalid and duplicate chart candles before rendering', () => {
    const candles: CandleDto[] = [
      { timestamp: '2026-04-03T13:30:00.000Z', open: 100, high: 103, low: 99, close: 102, volume: 1000 },
      { timestamp: 'not-a-date', open: 101, high: 104, low: 100, close: 103, volume: 1001 },
      { timestamp: '2026-04-03T13:30:00.500Z', open: 102, high: 100, low: 105, close: 104, volume: Number.NaN },
      { timestamp: '2026-04-03T13:31:00.000Z', open: Number.POSITIVE_INFINITY, high: 105, low: 101, close: 104, volume: 1002 },
      { timestamp: '2026-04-03T13:32:00.000Z', open: 104, high: 106, low: 103, close: 105, volume: 1003 },
    ];

    const sanitized = sanitizeCandlesForChart(candles);

    assert.equal(sanitized.length, 2);
    assert.deepEqual(sanitized.map((candle) => candle.timestamp), [
      '2026-04-03T13:30:00.500Z',
      '2026-04-03T13:32:00.000Z',
    ]);
    assert.equal(sanitized[0]?.high, 104);
    assert.equal(sanitized[0]?.low, 102);
    assert.equal(sanitized[0]?.volume, 0);
  });

  it('handles large candle sets without changing chronological order', () => {
    const candles = createCandles(10000, '2026-01-02T14:30:00.000Z', 1);

    const sanitized = sanitizeCandlesForChart(candles);
    const visible = clampCandlesByTimeframe(sanitized, 'all');

    assert.equal(visible.length, 10000);
    assert.equal(visible[0]?.timestamp, candles[0]?.timestamp);
    assert.equal(visible.at(-1)?.timestamp, candles.at(-1)?.timestamp);
  });

  it('downsamples very large chart payloads and preserves the latest candle', () => {
    const candles = createCandles(50000, '2026-01-02T14:30:00.000Z', 1);

    const reduced = downsampleCandlesForRender(candles, 12000);

    assert.equal(reduced.length <= 12001, true);
    assert.equal(reduced[0]?.timestamp, candles[0]?.timestamp);
    assert.equal(reduced.at(-1)?.timestamp, candles.at(-1)?.timestamp);
  });
});