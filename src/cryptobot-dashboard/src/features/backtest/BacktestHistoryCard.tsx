import { useState, type ReactNode } from 'react';
import { Button, Card, Checkbox, Popconfirm, Space, Table, Tag, Typography, message } from 'antd';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { backtestApi, type BacktestSettings, type BacktestSummary } from '../../api/client';
import { EXCEL_FORMAT, downloadExcel, type ExcelColumn } from '../../components/excel';
import { ExportExcelButton } from '../../components/ExportExcelButton';
import { excelVnDate, formatVn } from '../../time';
import { fmtSigned, pnlColor } from '../futures/format';

const percent = new Intl.NumberFormat('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
const amount = new Intl.NumberFormat('en-US', { maximumFractionDigits: 2 });
const fmtPercent = (value: number) => `${percent.format(value)}%`;
const fmtSignedPercent = (value: number) => `${value > 0 ? '+' : ''}${percent.format(value)}%`;
const MAX_COMPARE = 4;

/** `1000 USDT · 10% vốn` hoặc `1000 USDT · 100 USDT/lệnh`. */
export const fmtSizing = (settings: BacktestSettings) =>
  `${amount.format(settings.initialCapital)} USDT · ${
    settings.sizing === 'PercentOfEquity' ? `${amount.format(settings.sizeValue)}% vốn` : `${amount.format(settings.sizeValue)} USDT/lệnh`
  }`;

/** `TP 10% · SL 3%`, phần không dùng ghi "không". */
export const fmtStops = (settings: BacktestSettings) =>
  `TP ${settings.takeProfitPercent == null ? 'không' : `${amount.format(settings.takeProfitPercent)}%`} · SL ${
    settings.stopLossPercent == null ? 'không' : `${amount.format(settings.stopLossPercent)}%`
  }`;

const ema = (summary: BacktestSummary) =>
  `EMA${summary.parameters.fastPeriod}/${summary.parameters.slowPeriod} · ${summary.parameters.lookbackCandles}`;

const period = (summary: BacktestSummary) =>
  `${formatVn(summary.firstCandleUtc).slice(-10)} → ${formatVn(summary.lastCandleUtc).slice(-10)}`;

const excelColumns: ExcelColumn<BacktestSummary>[] = [
  { header: '#', value: (summary) => summary.id, width: 6 },
  { header: 'Lúc chạy', value: (summary) => excelVnDate(summary.createdAtUtc), format: EXCEL_FORMAT.dateTime, width: 17 },
  { header: 'Cặp', value: (summary) => summary.symbol, width: 10 },
  { header: 'Khung', value: (summary) => summary.interval, width: 7 },
  { header: 'EMA nhanh', value: (summary) => summary.parameters.fastPeriod, width: 10 },
  { header: 'EMA chậm', value: (summary) => summary.parameters.slowPeriod, width: 10 },
  { header: 'Cửa sổ', value: (summary) => summary.parameters.lookbackCandles, width: 8 },
  { header: 'Từ nến', value: (summary) => excelVnDate(summary.firstCandleUtc), format: EXCEL_FORMAT.dateTime, width: 17 },
  { header: 'Đến nến', value: (summary) => excelVnDate(summary.lastCandleUtc), format: EXCEL_FORMAT.dateTime, width: 17 },
  { header: 'Vốn ban đầu', value: (summary) => summary.settings.initialCapital, format: EXCEL_FORMAT.amount, width: 12 },
  { header: 'Khối lượng', value: (summary) => fmtSizing(summary.settings), width: 24 },
  { header: 'TP %', value: (summary) => summary.settings.takeProfitPercent, width: 7 },
  { header: 'SL %', value: (summary) => summary.settings.stopLossPercent, width: 7 },
  { header: 'Phí %', value: (summary) => summary.settings.feePercent, width: 7 },
  { header: 'Số lệnh', value: (summary) => summary.totalTrades, width: 8 },
  { header: 'Tỷ lệ thắng', value: (summary) => summary.winRatePercent, format: EXCEL_FORMAT.percent, width: 11 },
  { header: 'Lợi nhuận ròng (USDT)', value: (summary) => summary.netProfit, format: EXCEL_FORMAT.signedAmount, width: 18 },
  { header: 'Lợi nhuận ròng %', value: (summary) => summary.netProfitPercent, format: EXCEL_FORMAT.signedPercent, width: 15 },
  { header: 'Max Drawdown %', value: (summary) => summary.maxDrawdownPercent, format: EXCEL_FORMAT.percent, width: 14 },
  { header: 'Ghi chú', value: (summary) => summary.note, width: 40 },
];

/** Hàng của bảng so sánh: nhãn và cách lấy giá trị từ một lần chạy. */
const compareRows: { label: string; value: (summary: BacktestSummary) => ReactNode }[] = [
  { label: 'Lúc chạy', value: (summary) => formatVn(summary.createdAtUtc) },
  { label: 'Cặp · khung', value: (summary) => `${summary.symbol} ${summary.interval}` },
  { label: 'EMA · cửa sổ', value: ema },
  { label: 'Khoảng nến', value: period },
  { label: 'Vốn · khối lượng', value: (summary) => fmtSizing(summary.settings) },
  { label: 'TP · SL', value: (summary) => fmtStops(summary.settings) },
  { label: 'Phí mỗi lần khớp', value: (summary) => `${amount.format(summary.settings.feePercent)}%` },
  { label: 'Số lệnh', value: (summary) => summary.totalTrades.toLocaleString('vi-VN') },
  { label: 'Tỷ lệ thắng', value: (summary) => fmtPercent(summary.winRatePercent) },
  {
    label: 'Lợi nhuận ròng',
    value: (summary) => (
      <span style={{ color: pnlColor(summary.netProfit), fontWeight: 600 }}>
        {fmtSigned(summary.netProfit)} USDT ({fmtSignedPercent(summary.netProfitPercent)})
      </span>
    ),
  },
  { label: 'Max Drawdown', value: (summary) => <span style={{ color: '#dc2626' }}>{fmtPercent(summary.maxDrawdownPercent)}</span> },
  { label: 'Ghi chú', value: (summary) => summary.note ?? '—' },
];

interface Props {
  /** Chiến lược đang chọn trên dashboard, để lọc "chỉ chiến lược này". */
  strategyId: number;
  /** Lần chạy đang hiển thị bên dưới (tô đậm dòng). */
  openedId: number | null;
  onOpen: (id: number) => void;
}

/** Lịch sử mọi lần backtest đã chạy: xem lại, so sánh, ghi chú, xóa, xuất Excel. */
export function BacktestHistoryCard({ strategyId, openedId, onOpen }: Props) {
  const queryClient = useQueryClient();
  const [onlyThisStrategy, setOnlyThisStrategy] = useState(false);
  const [selected, setSelected] = useState<number[]>([]);
  const [comparing, setComparing] = useState(false);

  const history = useQuery({ queryKey: ['backtests'], queryFn: () => backtestApi.list() });
  const refresh = () => queryClient.invalidateQueries({ queryKey: ['backtests'] });

  const setNote = useMutation({
    mutationFn: ({ id, note }: { id: number; note: string }) => backtestApi.setNote(id, note),
    onSuccess: refresh,
    onError: (error) => message.error(error.message),
  });
  const remove = useMutation({
    mutationFn: backtestApi.remove,
    onSuccess: (_, id) => {
      setSelected((current) => current.filter((selectedId) => selectedId !== id));
      refresh();
    },
    onError: (error) => message.error(error.message),
  });

  const rows = (history.data ?? []).filter((summary) => !onlyThisStrategy || summary.strategyId === strategyId);
  const compared = (history.data ?? []).filter((summary) => selected.includes(summary.id));

  return (
    <Card
      size="small"
      title={`Lịch sử backtest (${rows.length})`}
      extra={
        <Space wrap>
          <Checkbox checked={onlyThisStrategy} onChange={(event) => setOnlyThisStrategy(event.target.checked)}>
            Chỉ chiến lược đang chọn
          </Checkbox>
          <Button size="small" disabled={selected.length < 2} onClick={() => setComparing((current) => !current)}>
            {comparing ? 'Ẩn so sánh' : `So sánh (${selected.length})`}
          </Button>
          <ExportExcelButton
            onExport={async () => ({ count: await downloadExcel('lich-su-backtest', 'Backtest', rows, excelColumns) })}
          />
        </Space>
      }
    >
      <Space orientation="vertical" style={{ width: '100%' }}>
        <Typography.Text type="secondary">
          Mọi lần chạy đều được lưu. Tick tối đa {MAX_COMPARE} dòng để so sánh, bấm vào ghi chú để sửa, bấm Xem để mở lại
          đầy đủ kết quả mà không chạy lại.
        </Typography.Text>

        {comparing && compared.length >= 2 && (
          <Table
            rowKey="label"
            size="small"
            bordered
            pagination={false}
            scroll={{ x: true }}
            dataSource={compareRows}
            columns={[
              { title: '', dataIndex: 'label', width: 150, render: (label: string) => <b>{label}</b> },
              ...compared.map((summary) => ({
                title: `#${summary.id}`,
                key: summary.id,
                render: (_: unknown, row: (typeof compareRows)[number]) => row.value(summary),
              })),
            ]}
          />
        )}

        <Table<BacktestSummary>
          rowKey="id"
          size="small"
          loading={history.isPending}
          dataSource={rows}
          pagination={{ pageSize: 10, showSizeChanger: false, hideOnSinglePage: true }}
          scroll={{ x: true }}
          rowClassName={(summary) => (summary.id === openedId ? 'ant-table-row-selected' : '')}
          rowSelection={{
            selectedRowKeys: selected,
            onChange: (keys) => setSelected((keys as number[]).slice(0, MAX_COMPARE)),
            getCheckboxProps: (summary) => ({
              disabled: selected.length >= MAX_COMPARE && !selected.includes(summary.id),
            }),
          }}
          columns={[
            { title: '#', dataIndex: 'id', width: 50 },
            {
              title: 'Lúc chạy',
              dataIndex: 'createdAtUtc',
              render: (iso: string) => <span style={{ whiteSpace: 'nowrap' }}>{formatVn(iso)}</span>,
            },
            {
              title: 'Chiến lược',
              key: 'strategy',
              render: (_, summary) => (
                <Space orientation="vertical" size={0}>
                  <Tag>{`${summary.symbol} ${summary.interval}`}</Tag>
                  <Typography.Text type="secondary">{ema(summary)}</Typography.Text>
                </Space>
              ),
            },
            {
              title: 'Khoảng nến',
              key: 'period',
              render: (_, summary) => <span style={{ whiteSpace: 'nowrap' }}>{period(summary)}</span>,
            },
            {
              title: 'Vốn · rủi ro',
              key: 'settings',
              render: (_, summary) => (
                <Space orientation="vertical" size={0} style={{ whiteSpace: 'nowrap' }}>
                  <span>{fmtSizing(summary.settings)}</span>
                  <Typography.Text type="secondary">
                    {fmtStops(summary.settings)} · phí {amount.format(summary.settings.feePercent)}%
                  </Typography.Text>
                </Space>
              ),
            },
            {
              title: 'Lệnh',
              dataIndex: 'totalTrades',
              align: 'right',
              sorter: (first, second) => first.totalTrades - second.totalTrades,
            },
            {
              title: 'Thắng',
              dataIndex: 'winRatePercent',
              align: 'right',
              sorter: (first, second) => first.winRatePercent - second.winRatePercent,
              render: fmtPercent,
            },
            {
              title: 'Lợi nhuận ròng',
              dataIndex: 'netProfit',
              align: 'right',
              sorter: (first, second) => first.netProfitPercent - second.netProfitPercent,
              render: (value: number, summary) => (
                <span style={{ color: pnlColor(value) }}>
                  {fmtSigned(value)} ({fmtSignedPercent(summary.netProfitPercent)})
                </span>
              ),
            },
            {
              title: 'Max DD',
              dataIndex: 'maxDrawdownPercent',
              align: 'right',
              sorter: (first, second) => first.maxDrawdownPercent - second.maxDrawdownPercent,
              render: fmtPercent,
            },
            {
              title: 'Ghi chú',
              dataIndex: 'note',
              width: 200,
              render: (note: string | null, summary) => (
                <Typography.Text
                  type={note ? undefined : 'secondary'}
                  editable={{
                    text: note ?? '',
                    maxLength: 500,
                    tooltip: 'Sửa ghi chú',
                    onChange: (value) => {
                      if (value !== (note ?? '')) setNote.mutate({ id: summary.id, note: value });
                    },
                  }}
                >
                  {note ?? 'thêm ghi chú'}
                </Typography.Text>
              ),
            },
            {
              title: '',
              key: 'actions',
              render: (_, summary) => (
                <Space size={4}>
                  <Button size="small" type="link" onClick={() => onOpen(summary.id)}>
                    Xem
                  </Button>
                  <Popconfirm
                    title={`Xóa lần chạy #${summary.id}?`}
                    okText="Xóa"
                    cancelText="Giữ"
                    onConfirm={() => remove.mutate(summary.id)}
                  >
                    <Button size="small" type="link" danger>
                      Xóa
                    </Button>
                  </Popconfirm>
                </Space>
              ),
            },
          ]}
        />
      </Space>
    </Card>
  );
}
