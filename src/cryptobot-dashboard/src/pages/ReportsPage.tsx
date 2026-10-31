import { useState } from 'react';
import { Alert, Button, Card, Empty, Segmented, Space, Spin, Table, Typography, message } from 'antd';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { reportApi, type SavedDailyReport } from '../api/client';
import { columnFilter } from '../components/tableFilters';
import { DailyReportCard, fmtPct2, pctColor } from '../features/reports/DailyReportCard';
import { formatVn } from '../time';

const colored = (value: number) => <span style={{ color: pctColor(value) }}>{fmtPct2(value)}</span>;

/** Báo cáo biến động lập mỗi ngày (DailyReportWorker): báo cáo mới nhất đầy đủ, các ngày trước dạng bảng. */
export function ReportsPage() {
  const [symbol, setSymbol] = useState('BTCUSDT');
  const queryClient = useQueryClient();
  const query = useQuery({
    queryKey: ['daily-reports', symbol],
    queryFn: () => reportApi.daily(symbol, 30),
    refetchInterval: 5 * 60_000,
  });
  const run = useMutation({
    mutationFn: reportApi.run,
    onSuccess: (result) => {
      if (result.errors.length > 0) message.warning(result.errors.join(' · '));
      else if (result.created > 0) message.success(`Đã lập ${result.created} báo cáo đến ${formatVn(result.periodEndUtc)}.`);
      else message.info(`Báo cáo đến ${formatVn(result.periodEndUtc)} đã có sẵn.`);
      queryClient.invalidateQueries({ queryKey: ['daily-reports'] });
    },
    onError: (error) => message.error(error.message),
  });

  const data = query.data;
  const [latest, ...older] = data?.reports ?? [];

  return (
    <Space orientation="vertical" size="middle" style={{ width: '100%' }}>
      <Space wrap style={{ justifyContent: 'space-between', width: '100%' }}>
        <Segmented
          value={symbol}
          onChange={(value) => setSymbol(value as string)}
          options={(data?.symbols ?? [symbol]).map((symbol) => ({ value: symbol, label: symbol.replace(/USDT$/, '') }))}
        />
        <Space wrap>
          {data && (
            <Typography.Text type="secondary">
              Lập mỗi ngày lúc {data.runAt} ({data.timeZone}) · lần tới {formatVn(data.nextRunUtc)}
            </Typography.Text>
          )}
          <Button loading={run.isPending} onClick={() => run.mutate()}>
            Tạo ngay
          </Button>
        </Space>
      </Space>

      {query.isPending ? (
        <Spin />
      ) : query.isError ? (
        <Alert type="error" showIcon title={`Không tải được báo cáo: ${query.error.message}`} />
      ) : !latest ? (
        <Card size="small">
          <Empty description='Chưa có báo cáo. Bấm "Tạo ngay" hoặc chờ tới giờ lập.' />
        </Card>
      ) : (
        <>
          <DailyReportCard key={latest.report.toUtc} saved={latest} />
          {older.length > 0 && (
            <Card size="small" title="Các ngày trước">
              <Table<SavedDailyReport>
                size="small"
                rowKey={(saved) => saved.report.toUtc}
                pagination={false}
                dataSource={older}
                columns={[
                  { title: 'Đến', render: (_, saved) => formatVn(saved.report.toUtc) },
                  {
                    title: 'Thay đổi',
                    ...columnFilter<SavedDailyReport>((saved) => (saved.report.changePercent >= 0 ? 'Up' : 'Down'), [
                      { text: 'Tăng', value: 'Up' },
                      { text: 'Giảm', value: 'Down' },
                    ]),
                    render: (_, saved) => colored(saved.report.changePercent),
                  },
                  { title: 'Biên độ', render: (_, saved) => `${saved.report.rangePercent.toFixed(2)}%` },
                  { title: 'Giảm sâu nhất', render: (_, saved) => colored(saved.report.maxDrawdownPercent) },
                  { title: 'Hồi mạnh nhất', render: (_, saved) => colored(saved.report.maxRallyPercent) },
                  {
                    title: 'Mua chủ động',
                    render: (_, saved) => (saved.report.buyPercent == null ? '—' : `${saved.report.buyPercent.toFixed(1)}%`),
                  },
                  {
                    title: 'Giờ khối lượng bất thường',
                    ...columnFilter<SavedDailyReport>((saved) => (saved.report.volumeSpikes.length > 0 ? 'Yes' : 'No'), [
                      { text: 'Có', value: 'Yes' },
                      { text: 'Không', value: 'No' },
                    ]),
                    render: (_, saved) => saved.report.volumeSpikes.length,
                  },
                ]}
              />
            </Card>
          )}
        </>
      )}
    </Space>
  );
}
