import { useEffect, useRef } from 'react';
import {
  CandlestickSeries,
  ColorType,
  createChart,
  createSeriesMarkers,
  CrosshairMode,
  LineSeries,
  LineStyle,
  TickMarkType,
  type IChartApi,
  type IPriceLine,
  type ISeriesApi,
  type ISeriesMarkersPluginApi,
  type Time,
  type UTCTimestamp,
} from 'lightweight-charts';
import type { ChartPoint, FuturesPosition, Signal } from '../../api/client';
import { formatVnTick, formatVnUnix } from '../../time';

const FAST_COLOR = '#d97706';
const SLOW_COLOR = '#2563eb';

interface Props {
  candles: ChartPoint[];
  signals: Signal[];
  fastLabel: string;
  slowLabel: string;
  /** Vị thế paper futures đang mở trên symbol này: vẽ Entry/SL/TP/Thanh lý. */
  position?: FuturesPosition;
}

interface Handles {
  chart: IChartApi;
  candles: ISeriesApi<'Candlestick'>;
  fast: ISeriesApi<'Line'>;
  slow: ISeriesApi<'Line'>;
  markers: ISeriesMarkersPluginApi<Time>;
}

export function CandleChart({ candles, signals, fastLabel, slowLabel, position }: Props) {
  const containerRef = useRef<HTMLDivElement>(null);
  const handles = useRef<Handles | null>(null);
  const fitted = useRef(false);
  const priceLines = useRef<IPriceLine[]>([]);

  useEffect(() => {
    const el = containerRef.current;
    if (!el) return;

    const chart = createChart(el, {
      autoSize: true,
      layout: { background: { type: ColorType.Solid, color: '#ffffff' }, textColor: '#334155' },
      grid: { vertLines: { color: '#f1f5f9' }, horzLines: { color: '#f1f5f9' } },
      crosshair: { mode: CrosshairMode.Normal },
      rightPriceScale: { borderColor: '#e2e8f0' },
      timeScale: {
        borderColor: '#e2e8f0',
        timeVisible: true,
        tickMarkFormatter: (time: Time, type: TickMarkType) =>
          formatVnTick(time as number, type === TickMarkType.DayOfMonth || type === TickMarkType.Month),
      },
      localization: { locale: 'vi-VN', timeFormatter: (time: Time) => formatVnUnix(time as number) },
    });

    const candleSeries = chart.addSeries(CandlestickSeries, {
      upColor: '#16a34a',
      downColor: '#dc2626',
      wickUpColor: '#16a34a',
      wickDownColor: '#dc2626',
      borderVisible: false,
    });
    const fast = chart.addSeries(LineSeries, { color: FAST_COLOR, lineWidth: 2, title: fastLabel, priceLineVisible: false });
    const slow = chart.addSeries(LineSeries, { color: SLOW_COLOR, lineWidth: 2, title: slowLabel, priceLineVisible: false });
    const markers = createSeriesMarkers(candleSeries, []);

    handles.current = { chart, candles: candleSeries, fast, slow, markers };
    fitted.current = false;

    return () => {
      priceLines.current = [];
      handles.current = null;
      chart.remove();
    };
  }, [fastLabel, slowLabel]);

  useEffect(() => {
    const h = handles.current;
    if (!h) return;

    h.candles.setData(
      candles.map((c) => ({
        time: c.time as UTCTimestamp,
        open: c.open,
        high: c.high,
        low: c.low,
        close: c.close,
        // Nến đang chạy hiển thị nhạt hơn: chiến lược chưa dùng nến này.
        ...(c.isClosed ? {} : { color: '#94a3b8', wickColor: '#94a3b8' }),
      })),
    );
    h.fast.setData(
      candles.filter((c) => c.fastEma !== null).map((c) => ({ time: c.time as UTCTimestamp, value: c.fastEma! })),
    );
    h.slow.setData(
      candles.filter((c) => c.slowEma !== null).map((c) => ({ time: c.time as UTCTimestamp, value: c.slowEma! })),
    );
    h.markers.setMarkers(
      signals.map((s) =>
        s.action === 'Buy'
          ? { time: s.time as UTCTimestamp, position: 'belowBar', color: '#16a34a', shape: 'arrowUp', text: 'BUY' }
          : { time: s.time as UTCTimestamp, position: 'aboveBar', color: '#dc2626', shape: 'arrowDown', text: 'SELL' },
      ),
    );

    if (!fitted.current && candles.length > 0) {
      fitted.current = true;
      // autoSize đo kích thước bất đồng bộ; fit sau frame kế tiếp để dùng đúng chiều rộng.
      requestAnimationFrame(() => handles.current?.chart.timeScale().fitContent());
    }
  }, [candles, signals]);

  useEffect(() => {
    const h = handles.current;
    if (!h) return;
    for (const line of priceLines.current) h.candles.removePriceLine(line);
    priceLines.current = [];
    if (!position) return;

    const add = (price: number, title: string, color: string, style: LineStyle) =>
      priceLines.current.push(
        h.candles.createPriceLine({ price, title, color, lineStyle: style, lineWidth: 1, axisLabelVisible: true }),
      );
    add(position.entryPrice, `${position.side} ${position.leverage}x`, '#475569', LineStyle.Solid);
    add(position.stopLoss, 'SL', '#dc2626', LineStyle.Dashed);
    if (position.takeProfit != null) add(position.takeProfit, 'TP', '#16a34a', LineStyle.Dashed);
    add(position.liquidationPrice, 'Thanh lý', '#9333ea', LineStyle.Dotted);
  }, [position, fastLabel, slowLabel]);

  return <div ref={containerRef} style={{ width: '100%', height: 480 }} />;
}

export const EMA_COLORS = { fast: FAST_COLOR, slow: SLOW_COLOR };
