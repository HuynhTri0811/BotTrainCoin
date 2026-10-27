import type { CSSProperties, ReactNode } from 'react';
import { Tooltip } from 'antd';
import type { MonthChange, MonthEvent, MonthlyHistory, MonthStats, YearChange } from '../../api/client';
import { fmtPrice2 as fmtPrice } from '../futures/format';

export const MONTHS = Array.from({ length: 12 }, (_, index) => index + 1);

/** Từ mức này trở lên ô có màu đậm nhất; crypto hay có tháng ±30%. */
const FULL_COLOR_PERCENT = 30;

export const fmtPct = (value: number) => `${value > 0 ? '+' : ''}${value.toFixed(1)}%`;

export function pctColor(value: number) {
  return value > 0 ? 'var(--up)' : value < 0 ? 'var(--down)' : undefined;
}

/** Sự kiện gom theo khóa `năm-tháng`. */
export function eventsByMonth(events: MonthEvent[]) {
  const map = new Map<string, MonthEvent[]>();
  for (const monthEvent of events) {
    const key = `${monthEvent.year}-${monthEvent.month}`;
    map.set(key, [...(map.get(key) ?? []), monthEvent]);
  }
  return map;
}

function cellBackground(value: number) {
  const alpha = 0.08 + 0.62 * Math.min(Math.abs(value) / FULL_COLOR_PERCENT, 1);
  const pct = (alpha * 100).toFixed(1);
  return `color-mix(in srgb, var(${value >= 0 ? '--up' : '--down'}) ${pct}%, transparent)`;
}

const border = '1px solid var(--border)';
const th: CSSProperties = { padding: '6px 8px', fontWeight: 600, fontSize: 12, background: 'var(--surface-subtle)', border, whiteSpace: 'nowrap' };
const td: CSSProperties = {
  padding: '6px 8px',
  fontSize: 12,
  textAlign: 'right',
  border,
  whiteSpace: 'nowrap',
  fontVariantNumeric: 'tabular-nums',
};

function PriceTip({ open, close, extra }: { open: number; close: number; extra?: ReactNode }) {
  return (
    <div style={{ fontSize: 12 }}>
      Mở {fmtPrice(open)} → Đóng {fmtPrice(close)}
      <br />
      Chênh lệch {close - open > 0 ? '+' : ''}
      {fmtPrice(close - open)}
      {extra}
    </div>
  );
}

function MonthCell({ change, events }: { change: MonthChange | undefined; events: MonthEvent[] | undefined }) {
  if (!change) return <td style={{ ...td, color: 'var(--faint-2)' }}>—</td>;
  return (
    <Tooltip
      title={
        <PriceTip
          open={change.open}
          close={change.close}
          extra={
            <>
              <br />
              {change.days} ngày có nến{change.complete ? '' : ' · tháng đang chạy, giá đóng là giá gần nhất'}
              {events?.map((monthEvent) => (
                <div key={monthEvent.title} style={{ marginTop: 4 }}>
                  ● {monthEvent.title}
                </div>
              ))}
            </>
          }
        />
      }
    >
      <td
        style={{
          ...td,
          background: cellBackground(change.changePercent),
          fontStyle: change.complete ? undefined : 'italic',
          outline: change.complete ? undefined : '1px dashed var(--text-muted)',
          outlineOffset: -3,
          position: 'relative',
        }}
      >
        {events && (
          <span
            aria-label="Có sự kiện"
            style={{ position: 'absolute', top: 2, left: 3, width: 6, height: 6, borderRadius: 3, background: 'var(--accent)' }}
          />
        )}
        {fmtPct(change.changePercent)}
        {change.complete ? '' : '*'}
      </td>
    </Tooltip>
  );
}

function YearCell({ year }: { year: YearChange | undefined }) {
  if (!year) return <td style={td}>—</td>;
  return (
    <Tooltip title={<PriceTip open={year.open} close={year.close} extra={year.complete ? null : <><br />Năm chưa trọn</>} />}>
      <td style={{ ...td, fontWeight: 600, color: pctColor(year.changePercent), borderLeft: '2px solid var(--faint)' }}>
        {fmtPct(year.changePercent)}
        {year.complete ? '' : '*'}
      </td>
    </Tooltip>
  );
}

function StatsRow({
  label,
  stats,
  render,
}: {
  label: string;
  stats: Map<number, MonthStats>;
  render: (stats: MonthStats) => ReactNode;
}) {
  return (
    <tr>
      <th style={{ ...th, textAlign: 'left' }}>{label}</th>
      {MONTHS.map((month) => {
        const monthStats = stats.get(month);
        return (
          <td key={month} style={{ ...td, background: 'var(--surface-subtle)' }}>
            {monthStats ? render(monthStats) : '—'}
          </td>
        );
      })}
      <td style={{ ...td, background: 'var(--surface-subtle)', borderLeft: '2px solid var(--faint)' }} />
    </tr>
  );
}

/** Bảng năm × tháng: mỗi ô là % chênh lệch từ giá mở cửa đầu tháng đến giá đóng cửa cuối tháng. */
export function MonthlyHeatmap({ data }: { data: MonthlyHistory }) {
  const byKey = new Map(data.months.map((change) => [`${change.year}-${change.month}`, change]));
  const years = new Map(data.years.map((pointY) => [pointY.year, pointY]));
  const stats = new Map(data.monthStats.map((monthStats) => [monthStats.month, monthStats]));
  const events = eventsByMonth(data.events);

  return (
    <div style={{ overflowX: 'auto' }}>
      <table style={{ borderCollapse: 'collapse', width: '100%', minWidth: 860 }}>
        <thead>
          <tr>
            <th style={{ ...th, textAlign: 'left' }}>Năm</th>
            {MONTHS.map((month) => (
              <th key={month} style={th}>
                T{month}
              </th>
            ))}
            <th style={{ ...th, borderLeft: '2px solid var(--faint)' }}>Cả năm</th>
          </tr>
        </thead>
        <tbody>
          {data.years.map(({ year }) => (
            <tr key={year}>
              <th style={{ ...th, textAlign: 'left' }}>{year}</th>
              {MONTHS.map((month) => (
                <MonthCell key={month} change={byKey.get(`${year}-${month}`)} events={events.get(`${year}-${month}`)} />
              ))}
              <YearCell year={years.get(year)} />
            </tr>
          ))}
        </tbody>
        <tfoot>
          <StatsRow
            label="Trung bình"
            stats={stats}
            render={(stats) => (
              <span style={{ fontWeight: 600, color: pctColor(stats.averageChangePercent) }}>
                {fmtPct(stats.averageChangePercent)}
              </span>
            )}
          />
          <StatsRow
            label="Số năm tăng"
            stats={stats}
            render={(stats) => (
              <Tooltip title={`${stats.upPercent.toFixed(0)}% số năm tháng này đóng cao hơn mở`}>
                <span>
                  {stats.upCount}/{stats.years}
                </span>
              </Tooltip>
            )}
          />
          <StatsRow
            label="Tốt nhất"
            stats={stats}
            render={(stats) => <span style={{ color: pctColor(stats.bestChangePercent) }}>{fmtPct(stats.bestChangePercent)}</span>}
          />
          <StatsRow
            label="Tệ nhất"
            stats={stats}
            render={(stats) => <span style={{ color: pctColor(stats.worstChangePercent) }}>{fmtPct(stats.worstChangePercent)}</span>}
          />
        </tfoot>
      </table>
    </div>
  );
}
