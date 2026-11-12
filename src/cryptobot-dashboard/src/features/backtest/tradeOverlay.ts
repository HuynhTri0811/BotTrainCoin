import {
  createSeriesMarkers,
  LineSeries,
  LineStyle,
  type AutoscaleInfo,
  type IChartApi,
  type ISeriesApi,
  type LineData,
  type SeriesMarker,
  type Time,
  type UTCTimestamp,
} from 'lightweight-charts';
import type { BacktestBar, BacktestOpenTrade, BacktestSettings, BacktestTrade, StrategyParameters } from '../../api/client';
import { fmtPrice2, fmtSignedPercent } from '../futures/format';

// Một nguồn màu duy nhất cho mũi tên, đường vị thế và chú thích.
export const EXIT_COLORS = { TakeProfit: '#16a34a', StopLoss: '#dc2626', Signal: '#d97706' } as const;
export const ENTRY_COLOR = '#0d9488';
export const TP_COLOR = EXIT_COLORS.TakeProfit;
export const SL_COLOR = EXIT_COLORS.StopLoss;
// Cùng màu EMA của trang Biểu đồ, nhưng nhạt hơn để mũi tên vào/ra nổi lên trên.
export const EMA_FAST_COLOR = 'rgba(217, 119, 6, 0.75)';
export const EMA_SLOW_COLOR = 'rgba(37, 99, 235, 0.75)';
const EXIT_TEXT = { TakeProfit: 'TP', StopLoss: 'SL', Signal: 'SELL' } as const;
const HIDDEN = 'transparent';

/** Lệnh để vẽ: lệnh đã đóng, hoặc lệnh còn mở ở cuối kỳ (exit null). */
export interface OverlayTrade {
  entryTimeUtc: string;
  entryPrice: number;
  exit: BacktestTrade | null;
}

export const toOverlayTrades = (trades: BacktestTrade[], open: BacktestOpenTrade | null): OverlayTrade[] => [
  ...trades.map((trade) => ({ entryTimeUtc: trade.entryTimeUtc, entryPrice: trade.entryPrice, exit: trade })),
  ...(open ? [{ entryTimeUtc: open.entryTimeUtc, entryPrice: open.entryPrice, exit: null }] : []),
];

const toUnix = (iso: string) => Math.floor(Date.parse(iso) / 1000);

/** Vị trí cột chứa thời điểm `unix` (cột cuối có time ≤ unix); -1 nếu trước cột đầu. */
function barIndexAt(times: number[], unix: number): number {
  let low = 0;
  let high = times.length - 1;
  let found = -1;
  while (low <= high) {
    const middle = (low + high) >> 1;
    if (times[middle] <= unix) {
      found = middle;
      low = middle + 1;
    } else high = middle - 1;
  }
  return found;
}

/**
 * Dữ liệu cho một series có đoạn hiện, đoạn ẩn. Lightweight Charts 5 nối mọi điểm (khoảng trống không làm đứt
 * đường) và tô đoạn i → i+1 bằng màu của điểm i, nên đoạn i → i+1 chỉ hiện khi `connects(i)`; điểm không có giá trị
 * lấy giá của điểm có giá trị gần nhất phía trước (hoặc phía sau) để đoạn ẩn nằm ngang. `shown` đánh dấu điểm đang
 * hiện, dùng để tính thang giá.
 */
function lineData(
  times: number[],
  values: (number | null)[],
  connects: (index: number) => boolean,
): { data: LineData[]; shown: boolean[] } {
  const filled = [...values];
  for (let index = 1; index < filled.length; index++) filled[index] ??= filled[index - 1];
  for (let index = filled.length - 2; index >= 0; index--) filled[index] ??= filled[index + 1];

  const drawsToNext = times.map((_, index) => index + 1 < times.length && connects(index));
  const shown = times.map((_, index) => drawsToNext[index] || (index > 0 && drawsToNext[index - 1]));
  const data = times.map((time, index) => ({
    time: time as UTCTimestamp,
    value: filled[index] ?? 0,
    ...(drawsToNext[index] ? {} : { color: HIDDEN }),
  }));
  return { data, shown };
}

/** Khoảng giá của các điểm đang hiện trong vùng đang xem; null nếu không có (thang giá theo nến). */
function visibleRange(chart: IChartApi, data: LineData[], shown: boolean[]): AutoscaleInfo | null {
  const range = chart.timeScale().getVisibleLogicalRange();
  const from = Math.max(0, Math.floor(range?.from ?? 0));
  const to = Math.min(data.length - 1, Math.ceil(range?.to ?? data.length - 1));
  let minValue = Infinity;
  let maxValue = -Infinity;
  for (let index = from; index <= to; index++) {
    if (!shown[index]) continue;
    minValue = Math.min(minValue, data[index].value);
    maxValue = Math.max(maxValue, data[index].value);
  }
  return minValue <= maxValue ? { priceRange: { minValue, maxValue } } : null;
}

/** Thêm một series đường từ `lineData`; điểm ẩn không được tính vào thang giá. */
function addLine(
  chart: IChartApi,
  line: { data: LineData[]; shown: boolean[] },
  options: { color: string; lineStyle?: LineStyle; title?: string },
) {
  chart
    .addSeries(LineSeries, {
      ...options,
      lineWidth: 1,
      priceLineVisible: false,
      lastValueVisible: false,
      crosshairMarkerVisible: false,
      autoscaleInfoProvider: () => visibleRange(chart, line.data, line.shown),
    })
    .setData(line.data);
}

/** Hai đường EMA nhanh/chậm; bỏ qua cột chưa có EMA (warm-up hoặc lần chạy cũ). */
export function addEmaLines(chart: IChartApi, bars: BacktestBar[], parameters: StrategyParameters) {
  const line = (pick: (bar: BacktestBar) => number | null, color: string, title: string) => {
    const data = bars.flatMap((bar) => {
      const value = pick(bar);
      return value == null ? [] : [{ time: bar.time as UTCTimestamp, value }];
    });
    if (data.length === 0) return;
    chart
      .addSeries(LineSeries, { color, lineWidth: 1, title, priceLineVisible: false, lastValueVisible: false, crosshairMarkerVisible: false })
      .setData(data);
  };
  line((bar) => bar.emaFast, EMA_FAST_COLOR, `EMA${parameters.fastPeriod}`);
  line((bar) => bar.emaSlow, EMA_SLOW_COLOR, `EMA${parameters.slowPeriod}`);
}

/** Một đoạn ngang: giữ giá `price` từ cột `from` đến cột `to`. */
interface Segment {
  from: number;
  to: number;
  price: number;
}

/**
 * Nhiều đoạn ngang rời nhau trên một series. Mỗi cột thuộc về tối đa một đoạn (đoạn sau thắng khi lệnh sau vào đúng
 * cột lệnh trước thoát), và chỉ vẽ nối hai cột cùng một đoạn, nên không bao giờ có đường chéo giữa hai lệnh.
 */
function segmentLine(times: number[], segments: Segment[]) {
  const owner = times.map(() => -1);
  segments.forEach((segment, segmentIndex) => {
    for (let index = segment.from; index <= segment.to; index++) owner[index] = segmentIndex;
  });
  const values = owner.map((segmentIndex) => (segmentIndex >= 0 ? segments[segmentIndex].price : null));
  return lineData(times, values, (index) => owner[index] >= 0 && owner[index] === owner[index + 1]);
}

/**
 * Vẽ từng lệnh: đường liền giá vào từ lúc mua đến lúc bán, đường đứt xanh = mức chốt lời, đỏ = mức cắt lỗ (tính từ
 * giá vào như lúc giả lập), và mũi tên ghi giá vào/ra cùng lãi/lỗ. Lệnh còn mở kéo tới cột cuối.
 */
export function addTradeOverlay(
  chart: IChartApi,
  candles: ISeriesApi<'Candlestick'>,
  bars: BacktestBar[],
  trades: OverlayTrade[],
  settings: BacktestSettings,
  showText: boolean,
) {
  const times = bars.map((bar) => bar.time);
  const entry: Segment[] = [];
  const takeProfit: Segment[] = [];
  const stopLoss: Segment[] = [];
  const markers: SeriesMarker<Time>[] = [];

  for (const trade of trades) {
    const from = barIndexAt(times, toUnix(trade.entryTimeUtc));
    const to = trade.exit ? barIndexAt(times, toUnix(trade.exit.exitTimeUtc)) : times.length - 1;
    if (from < 0 || to < 0) continue;

    entry.push({ from, to, price: trade.entryPrice });
    if (settings.takeProfitPercent != null)
      takeProfit.push({ from, to, price: trade.entryPrice * (1 + settings.takeProfitPercent / 100) });
    if (settings.stopLossPercent != null)
      stopLoss.push({ from, to, price: trade.entryPrice * (1 - settings.stopLossPercent / 100) });

    markers.push({
      time: times[from] as UTCTimestamp,
      position: 'belowBar',
      color: ENTRY_COLOR,
      shape: 'arrowUp',
      text: showText ? `BUY ${fmtPrice2(trade.entryPrice)}` : '',
    });
    if (trade.exit) {
      const reason = trade.exit.exitReason;
      markers.push({
        time: times[to] as UTCTimestamp,
        position: 'aboveBar',
        color: EXIT_COLORS[reason],
        shape: 'arrowDown',
        text: showText
          ? `${EXIT_TEXT[reason]} ${fmtPrice2(trade.exit.exitPrice)} · ${fmtSignedPercent(trade.exit.returnPercent)}`
          : '',
      });
    }
  }

  if (entry.length > 0) addLine(chart, segmentLine(times, entry), { color: ENTRY_COLOR });
  if (takeProfit.length > 0) addLine(chart, segmentLine(times, takeProfit), { color: TP_COLOR, lineStyle: LineStyle.Dashed });
  if (stopLoss.length > 0) addLine(chart, segmentLine(times, stopLoss), { color: SL_COLOR, lineStyle: LineStyle.Dashed });

  markers.sort((first, second) => (first.time as number) - (second.time as number));
  createSeriesMarkers(candles, markers);
}
