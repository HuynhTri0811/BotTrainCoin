import { Card, Empty, Space, Table, Tag, Typography } from 'antd';
import { useQuery } from '@tanstack/react-query';
import { predictionApi, type HorizonAccuracy, type IndicatorAccuracy, type IndicatorSignalOutcome } from '../../api/client';
import { columnFilter } from '../../components/tableFilters';
import { fmtPrice2 as fmtPrice } from '../futures/format';
import { formatVnUnix } from '../../time';

const signedPct = (v: number | null) => (v == null ? '—' : `${v > 0 ? '+' : ''}${v.toFixed(2)}%`);
const moveColor = (v: number | null) => (v == null || v === 0 ? undefined : v > 0 ? '#16a34a' : '#dc2626');

/**
 * Tín hiệu Long/Short của chỉ báo tự viết (vd. my-rsi) đúng bao nhiêu %: sau 1, 3, 6, 12 nến
 * giá có đi đúng hướng tín hiệu không, và trung bình đi được bao nhiêu %.
 */
export function IndicatorAccuracyCard({ symbol, interval }: { symbol: string; interval: string }) {
  const q = useQuery({
    queryKey: ['indicator-accuracy', symbol, interval],
    queryFn: () => predictionApi.indicators(symbol, interval),
    refetchInterval: 60_000,
  });

  return (
    <Card size="small" title={`Tỷ lệ đúng của chỉ báo · ${symbol} ${interval}`} loading={q.isPending}>
      {q.isError && <Typography.Text type="danger">Không tải được: {q.error.message}</Typography.Text>}
      {q.data?.length === 0 && <Empty description={`Chưa có chỉ báo nào cho ${symbol} ${interval}`} />}
      <Space orientation="vertical" size="large" style={{ width: '100%' }}>
        {q.data?.map((a) => <IndicatorBlock key={a.name} a={a} interval={interval} />)}
      </Space>
      <Typography.Paragraph type="secondary" style={{ fontSize: 12, margin: '12px 0 0' }}>
        Long đúng khi giá đóng sau N nến cao hơn giá đóng của nến phát tín hiệu, Short thì ngược lại. "Đi theo hướng" dương
        là trung bình giá đi đúng hướng tín hiệu, âm là đi ngược. Ít tín hiệu thì tỷ lệ chưa đáng tin.
      </Typography.Paragraph>
    </Card>
  );
}

function IndicatorBlock({ a, interval }: { a: IndicatorAccuracy; interval: string }) {
  const total = a.horizons[0]?.signals ?? 0;
  return (
    <div>
      <Space wrap style={{ marginBottom: 8 }}>
        <Tag color="purple">{a.name}</Tag>
        <Typography.Text type="secondary">
          {a.type} · {total} tín hiệu trong {a.candles} nến {interval}
          {a.fromTime != null && ` (từ ${formatVnUnix(a.fromTime)})`}
        </Typography.Text>
      </Space>

      <Table<HorizonAccuracy>
        rowKey="candles"
        size="small"
        pagination={false}
        dataSource={a.horizons}
        scroll={{ x: 'max-content' }}
        columns={[
          { title: 'Sau', dataIndex: 'candles', render: (n: number) => `${n} nến` },
          {
            title: 'Tỷ lệ đúng',
            key: 'hit',
            render: (_, h) =>
              h.signals === 0 ? (
                '—'
              ) : (
                <span style={{ color: h.hitRatePercent > 50 ? '#16a34a' : '#dc2626', fontWeight: 600 }}>
                  {h.hitRatePercent.toFixed(1)}%
                </span>
              ),
          },
          { title: 'Đúng / tín hiệu', key: 'n', render: (_, h) => `${h.correct}/${h.signals}` },
          {
            title: 'Đi theo hướng (TB)',
            dataIndex: 'averageMovePercent',
            align: 'right',
            render: (v: number, h) => (h.signals === 0 ? '—' : <span style={{ color: moveColor(v) }}>{signedPct(v)}</span>),
          },
        ]}
      />

      {a.recent.length > 0 && (
        <Table<IndicatorSignalOutcome>
          rowKey="time"
          size="small"
          style={{ marginTop: 8 }}
          dataSource={a.recent}
          pagination={{ pageSize: 5, hideOnSinglePage: true, size: 'small' }}
          scroll={{ x: 'max-content' }}
          columns={[
            { title: 'Tín hiệu lúc', dataIndex: 'time', render: formatVnUnix },
            {
              title: 'Hướng',
              dataIndex: 'direction',
              ...columnFilter<IndicatorSignalOutcome>((o) => o.direction, [
                { text: '▲ Long', value: 'Up' },
                { text: '▼ Short', value: 'Down' },
              ]),
              render: (d: string) => (d === 'Up' ? <Tag color="green">▲ Long</Tag> : <Tag color="red">▼ Short</Tag>),
            },
            { title: 'Giá đóng', dataIndex: 'close', align: 'right', render: fmtPrice },
            {
              title: 'Sau 1 nến',
              dataIndex: 'move1',
              align: 'right',
              render: (v: number | null) => <span style={{ color: moveColor(v) }}>{signedPct(v)}</span>,
            },
            {
              title: 'Sau 3 nến',
              dataIndex: 'move3',
              align: 'right',
              render: (v: number | null) => <span style={{ color: moveColor(v) }}>{signedPct(v)}</span>,
            },
            { title: 'Lý do', dataIndex: 'reason' },
          ]}
        />
      )}
    </div>
  );
}
