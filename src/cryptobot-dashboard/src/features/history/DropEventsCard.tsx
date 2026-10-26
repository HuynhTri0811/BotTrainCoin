import { useState } from 'react';
import { Card, Segmented, Table, Tag, Typography } from 'antd';
import type { MonthChange, MonthlyHistory } from '../../api/client';
import { fmtPrice2 as fmtPrice } from '../futures/format';
import { eventsByMonth, fmtPct, pctColor } from './MonthlyHeatmap';

const THRESHOLDS = [-10, -15, -20, -30];

/** Các tháng giảm từ ngưỡng trở xuống, kèm sự kiện thị trường cùng tháng để xem lại. */
export function DropEventsCard({ data }: { data: MonthlyHistory }) {
  const [threshold, setThreshold] = useState(-20);
  const events = eventsByMonth(data.events);
  const drops = data.months
    .filter((m) => m.changePercent <= threshold)
    .sort((a, b) => a.changePercent - b.changePercent);
  const withEvents = drops.filter((m) => events.has(`${m.year}-${m.month}`)).length;
  const coin = data.symbol.replace(/USD$/, '');

  return (
    <Card
      size="small"
      title={`Tháng giảm mạnh và sự kiện · ${coin}`}
      extra={
        <Segmented<number>
          size="small"
          value={threshold}
          onChange={setThreshold}
          options={THRESHOLDS.map((t) => ({ value: t, label: `≤ ${t}%` }))}
        />
      }
    >
      <Typography.Paragraph style={{ margin: '0 0 8px' }}>
        {drops.length} tháng {coin} giảm từ {Math.abs(threshold)}% trở lên; {withEvents} tháng có sự kiện ghi nhận.
      </Typography.Paragraph>
      <Table<MonthChange>
        rowKey={(m) => `${m.year}-${m.month}`}
        size="small"
        dataSource={drops}
        pagination={{ pageSize: 10, hideOnSinglePage: true, size: 'small' }}
        scroll={{ x: 'max-content' }}
        columns={[
          {
            title: 'Tháng',
            key: 'month',
            render: (_, m) => `${String(m.month).padStart(2, '0')}/${m.year}${m.complete ? '' : '*'}`,
          },
          {
            title: 'Thay đổi',
            dataIndex: 'changePercent',
            align: 'right',
            render: (v: number) => (
              <span style={{ fontWeight: 600, color: pctColor(v), fontVariantNumeric: 'tabular-nums' }}>{fmtPct(v)}</span>
            ),
          },
          {
            title: 'Mở → Đóng',
            key: 'prices',
            align: 'right',
            render: (_, m) => (
              <span style={{ fontVariantNumeric: 'tabular-nums' }}>
                {fmtPrice(m.open)} → {fmtPrice(m.close)}
              </span>
            ),
          },
          {
            title: 'Sự kiện cùng tháng',
            key: 'events',
            render: (_, m) => {
              const list = events.get(`${m.year}-${m.month}`);
              if (!list)
                return (
                  <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                    Chưa ghi nhận sự kiện
                  </Typography.Text>
                );
              return (
                <div style={{ display: 'grid', gap: 4, maxWidth: 560, whiteSpace: 'normal' }}>
                  {list.map((e) => (
                    <div key={e.title} style={{ fontSize: 12 }}>
                      <Typography.Text strong>{e.title}</Typography.Text>
                      {!e.marketWide && (
                        <Tag style={{ marginLeft: 6 }} color="purple">
                          riêng {coin}
                        </Tag>
                      )}
                      {e.detail && <div style={{ color: '#475569' }}>{e.detail}</div>}
                    </div>
                  ))}
                </div>
              );
            },
          },
        ]}
      />
      <Typography.Paragraph type="secondary" style={{ margin: '8px 0 0', fontSize: 12 }}>
        Sự kiện được ghi tay trong <code>src/CryptoBot.Api/marketevents.json</code> và chỉ là những gì xảy ra cùng
        tháng, không khẳng định là nguyên nhân giá giảm. Muốn thêm hoặc sửa, mở file đó rồi restart API.
      </Typography.Paragraph>
    </Card>
  );
}
