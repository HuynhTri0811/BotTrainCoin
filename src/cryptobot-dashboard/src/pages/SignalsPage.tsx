import { Card, Table, Tag } from 'antd';
import { useQuery } from '@tanstack/react-query';
import { api, type Signal, type Strategy } from '../api/client';
import { columnFilter } from '../components/tableFilters';
import { formatVn } from '../time';

const priceFormat = new Intl.NumberFormat('en-US', { maximumFractionDigits: 8 });

/** Tín hiệu Buy/Sell gần đây của chiến lược EMA đang chọn. */
export function SignalsPage({ strategy }: { strategy: Strategy }) {
  const signals = useQuery({
    queryKey: ['signals', strategy.id],
    queryFn: () => api.signals(strategy.id, 50),
    refetchInterval: 15_000,
  });

  const fastLabel = `EMA${strategy.parameters.fastPeriod}`;
  const slowLabel = `EMA${strategy.parameters.slowPeriod}`;

  return (
    <Card size="small" title={`Tín hiệu gần đây · ${strategy.symbol} ${strategy.interval}`}>
      <Table<Signal>
        rowKey="id"
        size="small"
        loading={signals.isPending}
        dataSource={signals.data ?? []}
        pagination={{ pageSize: 10, hideOnSinglePage: true }}
        scroll={{ x: true }}
        columns={[
          {
            title: 'Hành động',
            dataIndex: 'action',
            ...columnFilter<Signal>((s) => s.action, [
              { text: 'BUY', value: 'Buy' },
              { text: 'SELL', value: 'Sell' },
            ]),
            render: (a: Signal['action']) => <Tag color={a === 'Buy' ? 'green' : 'red'}>{a.toUpperCase()}</Tag>,
          },
          { title: 'Nến (mở)', dataIndex: 'candleOpenTimeUtc', render: formatVn },
          { title: 'Close', dataIndex: 'candleClose', align: 'right', render: (v: number) => priceFormat.format(v) },
          {
            title: `${fastLabel} / ${slowLabel}`,
            key: 'ema',
            align: 'right',
            render: (_, s) => `${priceFormat.format(s.fastEma)} / ${priceFormat.format(s.slowEma)}`,
          },
          { title: 'Lý do', dataIndex: 'reason' },
          {
            title: 'Loại',
            dataIndex: 'isBackfill',
            ...columnFilter<Signal>((s) => (s.isBackfill ? 'Backfill' : 'New'), [
              { text: 'Mới', value: 'New' },
              { text: 'Backfill', value: 'Backfill' },
            ]),
            render: (b: boolean) =>
              b ? <Tag>Backfill</Tag> : <Tag color="processing">Mới</Tag>,
          },
          { title: 'Tạo lúc', dataIndex: 'createdAtUtc', render: formatVn },
        ]}
      />
    </Card>
  );
}
