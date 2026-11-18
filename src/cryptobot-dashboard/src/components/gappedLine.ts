import type { AutoscaleInfo, IChartApi, LineData, UTCTimestamp } from 'lightweight-charts';

/** Màu của đoạn ẩn. */
export const HIDDEN = 'transparent';

/** Dữ liệu một series đường có đoạn hiện, đoạn ẩn; `shown` đánh dấu điểm đang hiện, dùng để tính thang giá. */
export interface GappedLine {
  data: LineData[];
  shown: boolean[];
}

/**
 * Dữ liệu cho một series có đoạn hiện, đoạn ẩn. Lightweight Charts 5 nối mọi điểm (khoảng trống, kể cả điểm chỉ có
 * `time`, không làm đứt đường) và tô đoạn i → i+1 bằng màu của điểm i, nên đoạn i → i+1 chỉ hiện khi `connects(i)`;
 * điểm không có giá trị lấy giá của điểm có giá trị gần nhất phía trước (hoặc phía sau) để đoạn ẩn nằm ngang.
 */
export function gappedLineData(
  times: number[],
  values: (number | null)[],
  connects: (index: number) => boolean,
): GappedLine {
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

/**
 * Khoảng giá của các điểm đang hiện trong khoảng thời gian đang xem; null nếu không có (thang giá theo series khác).
 * So theo thời gian chứ không theo chỉ số cột, vì series có thể ngắn hơn trục thời gian của chart.
 */
export function visibleRange(chart: IChartApi, line: GappedLine): AutoscaleInfo | null {
  const range = chart.timeScale().getVisibleRange();
  const from = range ? (range.from as number) : -Infinity;
  const to = range ? (range.to as number) : Infinity;
  let minValue = Infinity;
  let maxValue = -Infinity;
  line.data.forEach((point, index) => {
    const time = point.time as number;
    if (!line.shown[index] || time < from || time > to) return;
    minValue = Math.min(minValue, point.value);
    maxValue = Math.max(maxValue, point.value);
  });
  return minValue <= maxValue ? { priceRange: { minValue, maxValue } } : null;
}
