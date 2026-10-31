import { useState } from 'react';
import { Card, Segmented, Space, Table, Tag, Typography } from 'antd';
import type { MonthChange, MonthlyHistory } from '../../api/client';
import { fmtPrice2 as fmtPrice } from '../futures/format';
import { eventsByMonth, fmtPct, pctColor } from './MonthlyHeatmap';

type Direction = 'drop' | 'rise';

/** Ngưỡng % theo độ lớn; giảm là âm, tăng là dương. */
const THRESHOLDS = [10, 15, 20, 30];

/** Các tháng giảm (hoặc tăng) từ ngưỡng trở lên, kèm sự kiện thị trường cùng tháng để xem lại. */
export function MoveEventsCard({ data }: { data: MonthlyHistory }) {
  const [direction, setDirection] = useState<Direction>('drop');
  const [threshold, setThreshold] = useState(20);
  const rise = direction === 'rise';
  const events = eventsByMonth(data.events);
  // Tháng biến động mạnh nhất đứng đầu.
  const moves = data.months
    .filter((change) => (rise ? change.changePercent >= threshold : change.changePercent <= -threshold))
    .sort((first, second) => (rise ? second.changePercent - first.changePercent : first.changePercent - second.changePercent));
  const withEvents = moves.filter((change) => events.has(`${change.year}-${change.month}`)).length;
  const coin = data.symbol.replace(/USD$/, '');

  return (
    <Card
      size="small"
      title={`Tháng biến động mạnh và sự kiện · ${coin}`}
      extra={
        <Space size="small" wrap>
          <Segmented<Direction>
            size="small"
            value={direction}
            onChange={setDirection}
            options={[
              { value: 'drop', label: 'Giảm mạnh' },
              { value: 'rise', label: 'Tăng mạnh' },
            ]}
          />
          <Segmented<number>
            size="small"
            value={threshold}
            onChange={setThreshold}
            options={THRESHOLDS.map((threshold) => ({ value: threshold, label: rise ? `≥ +${threshold}%` : `≤ −${threshold}%` }))}
          />
        </Space>
      }
    >
      <Typography.Paragraph style={{ margin: '0 0 8px' }}>
        {moves.length} tháng {coin} {rise ? 'tăng' : 'giảm'} từ {threshold}% trở lên; {withEvents} tháng có sự kiện ghi
        nhận.
      </Typography.Paragraph>
      <Table<MonthChange>
        rowKey={(change) => `${change.year}-${change.month}`}
        size="small"
        dataSource={moves}
        pagination={{ pageSize: 10, hideOnSinglePage: true, size: 'small' }}
        scroll={{ x: 'max-content' }}
        columns={[
          {
            title: 'Tháng',
            key: 'month',
            render: (_, change) => `${String(change.month).padStart(2, '0')}/${change.year}${change.complete ? '' : '*'}`,
          },
          {
            title: 'Thay đổi',
            dataIndex: 'changePercent',
            align: 'right',
            render: (value: number) => (
              <span style={{ fontWeight: 600, color: pctColor(value), fontVariantNumeric: 'tabular-nums' }}>{fmtPct(value)}</span>
            ),
          },
          {
            title: 'Mở → Đóng',
            key: 'prices',
            align: 'right',
            render: (_, change) => (
              <span style={{ fontVariantNumeric: 'tabular-nums' }}>
                {fmtPrice(change.open)} → {fmtPrice(change.close)}
              </span>
            ),
          },
          {
            title: 'Sự kiện cùng tháng',
            key: 'events',
            render: (_, change) => {
              const list = events.get(`${change.year}-${change.month}`);
              if (!list)
                return (
                  <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                    Chưa ghi nhận sự kiện
                  </Typography.Text>
                );
              return (
                <div style={{ display: 'grid', gap: 4, maxWidth: 560, whiteSpace: 'normal' }}>
                  {list.map((monthEvent) => (
                    <div key={monthEvent.title} style={{ fontSize: 12 }}>
                      <Typography.Text strong>{monthEvent.title}</Typography.Text>
                      {!monthEvent.marketWide && (
                        <Tag style={{ marginLeft: 6 }} color="purple">
                          riêng {coin}
                        </Tag>
                      )}
                      {monthEvent.detail && <div style={{ color: '#475569' }}>{monthEvent.detail}</div>}
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
        tháng, không khẳng định là nguyên nhân giá tăng hay giảm. Muốn thêm hoặc sửa, mở file đó rồi restart API.
      </Typography.Paragraph>
    </Card>
  );
}
