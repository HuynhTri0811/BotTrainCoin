import type { CSSProperties, ReactNode } from 'react';
import { Tooltip } from 'antd';
import type { MonthChange, MonthEvent, MonthlyHistory, MonthStats, YearChange } from '../../api/client';
import { fmtPrice2 as fmtPrice } from '../futures/format';

export const MONTHS = Array.from({ length: 12 }, (_, i) => i + 1);

/** Từ mức này trở lên ô có màu đậm nhất; crypto hay có tháng ±30%. */
const FULL_COLOR_PERCENT = 30;

export const fmtPct = (v: number) => `${v > 0 ? '+' : ''}${v.toFixed(1)}%`;

export function pctColor(v: number) {
  return v > 0 ? 'var(--up)' : v < 0 ? 'var(--down)' : undefined;
}

/** Sự kiện gom theo khóa `năm-tháng`. */
export function eventsByMonth(events: MonthEvent[]) {
  const map = new Map<string, MonthEvent[]>();
  for (const e of events) {
    const key = `${e.year}-${e.month}`;
    map.set(key, [...(map.get(key) ?? []), e]);
  }
  return map;
}

function cellBackground(v: number) {
  const alpha = 0.08 + 0.62 * Math.min(Math.abs(v) / FULL_COLOR_PERCENT, 1);
  const pct = (alpha * 100).toFixed(1);
  return `color-mix(in srgb, var(${v >= 0 ? '--up' : '--down'}) ${pct}%, transparent)`;
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

function MonthCell({ m, events }: { m: MonthChange | undefined; events: MonthEvent[] | undefined }) {
  if (!m) return <td style={{ ...td, color: 'var(--faint-2)' }}>—</td>;
  return (
    <Tooltip
      title={
        <PriceTip
          open={m.open}
          close={m.close}
          extra={
            <>
              <br />
              {m.days} ngày có nến{m.complete ? '' : ' · tháng đang chạy, giá đóng là giá gần nhất'}
              {events?.map((e) => (
                <div key={e.title} style={{ marginTop: 4 }}>
                  ● {e.title}
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
          background: cellBackground(m.changePercent),
          fontStyle: m.complete ? undefined : 'italic',
          outline: m.complete ? undefined : '1px dashed var(--text-muted)',
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
        {fmtPct(m.changePercent)}
        {m.complete ? '' : '*'}
      </td>
    </Tooltip>
  );
}

function YearCell({ y }: { y: YearChange | undefined }) {
  if (!y) return <td style={td}>—</td>;
  return (
    <Tooltip title={<PriceTip open={y.open} close={y.close} extra={y.complete ? null : <><br />Năm chưa trọn</>} />}>
      <td style={{ ...td, fontWeight: 600, color: pctColor(y.changePercent), borderLeft: '2px solid var(--faint)' }}>
        {fmtPct(y.changePercent)}
        {y.complete ? '' : '*'}
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
  render: (s: MonthStats) => ReactNode;
}) {
  return (
    <tr>
      <th style={{ ...th, textAlign: 'left' }}>{label}</th>
      {MONTHS.map((month) => {
        const s = stats.get(month);
        return (
          <td key={month} style={{ ...td, background: 'var(--surface-subtle)' }}>
            {s ? render(s) : '—'}
          </td>
        );
      })}
      <td style={{ ...td, background: 'var(--surface-subtle)', borderLeft: '2px solid var(--faint)' }} />
    </tr>
  );
}

/** Bảng năm × tháng: mỗi ô là % chênh lệch từ giá mở cửa đầu tháng đến giá đóng cửa cuối tháng. */
export function MonthlyHeatmap({ data }: { data: MonthlyHistory }) {
  const byKey = new Map(data.months.map((m) => [`${m.year}-${m.month}`, m]));
  const years = new Map(data.years.map((y) => [y.year, y]));
  const stats = new Map(data.monthStats.map((s) => [s.month, s]));
  const events = eventsByMonth(data.events);

  return (
    <div style={{ overflowX: 'auto' }}>
      <table style={{ borderCollapse: 'collapse', width: '100%', minWidth: 860 }}>
        <thead>
          <tr>
            <th style={{ ...th, textAlign: 'left' }}>Năm</th>
            {MONTHS.map((m) => (
              <th key={m} style={th}>
                T{m}
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
                <MonthCell key={month} m={byKey.get(`${year}-${month}`)} events={events.get(`${year}-${month}`)} />
              ))}
              <YearCell y={years.get(year)} />
            </tr>
          ))}
        </tbody>
        <tfoot>
          <StatsRow
            label="Trung bình"
            stats={stats}
            render={(s) => (
              <span style={{ fontWeight: 600, color: pctColor(s.averageChangePercent) }}>
                {fmtPct(s.averageChangePercent)}
              </span>
            )}
          />
          <StatsRow
            label="Số năm tăng"
            stats={stats}
            render={(s) => (
              <Tooltip title={`${s.upPercent.toFixed(0)}% số năm tháng này đóng cao hơn mở`}>
                <span>
                  {s.upCount}/{s.years}
                </span>
              </Tooltip>
            )}
          />
          <StatsRow
            label="Tốt nhất"
            stats={stats}
            render={(s) => <span style={{ color: pctColor(s.bestChangePercent) }}>{fmtPct(s.bestChangePercent)}</span>}
          />
          <StatsRow
            label="Tệ nhất"
            stats={stats}
            render={(s) => <span style={{ color: pctColor(s.worstChangePercent) }}>{fmtPct(s.worstChangePercent)}</span>}
          />
        </tfoot>
      </table>
    </div>
  );
}
