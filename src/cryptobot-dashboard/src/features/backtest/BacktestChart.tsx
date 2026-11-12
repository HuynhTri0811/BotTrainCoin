import { useEffect, useRef } from 'react';
import {
  CandlestickSeries,
  ColorType,
  createChart,
  CrosshairMode,
  LineSeries,
  TickMarkType,
  type Time,
  type UTCTimestamp,
} from 'lightweight-charts';
import type { BacktestBar, BacktestSettings, StrategyParameters } from '../../api/client';
import { useChartColors } from '../../theme';
import { formatVnTick, formatVnUnix } from '../../time';
import { addEmaLines, addTradeOverlay, type OverlayTrade } from './tradeOverlay';

const EQUITY_HEIGHT = 160;
const MAX_LABELLED_TRADES = 100;

interface Props {
  bars: BacktestBar[];
  trades: OverlayTrade[];
  settings: BacktestSettings;
  parameters: StrategyParameters;
  /** Khung tổng vốn bên dưới (chart tổng); chart chi tiết một lệnh thì không có. */
  showEquity?: boolean;
  priceHeight?: number;
}

/**
 * Nến kèm EMA nhanh/chậm, đường giá vào và mức chốt lời/cắt lỗ của từng lệnh, mũi tên vào/ra có giá và lãi/lỗ.
 * Chart tổng dùng cột đã gộp (tối đa 3000 cột); chart chi tiết một lệnh dùng nến thật.
 */
export function BacktestChart({ bars, trades, settings, parameters, showEquity = false, priceHeight = 420 }: Props) {
  const containerRef = useRef<HTMLDivElement>(null);
  const colors = useChartColors();

  useEffect(() => {
    const element = containerRef.current;
    if (!element) return;

    const chart = createChart(element, {
      autoSize: true,
      layout: { background: { type: ColorType.Solid, color: colors.background }, textColor: colors.text },
      grid: { vertLines: { color: colors.grid }, horzLines: { color: colors.grid } },
      crosshair: { mode: CrosshairMode.Normal },
      rightPriceScale: { borderColor: colors.border },
      timeScale: {
        borderColor: colors.border,
        timeVisible: true,
        tickMarkFormatter: (time: Time, type: TickMarkType) =>
          formatVnTick(time as number, type === TickMarkType.DayOfMonth || type === TickMarkType.Month
            || type === TickMarkType.Year),
      },
      localization: { locale: 'vi-VN', timeFormatter: (time: Time) => formatVnUnix(time as number) },
    });

    const candles = chart.addSeries(CandlestickSeries, {
      upColor: colors.up,
      downColor: colors.down,
      wickUpColor: colors.up,
      wickDownColor: colors.down,
      borderVisible: false,
    });
    candles.setData(
      bars.map((bar) => ({ time: bar.time as UTCTimestamp, open: bar.open, high: bar.high, low: bar.low, close: bar.close })),
    );

    addEmaLines(chart, bars, parameters);
    // Nhiều lệnh thì bỏ chữ, chỉ giữ mũi tên có màu: chữ chồng lên nhau che mất nến.
    addTradeOverlay(chart, candles, bars, trades, settings, trades.length <= MAX_LABELLED_TRADES);

    if (showEquity) {
      const equity = chart.addSeries(
        LineSeries,
        { color: '#7c3aed', lineWidth: 2, title: 'Tổng vốn', priceLineVisible: false },
        1,
      );
      equity.setData(
        bars.filter((bar) => bar.equity !== null).map((bar) => ({ time: bar.time as UTCTimestamp, value: bar.equity! })),
      );
      chart.panes()[1]?.setHeight(EQUITY_HEIGHT);
    }

    // autoSize đo kích thước bất đồng bộ; fit sau frame kế tiếp để dùng đúng chiều rộng.
    const frame = requestAnimationFrame(() => chart.timeScale().fitContent());
    return () => {
      cancelAnimationFrame(frame);
      chart.remove();
    };
  }, [bars, trades, settings, parameters, showEquity, colors]);

  return <div ref={containerRef} style={{ width: '100%', height: priceHeight + (showEquity ? EQUITY_HEIGHT : 0) }} />;
}
