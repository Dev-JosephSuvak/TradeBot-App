import { useEffect, useMemo, useRef } from 'react';
import {
  ColorType,
  createChart,
  type IChartApi,
  type ISeriesApi,
  type HistogramData,
  type CandlestickData,
  type LineData,
} from 'lightweight-charts';
import type { CandleDto, ChartMode } from '../types';
import {
  toCandlestickSeries,
  toHeikinAshiSeries,
  toKagiSeries,
  toLineSeries,
  toRangeBarSeries,
  toRangeLineSeries,
} from '../chartTransforms';

type Props = {
  candles: CandleDto[];
  mode: ChartMode;
};

export default function TradingChart({ candles, mode }: Props) {
  const containerRef = useRef<HTMLDivElement | null>(null);
  const chartRef = useRef<IChartApi | null>(null);
  const seriesRef = useRef<ISeriesApi<any> | null>(null);

  const title = useMemo(() => {
    switch (mode) {
      case 'line':
        return 'Line View';
      case 'candle':
        return 'Candlestick View';
      case 'range-line':
        return 'Range Line View (High - Low)';
      case 'range-bar':
        return 'Range Bar View (High - Low)';
      case 'heikin-ashi':
        return 'Heikin Ashi View';
      case 'kagi':
        return 'Kagi View';
      default:
        return 'Chart View';
    }
  }, [mode]);

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
      seriesRef.current = null;
    };
  }, []);

  useEffect(() => {
    const chart = chartRef.current;
    if (!chart) {
      return;
    }

    if (seriesRef.current) {
      chart.removeSeries(seriesRef.current);
      seriesRef.current = null;
    }

    if (candles.length === 0) {
      return;
    }

    if (mode === 'line' || mode === 'kagi' || mode === 'range-line') {
      const series = chart.addLineSeries({
        color: mode === 'kagi' ? '#f59e0b' : mode === 'range-line' ? '#60a5fa' : '#22c55e',
        lineWidth: 2,
      });
      seriesRef.current = series;

      const data: LineData[] =
        mode === 'line'
          ? toLineSeries(candles)
          : mode === 'kagi'
            ? toKagiSeries(candles)
            : toRangeLineSeries(candles);

      series.setData(data);
      chart.timeScale().fitContent();
      return;
    }

    if (mode === 'range-bar') {
      const series = chart.addHistogramSeries({
        priceFormat: { type: 'price', precision: 4, minMove: 0.0001 },
      });
      seriesRef.current = series;

      series.setData(toRangeBarSeries(candles) as HistogramData[]);
      chart.timeScale().fitContent();
      return;
    }

    const series = chart.addCandlestickSeries({
      upColor: '#22c55e',
      downColor: '#f14f4f',
      borderVisible: false,
      wickUpColor: '#22c55e',
      wickDownColor: '#f14f4f',
    });

    seriesRef.current = series;

    const candleData: CandlestickData[] =
      mode === 'heikin-ashi' ? toHeikinAshiSeries(candles) : toCandlestickSeries(candles);

    series.setData(candleData);
    chart.timeScale().fitContent();
  }, [candles, mode]);

  return (
    <section className="panel">
      <div className="panel-header">
        <h2>{title}</h2>
      </div>
      <div ref={containerRef} className="chart-surface" />
    </section>
  );
}