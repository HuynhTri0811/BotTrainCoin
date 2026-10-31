import { useState } from 'react';
import { Button, Card, Popconfirm, Table, Tabs, Tag, Typography, message } from 'antd';
import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { ApiError, api, futuresApi, type FuturesAccount, type FuturesPosition } from '../../api/client';
import { columnFilter, optionsFrom, picked, serverFilter } from '../../components/tableFilters';
import { formatVn } from '../../time';
import { fmtPrice, fmtSigned, fmtUsdt, pnlColor } from './format';

const sideTag = (position: FuturesPosition) => (
  <Tag color={position.side === 'Long' ? 'green' : 'red'}>
    {position.side} {position.leverage}x
  </Tag>
);

const reasonLabel: Record<string, { text: string; color: string }> = {
  Manual: { text: 'Đóng tay', color: 'default' },
  StopLoss: { text: 'Stop loss', color: 'orange' },
  TakeProfit: { text: 'Take profit', color: 'green' },
  Liquidation: { text: 'Thanh lý', color: 'red' },
  Signal: { text: 'Chỉ báo đảo chiều', color: 'purple' },
};

const SIDES = [
  { text: 'Long', value: 'Long' },
  { text: 'Short', value: 'Short' },
];

export function PositionsCard({ account }: { account: FuturesAccount | undefined }) {
  const queryClient = useQueryClient();
  const [filter, setFilter] = useState<{ symbol?: string; side?: string; closeReason?: string }>({});
  const history = useQuery({
    queryKey: ['futures-history', filter],
    queryFn: () => futuresApi.history(filter),
    placeholderData: keepPreviousData,
    refetchInterval: 5_000,
  });
  // Lấy symbol từ chiến lược: nếu lấy từ lịch sử thì lọc xong danh sách chỉ còn đúng symbol đang chọn.
  const status = useQuery({ queryKey: ['status'], queryFn: api.status });

  const close = useMutation({
    mutationFn: (id: number) => futuresApi.close(id, crypto.randomUUID()),
    onSuccess: (position) => {
      message.success(`Đã đóng ${position.side} ${position.symbol} @ ${fmtPrice(position.exitPrice)}, PnL ròng ${fmtSigned(position.netPnl)} USDT`);
      queryClient.invalidateQueries({ queryKey: ['futures-account'] });
      queryClient.invalidateQueries({ queryKey: ['futures-history'] });
    },
    onError: (error) => message.error(error instanceof ApiError ? error.message : String(error)),
  });

  const open = account?.openPositions ?? [];
  const closed = history.data ?? [];

  return (
    <Card size="small">
      <Tabs
        size="small"
        items={[
          {
            key: 'open',
            label: `Vị thế mở (${open.length})`,
            children: (
              <Table<FuturesPosition>
                rowKey="id"
                size="small"
                dataSource={open}
                pagination={false}
                scroll={{ x: true }}
                locale={{ emptyText: 'Chưa có vị thế' }}
                columns={[
                  { title: 'Symbol', dataIndex: 'symbol' },
                  { title: 'Hướng', key: 'side', render: (_, position) => sideTag(position) },
                  { title: 'KL', dataIndex: 'quantity', align: 'right' },
                  { title: 'Giá vào', dataIndex: 'entryPrice', align: 'right', render: fmtPrice },
                  { title: 'Mark', dataIndex: 'markPrice', align: 'right', render: fmtPrice },
                  {
                    title: 'Thanh lý',
                    dataIndex: 'liquidationPrice',
                    align: 'right',
                    render: (value: number) => <span style={{ color: '#dc2626' }}>{fmtPrice(value)}</span>,
                  },
                  {
                    title: 'SL / TP',
                    key: 'sltp',
                    align: 'right',
                    render: (_, position) => `${fmtPrice(position.stopLoss)} / ${fmtPrice(position.takeProfit)}`,
                  },
                  { title: 'Margin', dataIndex: 'margin', align: 'right', render: fmtUsdt },
                  {
                    title: 'uPnL (ROE)',
                    key: 'upnl',
                    align: 'right',
                    render: (_, position) => (
                      <span style={{ color: pnlColor(position.unrealizedPnl) }}>
                        {fmtSigned(position.unrealizedPnl)}
                        {position.roePercent != null && ` (${position.roePercent.toFixed(2)}%)`}
                      </span>
                    ),
                  },
                  {
                    title: '',
                    key: 'close',
                    render: (_, position) => (
                      <Popconfirm
                        title={`Đóng ${position.side} ${position.symbol}?`}
                        description={`Lệnh market giả lập tại khoảng ${fmtPrice(position.markPrice)}.`}
                        okText="Đóng vị thế"
                        cancelText="Hủy"
                        onConfirm={() => close.mutateAsync(position.id)}
                      >
                        <Button size="small" danger loading={close.isPending && close.variables === position.id}>
                          Đóng
                        </Button>
                      </Popconfirm>
                    ),
                  },
                ]}
              />
            ),
          },
          {
            key: 'history',
            label: 'Lịch sử',
            children: (
              <Table<FuturesPosition>
                rowKey="id"
                size="small"
                loading={history.isPending}
                dataSource={closed}
                pagination={{ pageSize: 10, hideOnSinglePage: true }}
                scroll={{ x: true }}
                onChange={(_, filters) =>
                  setFilter({ symbol: picked(filters.symbol), side: picked(filters.side), closeReason: picked(filters.closeReason) })
                }
                columns={[
                  { title: 'Đóng lúc', dataIndex: 'closedAtUtc', render: formatVn },
                  {
                    title: 'Symbol',
                    dataIndex: 'symbol',
                    ...serverFilter(optionsFrom(status.data?.strategies ?? [], (strategy) => strategy.symbol)),
                  },
                  {
                    title: 'Hướng',
                    key: 'side',
                    ...serverFilter(SIDES),
                    render: (_, position) => sideTag(position),
                  },
                  {
                    title: 'Lý do',
                    dataIndex: 'closeReason',
                    ...serverFilter(Object.entries(reasonLabel).map(([value, { text }]) => ({ text, value }))),
                    render: (reason: string) => <Tag color={reasonLabel[reason]?.color}>{reasonLabel[reason]?.text ?? reason}</Tag>,
                  },
                  { title: 'Giá vào', dataIndex: 'entryPrice', align: 'right', render: fmtPrice },
                  { title: 'Giá ra', dataIndex: 'exitPrice', align: 'right', render: fmtPrice },
                  {
                    title: 'PnL giá',
                    dataIndex: 'realizedPnl',
                    align: 'right',
                    render: (value: number) => <span style={{ color: pnlColor(value) }}>{fmtSigned(value)}</span>,
                  },
                  {
                    title: 'Phí',
                    key: 'fees',
                    align: 'right',
                    render: (_, position) => fmtSigned(-(position.openFee + position.closeFee)),
                  },
                  {
                    title: 'PnL ròng',
                    dataIndex: 'netPnl',
                    ...columnFilter<FuturesPosition>((position) => ((position.netPnl ?? 0) >= 0 ? 'Win' : 'Loss'), [
                      { text: 'Lời', value: 'Win' },
                      { text: 'Lỗ', value: 'Loss' },
                    ]),
                    align: 'right',
                    render: (value: number) => (
                      <Typography.Text strong style={{ color: pnlColor(value) }}>
                        {fmtSigned(value)}
                      </Typography.Text>
                    ),
                  },
                ]}
              />
            ),
          },
        ]}
      />
    </Card>
  );
}
