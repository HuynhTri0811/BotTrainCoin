import type { CSSProperties, ReactNode } from 'react';
import { Tooltip } from 'antd';
import type { MonthChange, MonthlyHistory, MonthStats, YearChange } from '../../api/client';
import { fmtPrice2 as fmtPrice } from '../futures/format';

export const MONTHS = Array.from({ length: 12 }, (_, i) => i + 1);

/** Từ mức này trở lên ô có màu đậm nhất; crypto hay có tháng ±30%. */
const FULL_COLOR_PERCENT = 30;

export const fmtPct = (v: number) => `${v > 0 ? '+' : ''}${v.toFixed(1)}%`;

export function pctColor(v: number) {
  return v > 0 ? '#16a34a' : v < 0 ? '#dc2626' : undefined;
}

function cellBackground(v: number) {
  const alpha = 0.08 + 0.62 * Math.min(Math.abs(v) / FULL_COLOR_PERCENT, 1);
  return v >= 0 ? `rgba(22, 163, 74, ${alpha.toFixed(3)})` : `rgba(220, 38, 38, ${alpha.toFixed(3)})`;
}

const border = '1px solid #e5e7eb';
const th: CSSProperties = { padding: '6px 8px', fontWeight: 600, fontSize: 12, background: '#f8fafc', border, whiteSpace: 'nowrap' };
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

function MonthCell({ m }: { m: MonthChange | undefined }) {
  if (!m) return <td style={{ ...td, color: '#cbd5e1' }}>—</td>;
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
          outline: m.complete ? undefined : '1px dashed #64748b',
          outlineOffset: -3,
        }}
      >
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
      <td style={{ ...td, fontWeight: 600, color: pctColor(y.changePercent), borderLeft: '2px solid #94a3b8' }}>
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
          <td key={month} style={{ ...td, background: '#f8fafc' }}>
            {s ? render(s) : '—'}
          </td>
        );
      })}
      <td style={{ ...td, background: '#f8fafc', borderLeft: '2px solid #94a3b8' }} />
    </tr>
  );
}

/** Bảng năm × tháng: mỗi ô là % chênh lệch từ giá mở cửa đầu tháng đến giá đóng cửa cuối tháng. */
export function MonthlyHeatmap({ data }: { data: MonthlyHistory }) {
  const byKey = new Map(data.months.map((m) => [`${m.year}-${m.month}`, m]));
  const years = new Map(data.years.map((y) => [y.year, y]));
  const stats = new Map(data.monthStats.map((s) => [s.month, s]));

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
            <th style={{ ...th, borderLeft: '2px solid #94a3b8' }}>Cả năm</th>
          </tr>
        </thead>
        <tbody>
          {data.years.map(({ year }) => (
            <tr key={year}>
              <th style={{ ...th, textAlign: 'left' }}>{year}</th>
              {MONTHS.map((month) => (
                <MonthCell key={month} m={byKey.get(`${year}-${month}`)} />
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
