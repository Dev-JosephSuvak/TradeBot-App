import { useEffect, useMemo, useRef, useState } from 'react';
import {
  ColorType,
  LineStyle,
  createChart,
  type AreaData,
  type HistogramData,
  type IChartApi,
  type ISeriesApi,
  type CandlestickData,
  type LineData,
} from 'lightweight-charts';
import type { CandleDto, ChartMode, ChartTimeframe, IndicatorResponseDto, PortfolioStateDto } from '../types';
import {
  toCandlestickSeries,
  toHeikinAshiSeries,
  toKagiSeries,
  toLineSeries,
  toRangeBarSeries,
  toRangeLineSeries,
  toUtcTimestamp,
} from '../chartTransforms';
import {
  buildLogicalViewportRange,
  clampCandlesByTimeframe,
  computeResistanceBounds,
  downsampleCandlesForRender,
  isIntradayTimeframe,
  sanitizeCandlesForChart,
} from '../utils/chartViewport';

type Props = {
  candles: CandleDto[];
  timeframe: ChartTimeframe;
  onTimeframeChange: (timeframe: ChartTimeframe) => void;
  indicator: IndicatorResponseDto | null;
  portfolio: PortfolioStateDto | null;
  isLoading?: boolean;
};

const timeframeButtons: Array<{ key: ChartTimeframe; label: string }> = [
  { key: '5m', label: '5 min' },
  { key: '15m', label: '15 min' },
  { key: '1h', label: '1 hr' },
  { key: '1d', label: 'Day' },
  { key: '1w', label: 'Week' },
  { key: '1mo', label: 'Month' },
  { key: '1y', label: 'Year' },
  { key: 'all', label: 'All' },
];

const chartModeButtons: Array<{ key: ChartMode; label: string }> = [
  { key: 'candle', label: 'Candles' },
  { key: 'line', label: 'Line' },
  { key: 'heikin-ashi', label: 'Heikin' },
  { key: 'kagi', label: 'Kagi' },
  { key: 'range-line', label: 'Range Line' },
  { key: 'range-bar', label: 'Range Bar' },
];


function computeMovingAverageSeries(candles: CandleDto[], window: number): LineData[] {
  if (candles.length < window || window <= 0) {
    return [];
  }

  const values: LineData[] = [];
  let rollingSum = 0;

  for (let index = 0; index < candles.length; index++) {
    rollingSum += candles[index].close;

    if (index >= window) {
      rollingSum -= candles[index - window].close;
    }

    if (index < window - 1) {
      continue;
    }

    const average = rollingSum / window;
    values.push({ time: toUtcTimestamp(candles[index].timestamp), value: average });
  }

  return values;
}

function computeRsiValues(candles: CandleDto[], period = 14): Array<number | null> {
  if (candles.length < 2) {
    return candles.map(() => null);
  }

  const rsi: Array<number | null> = [null];
  let gainSum = 0;
  let lossSum = 0;

  for (let index = 1; index < candles.length; index++) {
    const delta = candles[index].close - candles[index - 1].close;
    const gain = Math.max(delta, 0);
    const loss = Math.max(-delta, 0);

    gainSum += gain;
    lossSum += loss;

    if (index > period) {
      const previousDelta = candles[index - period].close - candles[index - period - 1].close;
      gainSum -= Math.max(previousDelta, 0);
      lossSum -= Math.max(-previousDelta, 0);
    }

    if (index < period) {
      rsi.push(null);
      continue;
    }

    const averageGain = gainSum / period;
    const averageLoss = lossSum / period;

    if (averageLoss === 0) {
      rsi.push(100);
      continue;
    }

    const relativeStrength = averageGain / averageLoss;
    rsi.push(100 - 100 / (1 + relativeStrength));
  }

  return rsi;
}

function getBiasPalette(indicator: IndicatorResponseDto | null): { topColor: string; bottomColor: string; lineColor: string } {
  if (!indicator) {
    return {
      topColor: 'rgba(96, 165, 250, 0.12)',
      bottomColor: 'rgba(96, 165, 250, 0.02)',
      lineColor: 'rgba(96, 165, 250, 0.25)',
    };
  }

  if (indicator.rsi >= indicator.avgHighRSI) {
    return {
      topColor: 'rgba(245, 158, 11, 0.25)',
      bottomColor: 'rgba(245, 158, 11, 0.02)',
      lineColor: 'rgba(245, 158, 11, 0.35)',
    };
  }

  if (indicator.rsi <= indicator.avgLowRSI) {
    return {
      topColor: 'rgba(59, 130, 246, 0.22)',
      bottomColor: 'rgba(59, 130, 246, 0.02)',
      lineColor: 'rgba(59, 130, 246, 0.3)',
    };
  }

  if (indicator.crossover >= 0) {
    return {
      topColor: 'rgba(34, 197, 94, 0.2)',
      bottomColor: 'rgba(34, 197, 94, 0.03)',
      lineColor: 'rgba(34, 197, 94, 0.25)',
    };
  }

  return {
    topColor: 'rgba(239, 68, 68, 0.2)',
    bottomColor: 'rgba(239, 68, 68, 0.03)',
    lineColor: 'rgba(239, 68, 68, 0.25)',
  };
}

function getSignalColor(rsi: number | null, crossover: number, indicator: IndicatorResponseDto | null): string {
  if (!indicator || rsi === null) {
    return crossover >= 0 ? 'rgba(34, 197, 94, 0.45)' : 'rgba(239, 68, 68, 0.45)';
  }

  if (rsi >= indicator.avgHighRSI) {
    return 'rgba(245, 158, 11, 0.7)';
  }

  if (rsi <= indicator.avgLowRSI) {
    return 'rgba(59, 130, 246, 0.7)';
  }

  return crossover >= 0 ? 'rgba(34, 197, 94, 0.55)' : 'rgba(239, 68, 68, 0.55)';
}

export default function TradingChart({ candles, timeframe, onTimeframeChange, indicator, portfolio, isLoading = false }: Props) {
  const containerRef = useRef<HTMLDivElement | null>(null);
  const chartRef = useRef<IChartApi | null>(null);
  const seriesRefs = useRef<ISeriesApi<any>[]>([]);
  const shouldResetViewportRef = useRef(true);
  const [mode, setMode] = useState<ChartMode>('candle');

  const sanitizedCandles = useMemo(() => sanitizeCandlesForChart(candles), [candles]);
  const visibleCandles = useMemo(() => {
    const scoped = clampCandlesByTimeframe(sanitizedCandles, timeframe);
    return downsampleCandlesForRender(scoped);
  }, [sanitizedCandles, timeframe]);

  const overlaySummary = useMemo(() => {
    if (!indicator) {
      return 'Waiting for indicator data…';
    }

    const position = portfolio && portfolio.position_quantity > 0
      ? `Position: long ${portfolio.position_quantity.toFixed(4)} @ ${portfolio.entry_price.toFixed(4)}`
      : 'Position: flat';

    return `${indicator.selectedPeriodKey} · RSI ${indicator.rsi.toFixed(2)} · crossover ${indicator.crossover.toFixed(4)} · ${position}`;
  }, [indicator, portfolio]);

  const movingAverageShort = useMemo(() => computeMovingAverageSeries(visibleCandles, 12), [visibleCandles]);
  const movingAverageLong = useMemo(() => computeMovingAverageSeries(visibleCandles, 26), [visibleCandles]);
  const rsiValues = useMemo(() => computeRsiValues(visibleCandles, 14), [visibleCandles]);
  const biasPalette = useMemo(() => getBiasPalette(indicator), [indicator]);
  const resistanceBounds = useMemo(() => computeResistanceBounds(visibleCandles), [visibleCandles]);
  const resistanceBandData = useMemo(
    () => visibleCandles.map((candle) => ({
      time: toUtcTimestamp(candle.timestamp),
      value: resistanceBounds.resistanceHigh,
    })),
    [resistanceBounds.resistanceHigh, visibleCandles],
  );
  const timestampKeys = useMemo(() => visibleCandles.map((candle) => toUtcTimestamp(candle.timestamp)), [visibleCandles]);
  const movingAverageShortByTime = useMemo(
    () => new Map(movingAverageShort.map((point) => [point.time, point.value])),
    [movingAverageShort],
  );
  const movingAverageLongByTime = useMemo(
    () => new Map(movingAverageLong.map((point) => [point.time, point.value])),
    [movingAverageLong],
  );

  useEffect(() => {
    if (!containerRef.current) {
      return;
    }

    const chart = createChart(containerRef.current, {
      autoSize: true,
      layout: {
        textColor: '#e5e7eb',
        background: { type: ColorType.Solid, color: '#10192c' },
      },
      grid: {
        vertLines: { color: '#22314d' },
        horzLines: { color: '#22314d' },
      },
      rightPriceScale: {
        borderColor: '#2f4368',
      },
      timeScale: {
        borderColor: '#2f4368',
        timeVisible: true,
        secondsVisible: false,
      },
      handleScroll: {
        mouseWheel: true,
        pressedMouseMove: true,
        horzTouchDrag: true,
        vertTouchDrag: false,
      },
      handleScale: {
        mouseWheel: true,
        pinch: true,
        axisPressedMouseMove: true,
        axisDoubleClickReset: false,
      },
      crosshair: {
        vertLine: { color: '#7aa2f7' },
        horzLine: { color: '#7aa2f7' },
      },
    });

    chartRef.current = chart;

    return () => {
      chart.remove();
      chartRef.current = null;
      seriesRefs.current = [];
    };
  }, []);

  useEffect(() => {
    const chart = chartRef.current;
    if (!chart) {
      return;
    }

    chart.applyOptions({
      timeScale: {
        borderColor: '#2f4368',
        timeVisible: isIntradayTimeframe(timeframe),
        secondsVisible: false,
      },
    });
  }, [timeframe]);

  useEffect(() => {
    shouldResetViewportRef.current = true;
  }, [timeframe]);

  useEffect(() => {
    const chart = chartRef.current;
    if (!chart) {
      return;
    }

    seriesRefs.current.forEach((series) => chart.removeSeries(series));
    seriesRefs.current = [];

    if (visibleCandles.length === 0) {
      return;
    }

    const resistanceBand = chart.addBaselineSeries({
      baseValue: { type: 'price', price: resistanceBounds.resistanceLow },
      topLineColor: 'rgba(125, 211, 252, 0.0)',
      topFillColor1: 'rgba(56, 189, 248, 0.20)',
      topFillColor2: 'rgba(56, 189, 248, 0.08)',
      bottomLineColor: 'rgba(125, 211, 252, 0.0)',
      bottomFillColor1: 'rgba(56, 189, 248, 0.0)',
      bottomFillColor2: 'rgba(56, 189, 248, 0.0)',
      lineWidth: 1,
      lineStyle: LineStyle.Solid,
      lineVisible: false,
      priceLineVisible: false,
      lastValueVisible: false,
      crosshairMarkerVisible: false,
    });

    resistanceBand.setData(resistanceBandData as AreaData[]);
    seriesRefs.current.push(resistanceBand);

    if (mode === 'line' || mode === 'kagi' || mode === 'range-line') {
      const primarySeries = chart.addLineSeries({
        color: mode === 'kagi' ? '#f59e0b' : mode === 'range-line' ? '#60a5fa' : '#22c55e',
        lineWidth: 2,
      });

      const primaryData: LineData[] =
        mode === 'line'
          ? toLineSeries(visibleCandles)
          : mode === 'kagi'
            ? toKagiSeries(visibleCandles)
            : toRangeLineSeries(visibleCandles);

      primarySeries.setData(primaryData);
      seriesRefs.current.push(primarySeries);
    } else if (mode === 'range-bar') {
      const primarySeries = chart.addHistogramSeries({
        priceFormat: { type: 'price', precision: 4, minMove: 0.0001 },
      });

      primarySeries.setData(toRangeBarSeries(visibleCandles) as HistogramData[]);
      seriesRefs.current.push(primarySeries);
    } else {
      const primarySeries = chart.addCandlestickSeries({
        upColor: '#22c55e',
        downColor: '#f14f4f',
        borderVisible: false,
        wickUpColor: '#22c55e',
        wickDownColor: '#f14f4f',
      });

      const primaryData: CandlestickData[] =
        mode === 'heikin-ashi' ? toHeikinAshiSeries(visibleCandles) : toCandlestickSeries(visibleCandles);

      primarySeries.setData(primaryData);
      seriesRefs.current.push(primarySeries);

      if (portfolio && portfolio.position_quantity > 0) {
        primarySeries.createPriceLine({
          price: portfolio.entry_price,
          color: '#38bdf8',
          lineWidth: 2,
          lineStyle: LineStyle.Dashed,
          axisLabelVisible: true,
          title: `Position ${portfolio.position_quantity.toFixed(4)}`,
        });
      }
    }

    const shortSeries = chart.addLineSeries({
      color: '#60a5fa',
      lineWidth: 2,
      priceLineVisible: false,
      lastValueVisible: false,
      crosshairMarkerVisible: false,
    });
    shortSeries.setData(movingAverageShort);
    seriesRefs.current.push(shortSeries);

    const longSeries = chart.addLineSeries({
      color: '#f59e0b',
      lineWidth: 2,
      priceLineVisible: false,
      lastValueVisible: false,
      crosshairMarkerVisible: false,
    });
    longSeries.setData(movingAverageLong);
    seriesRefs.current.push(longSeries);

    const resistanceHighSeries = chart.addLineSeries({
      color: 'rgba(56, 189, 248, 0.8)',
      lineWidth: 1,
      lineStyle: LineStyle.Dashed,
      priceLineVisible: false,
      lastValueVisible: false,
      crosshairMarkerVisible: false,
    });
    resistanceHighSeries.setData(
      timestampKeys.map((time) => ({
        time,
        value: resistanceBounds.resistanceHigh,
      })),
    );
    seriesRefs.current.push(resistanceHighSeries);

    const resistanceLowSeries = chart.addLineSeries({
      color: 'rgba(56, 189, 248, 0.8)',
      lineWidth: 1,
      lineStyle: LineStyle.Dashed,
      priceLineVisible: false,
      lastValueVisible: false,
      crosshairMarkerVisible: false,
    });
    resistanceLowSeries.setData(
      timestampKeys.map((time) => ({
        time,
        value: resistanceBounds.resistanceLow,
      })),
    );
    seriesRefs.current.push(resistanceLowSeries);

    const crossoverSeries = chart.addHistogramSeries({
      priceScaleId: 'signal',
      priceFormat: { type: 'volume' },
      priceLineVisible: false,
      lastValueVisible: false,
      base: 0,
    });

    chart.priceScale('signal').applyOptions({
      visible: false,
      scaleMargins: {
        top: 0.78,
        bottom: 0,
      },
    });

    const crossoverData: HistogramData[] = visibleCandles.map((candle, index) => {
      const time = timestampKeys[index];
      const shortAverage = movingAverageShortByTime.get(time);
      const longAverage = movingAverageLongByTime.get(time);
      const crossoverValue = shortAverage !== undefined && longAverage !== undefined ? shortAverage - longAverage : 0;

      return {
        time,
        value: crossoverValue,
        color: getSignalColor(rsiValues[index], crossoverValue, indicator),
      };
    });

    crossoverSeries.setData(crossoverData);
    seriesRefs.current.push(crossoverSeries);

    if (shouldResetViewportRef.current) {
      chart.timeScale().setVisibleLogicalRange(buildLogicalViewportRange(visibleCandles.length));

      shouldResetViewportRef.current = false;
    }
  }, [biasPalette, indicator, mode, movingAverageLong, movingAverageLongByTime, movingAverageShort, movingAverageShortByTime, portfolio, resistanceBandData, resistanceBounds.resistanceHigh, resistanceBounds.resistanceLow, rsiValues, timeframe, timestampKeys, visibleCandles]);

  return (
    <section className="panel">
      <div className="panel-header">
        <div>
          <h2>Market Chart</h2>
          <p style={{ margin: '0.35rem 0 0', color: '#9ca3af', fontSize: '0.92rem' }}>{overlaySummary}</p>
        </div>
        <div style={{ display: 'flex', flexWrap: 'wrap', gap: '0.5rem', justifyContent: 'flex-end' }}>
          {timeframeButtons.map((button) => (
            <button
              key={button.key}
              type="button"
              onClick={() => onTimeframeChange(button.key)}
              disabled={isLoading && button.key === timeframe}
              style={{
                padding: '0.4rem 0.7rem',
                borderRadius: '999px',
                border: button.key === timeframe ? '1px solid #60a5fa' : '1px solid #334155',
                background: button.key === timeframe ? '#0f172a' : '#111827',
                color: button.key === timeframe ? '#e0f2fe' : '#cbd5e1',
                cursor: 'pointer',
              }}
            >
              {button.label}
            </button>
          ))}
        </div>
        <div style={{ display: 'flex', flexWrap: 'wrap', gap: '0.5rem', justifyContent: 'flex-end' }}>
          {chartModeButtons.map((button) => (
            <button
              key={button.key}
              type="button"
              onClick={() => setMode(button.key)}
              style={{
                padding: '0.35rem 0.65rem',
                borderRadius: '999px',
                border: button.key === mode ? '1px solid #22d3ee' : '1px solid #334155',
                background: button.key === mode ? '#082f49' : '#111827',
                color: button.key === mode ? '#cffafe' : '#cbd5e1',
                cursor: 'pointer',
              }}
            >
              {button.label}
            </button>
          ))}
        </div>
      </div>
      <div ref={containerRef} className="chart-surface" />
    </section>
  );
}