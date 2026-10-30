import { useState } from 'react';
import { Alert, Button, Card, Empty, Segmented, Space, Spin, Table, Typography, message } from 'antd';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { reportApi, type SavedDailyReport } from '../api/client';
import { DailyReportCard, fmtPct2, pctColor } from '../features/reports/DailyReportCard';
import { formatVn } from '../time';

const colored = (v: number) => <span style={{ color: pctColor(v) }}>{fmtPct2(v)}</span>;

/** Báo cáo biến động lập mỗi ngày (DailyReportWorker): báo cáo mới nhất đầy đủ, các ngày trước dạng bảng. */
export function ReportsPage() {
  const [symbol, setSymbol] = useState('BTCUSDT');
  const queryClient = useQueryClient();
  const q = useQuery({
    queryKey: ['daily-reports', symbol],
    queryFn: () => reportApi.daily(symbol, 30),
    refetchInterval: 5 * 60_000,
  });
  const run = useMutation({
    mutationFn: reportApi.run,
    onSuccess: (r) => {
      if (r.errors.length > 0) message.warning(r.errors.join(' · '));
      else if (r.created > 0) message.success(`Đã lập ${r.created} báo cáo đến ${formatVn(r.periodEndUtc)}.`);
      else message.info(`Báo cáo đến ${formatVn(r.periodEndUtc)} đã có sẵn.`);
      queryClient.invalidateQueries({ queryKey: ['daily-reports'] });
    },
    onError: (e) => message.error(e.message),
  });

  const d = q.data;
  const [latest, ...older] = d?.reports ?? [];

  return (
    <Space orientation="vertical" size="middle" style={{ width: '100%' }}>
      <Space wrap style={{ justifyContent: 'space-between', width: '100%' }}>
        <Segmented
          value={symbol}
          onChange={(v) => setSymbol(v as string)}
          options={(d?.symbols ?? [symbol]).map((s) => ({ value: s, label: s.replace(/USDT$/, '') }))}
        />
        <Space wrap>
          {d && (
            <Typography.Text type="secondary">
              Lập mỗi ngày lúc {d.runAt} ({d.timeZone}) · lần tới {formatVn(d.nextRunUtc)}
            </Typography.Text>
          )}
          <Button loading={run.isPending} onClick={() => run.mutate()}>
            Tạo ngay
          </Button>
        </Space>
      </Space>

      {q.isPending ? (
        <Spin />
      ) : q.isError ? (
        <Alert type="error" showIcon title={`Không tải được báo cáo: ${q.error.message}`} />
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
                rowKey={(r) => r.report.toUtc}
                pagination={false}
                dataSource={older}
                columns={[
                  { title: 'Đến', render: (_, r) => formatVn(r.report.toUtc) },
                  { title: 'Thay đổi', render: (_, r) => colored(r.report.changePercent) },
                  { title: 'Biên độ', render: (_, r) => `${r.report.rangePercent.toFixed(2)}%` },
                  { title: 'Giảm sâu nhất', render: (_, r) => colored(r.report.maxDrawdownPercent) },
                  { title: 'Hồi mạnh nhất', render: (_, r) => colored(r.report.maxRallyPercent) },
                  {
                    title: 'Mua chủ động',
                    render: (_, r) => (r.report.buyPercent == null ? '—' : `${r.report.buyPercent.toFixed(1)}%`),
                  },
                  { title: 'Giờ khối lượng bất thường', render: (_, r) => r.report.volumeSpikes.length },
                ]}
              />
            </Card>
          )}
        </>
      )}
    </Space>
  );
}
