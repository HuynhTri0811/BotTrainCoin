import { useMemo, useState } from 'react';
import { Button, Card, Popconfirm, Table, Tabs, Tag, Typography, message } from 'antd';
import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import type { ColumnsType } from 'antd/es/table';
import { API_MAX_LIMIT, ApiError, api, futuresApi, type FuturesAccount, type FuturesPosition } from '../../api/client';
import { EXCEL_FORMAT, downloadExcel, limitIfTruncated, type ExcelColumn } from '../../components/excel';
import { ExportExcelButton } from '../../components/ExportExcelButton';
import {
  applyColumnFilters,
  columnFilter,
  optionsFrom,
  picked,
  serverFilter,
  type TableFilters,
  withFilteredValues,
} from '../../components/tableFilters';
import { excelVnDate, formatVn } from '../../time';
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

const EXCEL_COLUMNS: ExcelColumn<FuturesPosition>[] = [
  { header: 'Mở lúc', value: (position) => excelVnDate(position.openedAtUtc), format: EXCEL_FORMAT.dateTime, width: 17 },
  { header: 'Đóng lúc', value: (position) => excelVnDate(position.closedAtUtc), format: EXCEL_FORMAT.dateTime, width: 17 },
  { header: 'Symbol', value: (position) => position.symbol, width: 11 },
  { header: 'Hướng', value: (position) => position.side },
  { header: 'Đòn bẩy', value: (position) => position.leverage },
  { header: 'Khối lượng', value: (position) => position.quantity, width: 12 },
  { header: 'Giá vào', value: (position) => position.entryPrice, format: EXCEL_FORMAT.price, width: 12 },
  { header: 'Giá ra', value: (position) => position.exitPrice, format: EXCEL_FORMAT.price, width: 12 },
  { header: 'Stop loss', value: (position) => position.stopLoss, format: EXCEL_FORMAT.price, width: 12 },
  { header: 'Take profit', value: (position) => position.takeProfit, format: EXCEL_FORMAT.price, width: 12 },
  { header: 'Margin (USDT)', value: (position) => position.margin, format: EXCEL_FORMAT.amount, width: 13 },
  { header: 'Phí (USDT)', value: (position) => position.openFee + position.closeFee, format: EXCEL_FORMAT.amount, width: 11 },
  { header: 'PnL giá (USDT)', value: (position) => position.realizedPnl, format: EXCEL_FORMAT.signedAmount, width: 14 },
  { header: 'PnL ròng (USDT)', value: (position) => position.netPnl, format: EXCEL_FORMAT.signedAmount, width: 15 },
  {
    header: 'Lý do đóng',
    value: (position) => (position.closeReason ? (reasonLabel[position.closeReason]?.text ?? position.closeReason) : null),
    width: 17,
  },
];

export function PositionsCard({ account }: { account: FuturesAccount | undefined }) {
  const queryClient = useQueryClient();
  // Một state cho mọi bộ lọc của bảng lịch sử (controlled): bộ lọc API lấy ra từ đây, lọc lời/lỗ chạy trên trình duyệt.
  const [tableFilters, setTableFilters] = useState<TableFilters>({});
  const filter = useMemo(
    () => ({
      symbol: picked(tableFilters.symbol),
      side: picked(tableFilters.side),
      closeReason: picked(tableFilters.closeReason),
    }),
    [tableFilters],
  );
  const [tab, setTab] = useState('open');
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

  // Dựng lại cột chỉ khi danh sách chiến lược hoặc bộ lọc đổi, không phải mỗi lần poll 5 giây.
  const strategies = status.data?.strategies;
  const historyColumns = useMemo<ColumnsType<FuturesPosition>>(
    () => [
      { title: 'Đóng lúc', dataIndex: 'closedAtUtc', render: formatVn },
      {
        title: 'Symbol',
        dataIndex: 'symbol',
        ...serverFilter(optionsFrom(strategies ?? [], (strategy) => strategy.symbol)),
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
    ],
    [strategies],
  );
  const tableColumns = useMemo(() => withFilteredValues(historyColumns, tableFilters), [historyColumns, tableFilters]);

  // Tải lại với giới hạn lớn nhất của API (bảng chỉ tải 50 lệnh gần nhất), cùng bộ lọc đang chọn trên bảng. Lọc lời/lỗ
  // chạy sau khi tải nên nếu bị cắt ở giới hạn API thì cũng chỉ lọc trên các lệnh mới nhất đó: nút sẽ cảnh báo.
  const exportHistory = async () => {
    const fetched = await futuresApi.history(filter, API_MAX_LIMIT.positionHistory);
    const rows = applyColumnFilters(fetched, historyColumns, tableFilters);
    return {
      count: await downloadExcel('lich-su-lenh', 'Lịch sử lệnh', rows, EXCEL_COLUMNS),
      truncatedAt: limitIfTruncated(fetched.length, API_MAX_LIMIT.positionHistory),
    };
  };

  return (
    <Card size="small">
      <Tabs
        size="small"
        activeKey={tab}
        onChange={setTab}
        tabBarExtraContent={tab === 'history' && <ExportExcelButton onExport={exportHistory} />}
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
                onChange={(_, filters) => setTableFilters(filters)}
                columns={tableColumns}
              />
            ),
          },
        ]}
      />
    </Card>
  );
}
