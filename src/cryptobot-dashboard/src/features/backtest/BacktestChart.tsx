import { useEffect, useRef } from 'react';
import {
  CandlestickSeries,
  ColorType,
  createChart,
  createSeriesMarkers,
  CrosshairMode,
  LineSeries,
  TickMarkType,
  type SeriesMarker,
  type Time,
  type UTCTimestamp,
} from 'lightweight-charts';
import type { BacktestResult } from '../../api/client';
import { formatVnTick, formatVnUnix } from '../../time';

const PRICE_HEIGHT = 420;
const EQUITY_HEIGHT = 160;
const MAX_LABELLED_MARKERS = 200;

export const EXIT_COLORS = { TakeProfit: '#16a34a', StopLoss: '#dc2626', Signal: '#d97706' } as const;
const ENTRY_COLOR = '#2563eb';
const EXIT_TEXT = { TakeProfit: 'TP', StopLoss: 'SL', Signal: 'SELL' } as const;

/**
 * Nến của khoảng backtest kèm điểm vào (mũi tên xanh dưới nến) và thoát lệnh (mũi tên trên nến: TP xanh lá,
 * SL đỏ, SELL cam), cùng đường tổng vốn ở khung dưới. Dữ liệu dài đã được backend gộp thành tối đa 3000 cột.
 */
export function BacktestChart({ result }: { result: BacktestResult }) {
  const containerRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    const element = containerRef.current;
    if (!element) return;

    const chart = createChart(element, {
      autoSize: true,
      layout: { background: { type: ColorType.Solid, color: '#ffffff' }, textColor: '#334155' },
      grid: { vertLines: { color: '#f1f5f9' }, horzLines: { color: '#f1f5f9' } },
      crosshair: { mode: CrosshairMode.Normal },
      rightPriceScale: { borderColor: '#e2e8f0' },
      timeScale: {
        borderColor: '#e2e8f0',
        timeVisible: true,
        tickMarkFormatter: (time: Time, type: TickMarkType) =>
          formatVnTick(time as number, type === TickMarkType.DayOfMonth || type === TickMarkType.Month
            || type === TickMarkType.Year),
      },
      localization: { locale: 'vi-VN', timeFormatter: (time: Time) => formatVnUnix(time as number) },
    });

    const candles = chart.addSeries(CandlestickSeries, {
      upColor: '#16a34a',
      downColor: '#dc2626',
      wickUpColor: '#16a34a',
      wickDownColor: '#dc2626',
      borderVisible: false,
    });
    candles.setData(
      result.chart.bars.map((bar) => ({
        time: bar.time as UTCTimestamp,
        open: bar.open,
        high: bar.high,
        low: bar.low,
        close: bar.close,
      })),
    );

    // Nhiều lệnh thì bỏ chữ, chỉ giữ mũi tên có màu: chữ chồng lên nhau che mất nến.
    const showText = result.chart.markers.length <= MAX_LABELLED_MARKERS;
    const markers: SeriesMarker<Time>[] = result.chart.markers.map((marker) =>
      marker.kind === 'Entry'
        ? { time: marker.time as UTCTimestamp, position: 'belowBar', color: ENTRY_COLOR, shape: 'arrowUp', text: showText ? 'BUY' : '' }
        : {
            time: marker.time as UTCTimestamp,
            position: 'aboveBar',
            color: EXIT_COLORS[marker.exitReason ?? 'Signal'],
            shape: 'arrowDown',
            text: showText ? EXIT_TEXT[marker.exitReason ?? 'Signal'] : '',
          },
    );
    markers.sort((first, second) => (first.time as number) - (second.time as number));
    createSeriesMarkers(candles, markers);

    const equity = chart.addSeries(
      LineSeries,
      { color: '#7c3aed', lineWidth: 2, title: 'Tổng vốn', priceLineVisible: false },
      1,
    );
    equity.setData(
      result.chart.bars
        .filter((bar) => bar.equity !== null)
        .map((bar) => ({ time: bar.time as UTCTimestamp, value: bar.equity! })),
    );
    chart.panes()[1]?.setHeight(EQUITY_HEIGHT);

    // autoSize đo kích thước bất đồng bộ; fit sau frame kế tiếp để dùng đúng chiều rộng.
    const frame = requestAnimationFrame(() => chart.timeScale().fitContent());
    return () => {
      cancelAnimationFrame(frame);
      chart.remove();
    };
  }, [result]);

  return <div ref={containerRef} style={{ width: '100%', height: PRICE_HEIGHT + EQUITY_HEIGHT }} />;
}
