import { useEffect, useRef } from 'react';
import {
  CandlestickSeries,
  ColorType,
  createChart,
  createSeriesMarkers,
  CrosshairMode,
  HistogramSeries,
  LineSeries,
  LineStyle,
  TickMarkType,
  type IChartApi,
  type IPriceLine,
  type ISeriesApi,
  type ISeriesMarkersPluginApi,
  type SeriesMarker,
  type Time,
  type UTCTimestamp,
} from 'lightweight-charts';
import type { ChartPoint, FuturesPosition, IndicatorChart, Signal } from '../../api/client';
import { formatVnTick, formatVnUnix } from '../../time';

const FAST_COLOR = '#d97706';
const SLOW_COLOR = '#2563eb';
export const INDICATOR_MARKER_COLOR = '#7c3aed';
const CHART_HEIGHT = 480;
const INDICATOR_PANE_HEIGHT = 160;

interface Props {
  candles: ChartPoint[];
  signals: Signal[];
  fastLabel: string;
  slowLabel: string;
  /** Vị thế paper futures đang mở trên symbol này: vẽ Entry/SL/TP/Thanh lý. */
  position?: FuturesPosition;
  /** Chỉ báo tự viết cùng symbol/khung nến: đường overlay vẽ trên nến, còn lại vẽ ở khung bên dưới. */
  indicator?: IndicatorChart;
}

interface Handles {
  chart: IChartApi;
  candles: ISeriesApi<'Candlestick'>;
  volume: ISeriesApi<'Histogram'>;
  fast: ISeriesApi<'Line'>;
  slow: ISeriesApi<'Line'>;
  markers: ISeriesMarkersPluginApi<Time>;
}

export function CandleChart({ candles, signals, fastLabel, slowLabel, position, indicator }: Props) {
  const containerRef = useRef<HTMLDivElement>(null);
  const handles = useRef<Handles | null>(null);
  const fitted = useRef(false);
  const priceLines = useRef<IPriceLine[]>([]);
  const indicatorSeries = useRef(new Map<string, ISeriesApi<'Line'>>());

  // Chỉ tạo lại series khi danh sách đường thay đổi, không phải mỗi lần dữ liệu được làm mới.
  const linesKey = JSON.stringify(indicator?.indicator.lines ?? []);
  const hasPane = (indicator?.indicator.lines ?? []).some((line) => !line.overlay);

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
    // Cột khối lượng ở đáy khung giá, thang riêng (không có trục) để không đè lên nến.
    const volume = chart.addSeries(HistogramSeries, {
      priceScaleId: 'volume',
      priceFormat: { type: 'volume' },
      lastValueVisible: false,
      priceLineVisible: false,
    });
    volume.priceScale().applyOptions({ scaleMargins: { top: 0.8, bottom: 0 }, visible: false });
    const fast = chart.addSeries(LineSeries, { color: FAST_COLOR, lineWidth: 2, title: fastLabel, priceLineVisible: false });
    const slow = chart.addSeries(LineSeries, { color: SLOW_COLOR, lineWidth: 2, title: slowLabel, priceLineVisible: false });
    const markers = createSeriesMarkers(candleSeries, []);

    handles.current = { chart, candles: candleSeries, volume, fast, slow, markers };
    fitted.current = false;

    return () => {
      priceLines.current = [];
      handles.current = null;
      chart.remove();
    };
  }, [fastLabel, slowLabel]);

  useEffect(() => {
    const chart = handles.current;
    if (!chart) return;

    chart.candles.setData(
      candles.map((point) => ({
        time: point.time as UTCTimestamp,
        open: point.open,
        high: point.high,
        low: point.low,
        close: point.close,
        // Nến đang chạy hiển thị nhạt hơn: chiến lược chưa dùng nến này.
        ...(point.isClosed ? {} : { color: '#94a3b8', wickColor: '#94a3b8' }),
      })),
    );
    chart.volume.setData(
      candles.map((point) => ({
        time: point.time as UTCTimestamp,
        value: point.volume,
        color: !point.isClosed ? '#cbd5e1' : point.close >= point.open ? 'rgba(22,163,74,0.35)' : 'rgba(220,38,38,0.35)',
      })),
    );
    chart.fast.setData(
      candles.filter((point) => point.fastEma !== null).map((point) => ({ time: point.time as UTCTimestamp, value: point.fastEma! })),
    );
    chart.slow.setData(
      candles.filter((point) => point.slowEma !== null).map((point) => ({ time: point.time as UTCTimestamp, value: point.slowEma! })),
    );
    if (!fitted.current && candles.length > 0) {
      fitted.current = true;
      // autoSize đo kích thước bất đồng bộ; fit sau frame kế tiếp để dùng đúng chiều rộng.
      requestAnimationFrame(() => handles.current?.chart.timeScale().fitContent());
    }
  }, [candles]);

  useEffect(() => {
    const chartHandles = handles.current;
    if (!chartHandles) return;
    const lines = JSON.parse(linesKey) as IndicatorChart['indicator']['lines'];
    const created = lines.map((line) => {
      const series = chartHandles.chart.addSeries(
        LineSeries,
        { color: line.color, lineWidth: 2, title: line.title, priceLineVisible: false },
        line.overlay ? 0 : 1,
      );
      for (const level of line.levels)
        series.createPriceLine({ price: level, color: '#94a3b8', lineStyle: LineStyle.Dashed, lineWidth: 1, axisLabelVisible: true, title: '' });
      return [line.key, series] as const;
    });
    indicatorSeries.current = new Map(created);
    if (lines.some((line) => !line.overlay)) chartHandles.chart.panes()[1]?.setHeight(INDICATOR_PANE_HEIGHT);

    return () => {
      // Khi chart bị tạo lại, chart cũ đã bị remove cùng các series.
      const chart = handles.current?.chart;
      if (chart) for (const [, series] of created) chart.removeSeries(series);
      indicatorSeries.current = new Map();
    };
  }, [linesKey, fastLabel, slowLabel]);

  useEffect(() => {
    for (const [key, series] of indicatorSeries.current) {
      series.setData(
        (indicator?.points ?? [])
          .filter((point) => point.values[key] != null)
          .map((point) => ({ time: point.time as UTCTimestamp, value: point.values[key]! })),
      );
    }
  }, [indicator, linesKey, fastLabel, slowLabel]);

  useEffect(() => {
    const chart = handles.current;
    if (!chart) return;
    const markers: SeriesMarker<Time>[] = signals.map((signal) =>
      signal.action === 'Buy'
        ? { time: signal.time as UTCTimestamp, position: 'belowBar', color: '#16a34a', shape: 'arrowUp', text: 'BUY' }
        : { time: signal.time as UTCTimestamp, position: 'aboveBar', color: '#dc2626', shape: 'arrowDown', text: 'SELL' },
    );
    // Tín hiệu của chỉ báo tự viết: chấm tím, khác mũi tên BUY/SELL của chiến lược EMA.
    for (const month of indicator?.markers ?? [])
      markers.push(
        month.signal === 'Long'
          ? { time: month.time as UTCTimestamp, position: 'belowBar', color: INDICATOR_MARKER_COLOR, shape: 'circle', text: 'L' }
          : { time: month.time as UTCTimestamp, position: 'aboveBar', color: INDICATOR_MARKER_COLOR, shape: 'circle', text: 'S' },
      );
    markers.sort((first, second) => (first.time as number) - (second.time as number));
    chart.markers.setMarkers(markers);
  }, [signals, indicator, fastLabel, slowLabel]);

  useEffect(() => {
    const chart = handles.current;
    if (!chart) return;
    for (const line of priceLines.current) chart.candles.removePriceLine(line);
    priceLines.current = [];
    if (!position) return;

    const add = (price: number, title: string, color: string, style: LineStyle) =>
      priceLines.current.push(
        chart.candles.createPriceLine({ price, title, color, lineStyle: style, lineWidth: 1, axisLabelVisible: true }),
      );
    add(position.entryPrice, `${position.side} ${position.leverage}x`, '#475569', LineStyle.Solid);
    add(position.stopLoss, 'SL', '#dc2626', LineStyle.Dashed);
    if (position.takeProfit != null) add(position.takeProfit, 'TP', '#16a34a', LineStyle.Dashed);
    add(position.liquidationPrice, 'Thanh lý', '#9333ea', LineStyle.Dotted);
  }, [position, fastLabel, slowLabel]);

  return (
    <div
      ref={containerRef}
      style={{ width: '100%', height: CHART_HEIGHT + (hasPane ? INDICATOR_PANE_HEIGHT : 0) }}
    />
  );
}

export const EMA_COLORS = { fast: FAST_COLOR, slow: SLOW_COLOR };
