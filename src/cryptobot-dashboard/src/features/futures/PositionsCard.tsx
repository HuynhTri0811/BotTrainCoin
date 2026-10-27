import { Button, Card, Popconfirm, Table, Tabs, Tag, Typography, message } from 'antd';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { ApiError, futuresApi, type FuturesAccount, type FuturesPosition } from '../../api/client';
import { formatVn } from '../../time';
import { fmtPrice, fmtSigned, fmtUsdt, pnlColor } from './format';

const sideTag = (p: FuturesPosition) => (
  <Tag color={p.side === 'Long' ? 'green' : 'red'}>
    {p.side} {p.leverage}x
  </Tag>
);

const reasonLabel: Record<string, { text: string; color: string }> = {
  Manual: { text: 'Đóng tay', color: 'default' },
  StopLoss: { text: 'Stop loss', color: 'orange' },
  TakeProfit: { text: 'Take profit', color: 'green' },
  Liquidation: { text: 'Thanh lý', color: 'red' },
  Signal: { text: 'Chỉ báo đảo chiều', color: 'purple' },
};

export function PositionsCard({ account }: { account: FuturesAccount | undefined }) {
  const queryClient = useQueryClient();
  const history = useQuery({
    queryKey: ['futures-history'],
    queryFn: () => futuresApi.history(50),
    refetchInterval: 5_000,
  });

  const close = useMutation({
    mutationFn: (id: number) => futuresApi.close(id, crypto.randomUUID()),
    onSuccess: (p) => {
      message.success(`Đã đóng ${p.side} ${p.symbol} @ ${fmtPrice(p.exitPrice)}, PnL ròng ${fmtSigned(p.netPnl)} USDT`);
      queryClient.invalidateQueries({ queryKey: ['futures-account'] });
      queryClient.invalidateQueries({ queryKey: ['futures-history'] });
    },
    onError: (e) => message.error(e instanceof ApiError ? e.message : String(e)),
  });

  const open = account?.openPositions ?? [];

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
                  { title: 'Hướng', key: 'side', render: (_, p) => sideTag(p) },
                  { title: 'KL', dataIndex: 'quantity', align: 'right' },
                  { title: 'Giá vào', dataIndex: 'entryPrice', align: 'right', render: fmtPrice },
                  { title: 'Mark', dataIndex: 'markPrice', align: 'right', render: fmtPrice },
                  {
                    title: 'Thanh lý',
                    dataIndex: 'liquidationPrice',
                    align: 'right',
                    render: (v: number) => <span style={{ color: 'var(--down)' }}>{fmtPrice(v)}</span>,
                  },
                  {
                    title: 'SL / TP',
                    key: 'sltp',
                    align: 'right',
                    render: (_, p) => `${fmtPrice(p.stopLoss)} / ${fmtPrice(p.takeProfit)}`,
                  },
                  { title: 'Margin', dataIndex: 'margin', align: 'right', render: fmtUsdt },
                  {
                    title: 'uPnL (ROE)',
                    key: 'upnl',
                    align: 'right',
                    render: (_, p) => (
                      <span style={{ color: pnlColor(p.unrealizedPnl) }}>
                        {fmtSigned(p.unrealizedPnl)}
                        {p.roePercent != null && ` (${p.roePercent.toFixed(2)}%)`}
                      </span>
                    ),
                  },
                  {
                    title: '',
                    key: 'close',
                    render: (_, p) => (
                      <Popconfirm
                        title={`Đóng ${p.side} ${p.symbol}?`}
                        description={`Lệnh market giả lập tại khoảng ${fmtPrice(p.markPrice)}.`}
                        okText="Đóng vị thế"
                        cancelText="Hủy"
                        onConfirm={() => close.mutateAsync(p.id)}
                      >
                        <Button size="small" danger loading={close.isPending && close.variables === p.id}>
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
                dataSource={history.data ?? []}
                pagination={{ pageSize: 10, hideOnSinglePage: true }}
                scroll={{ x: true }}
                columns={[
                  { title: 'Đóng lúc', dataIndex: 'closedAtUtc', render: formatVn },
                  { title: 'Symbol', dataIndex: 'symbol' },
                  { title: 'Hướng', key: 'side', render: (_, p) => sideTag(p) },
                  {
                    title: 'Lý do',
                    dataIndex: 'closeReason',
                    render: (r: string) => <Tag color={reasonLabel[r]?.color}>{reasonLabel[r]?.text ?? r}</Tag>,
                  },
                  { title: 'Giá vào', dataIndex: 'entryPrice', align: 'right', render: fmtPrice },
                  { title: 'Giá ra', dataIndex: 'exitPrice', align: 'right', render: fmtPrice },
                  {
                    title: 'PnL giá',
                    dataIndex: 'realizedPnl',
                    align: 'right',
                    render: (v: number) => <span style={{ color: pnlColor(v) }}>{fmtSigned(v)}</span>,
                  },
                  {
                    title: 'Phí',
                    key: 'fees',
                    align: 'right',
                    render: (_, p) => fmtSigned(-(p.openFee + p.closeFee)),
                  },
                  {
                    title: 'PnL ròng',
                    dataIndex: 'netPnl',
                    align: 'right',
                    render: (v: number) => (
                      <Typography.Text strong style={{ color: pnlColor(v) }}>
                        {fmtSigned(v)}
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
