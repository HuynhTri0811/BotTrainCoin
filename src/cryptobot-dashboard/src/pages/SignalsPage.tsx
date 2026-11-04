import { useState } from 'react';
import { Card, Table, Tag } from 'antd';
import { keepPreviousData, useQuery } from '@tanstack/react-query';
import { api, type Signal, type Strategy } from '../api/client';
import { EXCEL_FORMAT, downloadExcel, type ExcelColumn } from '../components/excel';
import { ExportExcelButton } from '../components/ExportExcelButton';
import { picked, serverFilter } from '../components/tableFilters';
import { excelVnDate, formatVn } from '../time';

const priceFormat = new Intl.NumberFormat('en-US', { maximumFractionDigits: 8 });

/** API trả tối đa 1000 tín hiệu mỗi lần (BotEndpoints). */
const EXPORT_LIMIT = 1000;

/** Tín hiệu Buy/Sell gần đây của chiến lược EMA đang chọn. */
export function SignalsPage({ strategy }: { strategy: Strategy }) {
  const [filter, setFilter] = useState<{ action?: string; isBackfill?: string }>({});
  const signals = useQuery({
    queryKey: ['signals', strategy.id, filter],
    queryFn: () => api.signals(strategy.id, filter),
    placeholderData: keepPreviousData,
    refetchInterval: 15_000,
  });

  const fastLabel = `EMA${strategy.parameters.fastPeriod}`;
  const slowLabel = `EMA${strategy.parameters.slowPeriod}`;

  // Tải lại với giới hạn lớn nhất của API (bảng chỉ tải 50 tín hiệu gần nhất), cùng bộ lọc đang chọn.
  const exportSignals = async () => {
    const columns: ExcelColumn<Signal>[] = [
      { header: 'Hành động', value: (signal) => signal.action.toUpperCase() },
      { header: 'Nến (mở)', value: (signal) => excelVnDate(signal.candleOpenTimeUtc), format: EXCEL_FORMAT.dateTime, width: 17 },
      { header: 'Close', value: (signal) => signal.candleClose, format: EXCEL_FORMAT.price, width: 12 },
      { header: fastLabel, value: (signal) => signal.fastEma, format: EXCEL_FORMAT.price, width: 12 },
      { header: slowLabel, value: (signal) => signal.slowEma, format: EXCEL_FORMAT.price, width: 12 },
      { header: 'Lý do', value: (signal) => signal.reason, width: 60 },
      { header: 'Loại', value: (signal) => (signal.isBackfill ? 'Backfill' : 'Mới') },
      { header: 'Tạo lúc', value: (signal) => excelVnDate(signal.createdAtUtc), format: EXCEL_FORMAT.dateTime, width: 17 },
    ];
    const rows = await api.signals(strategy.id, filter, EXPORT_LIMIT);
    return downloadExcel(`tin-hieu-${strategy.symbol}-${strategy.interval}`, 'Tín hiệu', rows, columns);
  };

  return (
    <Card
      size="small"
      title={`Tín hiệu gần đây · ${strategy.symbol} ${strategy.interval}`}
      extra={<ExportExcelButton onExport={exportSignals} />}
    >
      <Table<Signal>
        rowKey="id"
        size="small"
        loading={signals.isPending}
        dataSource={signals.data ?? []}
        pagination={{ pageSize: 10, hideOnSinglePage: true }}
        scroll={{ x: true }}
        onChange={(_, filters) => setFilter({ action: picked(filters.action), isBackfill: picked(filters.isBackfill) })}
        columns={[
          {
            title: 'Hành động',
            dataIndex: 'action',
            ...serverFilter([
              { text: 'BUY', value: 'Buy' },
              { text: 'SELL', value: 'Sell' },
            ]),
            render: (item: Signal['action']) => <Tag color={item === 'Buy' ? 'green' : 'red'}>{item.toUpperCase()}</Tag>,
          },
          { title: 'Nến (mở)', dataIndex: 'candleOpenTimeUtc', render: formatVn },
          { title: 'Close', dataIndex: 'candleClose', align: 'right', render: (value: number) => priceFormat.format(value) },
          {
            title: `${fastLabel} / ${slowLabel}`,
            key: 'ema',
            align: 'right',
            render: (_, signal) => `${priceFormat.format(signal.fastEma)} / ${priceFormat.format(signal.slowEma)}`,
          },
          { title: 'Lý do', dataIndex: 'reason' },
          {
            title: 'Loại',
            dataIndex: 'isBackfill',
            ...serverFilter([
              { text: 'Mới', value: 'false' },
              { text: 'Backfill', value: 'true' },
            ]),
            render: (isBackfill: boolean) =>
              isBackfill ? <Tag>Backfill</Tag> : <Tag color="processing">Mới</Tag>,
          },
          { title: 'Tạo lúc', dataIndex: 'createdAtUtc', render: formatVn },
        ]}
      />
    </Card>
  );
}
