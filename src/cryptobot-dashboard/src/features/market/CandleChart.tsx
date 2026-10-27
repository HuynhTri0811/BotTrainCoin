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
import { useChartColors } from '../../theme';
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
  const colors = useChartColors();

  // Chỉ tạo lại series khi danh sách đường thay đổi, không phải mỗi lần dữ liệu được làm mới.
  const linesKey = JSON.stringify(indicator?.indicator.lines ?? []);
  const hasPane = (indicator?.indicator.lines ?? []).some((l) => !l.overlay);

  useEffect(() => {
    const el = containerRef.current;
    if (!el) return;

    const chart = createChart(el, {
      autoSize: true,
      crosshair: { mode: CrosshairMode.Normal },
      timeScale: {
        timeVisible: true,
        tickMarkFormatter: (time: Time, type: TickMarkType) =>
          formatVnTick(time as number, type === TickMarkType.DayOfMonth || type === TickMarkType.Month),
      },
      localization: { locale: 'vi-VN', timeFormatter: (time: Time) => formatVnUnix(time as number) },
    });

    // Màu theo chế độ sáng/tối được áp ở effect bên dưới.
    const candleSeries = chart.addSeries(CandlestickSeries, { borderVisible: false });
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

  // Đổi chế độ sáng/tối: áp màu mới lên chart đang có, không tạo lại.
  useEffect(() => {
    const h = handles.current;
    if (!h) return;
    h.chart.applyOptions({
      layout: { background: { type: ColorType.Solid, color: colors.background }, textColor: colors.text },
      grid: { vertLines: { color: colors.grid }, horzLines: { color: colors.grid } },
      rightPriceScale: { borderColor: colors.border },
      timeScale: { borderColor: colors.border },
    });
    h.candles.applyOptions({
      upColor: colors.up,
      downColor: colors.down,
      wickUpColor: colors.up,
      wickDownColor: colors.down,
    });
  }, [colors, fastLabel, slowLabel]);

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
        ...(c.isClosed ? {} : { color: colors.running, wickColor: colors.running }),
      })),
    );
    h.volume.setData(
      candles.map((c) => ({
        time: c.time as UTCTimestamp,
        value: c.volume,
        color: !c.isClosed ? colors.runningVolume : c.close >= c.open ? colors.upVolume : colors.downVolume,
      })),
    );
    h.fast.setData(
      candles.filter((c) => c.fastEma !== null).map((c) => ({ time: c.time as UTCTimestamp, value: c.fastEma! })),
    );
    h.slow.setData(
      candles.filter((c) => c.slowEma !== null).map((c) => ({ time: c.time as UTCTimestamp, value: c.slowEma! })),
    );
    if (!fitted.current && candles.length > 0) {
      fitted.current = true;
      // autoSize đo kích thước bất đồng bộ; fit sau frame kế tiếp để dùng đúng chiều rộng.
      requestAnimationFrame(() => handles.current?.chart.timeScale().fitContent());
    }
  }, [candles, colors]);

  useEffect(() => {
    const h = handles.current;
    if (!h) return;
    const lines = JSON.parse(linesKey) as IndicatorChart['indicator']['lines'];
    const created = lines.map((l) => {
      const series = h.chart.addSeries(
        LineSeries,
        { color: l.color, lineWidth: 2, title: l.title, priceLineVisible: false },
        l.overlay ? 0 : 1,
      );
      for (const level of l.levels)
        series.createPriceLine({ price: level, color: colors.level, lineStyle: LineStyle.Dashed, lineWidth: 1, axisLabelVisible: true, title: '' });
      return [l.key, series] as const;
    });
    indicatorSeries.current = new Map(created);
    if (lines.some((l) => !l.overlay)) h.chart.panes()[1]?.setHeight(INDICATOR_PANE_HEIGHT);

    return () => {
      // Khi chart bị tạo lại, chart cũ đã bị remove cùng các series.
      const chart = handles.current?.chart;
      if (chart) for (const [, series] of created) chart.removeSeries(series);
      indicatorSeries.current = new Map();
    };
  }, [linesKey, fastLabel, slowLabel, colors]);

  useEffect(() => {
    for (const [key, series] of indicatorSeries.current) {
      series.setData(
        (indicator?.points ?? [])
          .filter((p) => p.values[key] != null)
          .map((p) => ({ time: p.time as UTCTimestamp, value: p.values[key]! })),
      );
    }
  }, [indicator, linesKey, fastLabel, slowLabel, colors]);

  useEffect(() => {
    const h = handles.current;
    if (!h) return;
    const markers: SeriesMarker<Time>[] = signals.map((s) =>
      s.action === 'Buy'
        ? { time: s.time as UTCTimestamp, position: 'belowBar', color: colors.up, shape: 'arrowUp', text: 'BUY' }
        : { time: s.time as UTCTimestamp, position: 'aboveBar', color: colors.down, shape: 'arrowDown', text: 'SELL' },
    );
    // Tín hiệu của chỉ báo tự viết: chấm tím, khác mũi tên BUY/SELL của chiến lược EMA.
    for (const m of indicator?.markers ?? [])
      markers.push(
        m.signal === 'Long'
          ? { time: m.time as UTCTimestamp, position: 'belowBar', color: INDICATOR_MARKER_COLOR, shape: 'circle', text: 'L' }
          : { time: m.time as UTCTimestamp, position: 'aboveBar', color: INDICATOR_MARKER_COLOR, shape: 'circle', text: 'S' },
      );
    markers.sort((a, b) => (a.time as number) - (b.time as number));
    h.markers.setMarkers(markers);
  }, [signals, indicator, fastLabel, slowLabel, colors]);

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
    add(position.entryPrice, `${position.side} ${position.leverage}x`, colors.entry, LineStyle.Solid);
    add(position.stopLoss, 'SL', colors.down, LineStyle.Dashed);
    if (position.takeProfit != null) add(position.takeProfit, 'TP', colors.up, LineStyle.Dashed);
    add(position.liquidationPrice, 'Thanh lý', colors.liquidation, LineStyle.Dotted);
  }, [position, fastLabel, slowLabel, colors]);

  return (
    <div
      ref={containerRef}
      style={{ width: '100%', height: CHART_HEIGHT + (hasPane ? INDICATOR_PANE_HEIGHT : 0) }}
    />
  );
}

export const EMA_COLORS = { fast: FAST_COLOR, slow: SLOW_COLOR };
