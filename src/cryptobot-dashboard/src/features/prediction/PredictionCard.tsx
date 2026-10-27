import { useState } from 'react';
import { Alert, Card, Col, Row, Segmented, Statistic, Table, Tag, Typography } from 'antd';
import { useQuery } from '@tanstack/react-query';
import { predictionApi, type PredictionHistory, type PriceDirection } from '../../api/client';
import { fmtPrice2 as fmtPrice } from '../futures/format';
import { formatVnUnix } from '../../time';

const HISTORY = 300;

function DirectionTag({ d, big }: { d: PriceDirection | null; big?: boolean }) {
  if (d == null) return <Tag>Đứng giá</Tag>;
  const style = big ? { fontSize: 18, padding: '4px 12px' } : undefined;
  return d === 'Up' ? (
    <Tag color="green" style={style}>
      ▲ Lên
    </Tag>
  ) : (
    <Tag color="red" style={style}>
      ▼ Xuống
    </Tag>
  );
}

type Mode = 'candle' | 'day';

/**
 * Kết quả hàm core PriceDirectionPredictor: nến kế tiếp (hoặc ngày hiện tại) lên hay xuống, tỷ lệ đúng khi chạy thử
 * trên lịch sử, và các lần đoán gần nhất so với thực tế. Sửa hàm trong Domain rồi restart API để thấy kết quả mới.
 */
export function PredictionCard({ symbol, interval }: { symbol: string; interval: string }) {
  const [mode, setMode] = useState<Mode>('candle');
  const day = mode === 'day';
  // 1 ngày: cùng hàm dự đoán nhưng chạy trên nến ngày (1d), nến được đoán là ngày đang chạy.
  const queryInterval = day ? '1d' : interval;
  const unit = day ? 'ngày' : 'nến';
  const q = useQuery({
    queryKey: ['prediction', symbol, queryInterval],
    queryFn: () => predictionApi.get(symbol, queryInterval, HISTORY),
    refetchInterval: day ? 60_000 : 15_000,
  });
  const p = q.data;
  const recentHits = p?.recent.filter((r) => r.actual != null && r.actual === r.predicted).length ?? 0;
  const recentCounted = p?.recent.filter((r) => r.actual != null).length ?? 0;

  return (
    <Card
      size="small"
      title={`Dự đoán ${day ? '1 ngày' : 'nến kế tiếp'} · ${symbol} ${queryInterval}`}
      extra={
        <Segmented<Mode>
          size="small"
          value={mode}
          onChange={setMode}
          options={[
            { value: 'candle', label: `Nến kế tiếp (${interval})` },
            { value: 'day', label: '1 ngày' },
          ]}
        />
      }
      loading={q.isPending}
    >
      {q.isError && <Alert type="error" showIcon title={`Không tải được dự đoán: ${q.error.message}`} />}
      {p && (
        <>
          <Row gutter={16} align="middle">
            <Col xs={24} md={8}>
              <Typography.Text type="secondary">
                {day ? 'Ngày' : 'Nến'} mở lúc {formatVnUnix(p.nextCandleTime)} sẽ đóng
              </Typography.Text>
              <div style={{ margin: '6px 0' }}>
                <DirectionTag d={p.direction} big />
              </div>
              <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                so với giá đóng {day ? 'ngày' : 'nến'} trước {fmtPrice(p.lastClose)} · điểm {p.score.toFixed(4)}
              </Typography.Text>
            </Col>
            <Col xs={12} md={8}>
              <Statistic
                title={`Tỷ lệ đúng (chạy thử ${p.backtestPredictions} ${unit})`}
                value={p.hitRatePercent.toFixed(1)}
                suffix="%"
                styles={{ content: { color: p.hitRatePercent > 50 ? 'var(--up)' : 'var(--down)' } }}
              />
              <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                {p.backtestCorrect}/{p.backtestPredictions} lần đúng · đoán bừa ≈ 50%
              </Typography.Text>
            </Col>
            <Col xs={12} md={8}>
              <Statistic title={`Đúng ${recentCounted} ${unit} gần nhất`} value={`${recentHits}/${recentCounted}`} />
            </Col>
          </Row>

          <Typography.Paragraph style={{ margin: '12px 0 0', fontSize: 12 }}>
            <Typography.Text strong>Lý do: </Typography.Text>
            {p.reason}
          </Typography.Paragraph>
          <Typography.Paragraph type="secondary" style={{ margin: '4px 0 0', fontSize: 12 }}>
            Tham số: xu hướng TB {p.settings.trendPeriod} {unit} × {p.settings.trendWeight}, đà {p.settings.momentumPeriod}{' '}
            {unit} × {p.settings.momentumWeight}, lực mua/bán × {p.settings.pressureWeight}. Sửa trong{' '}
            <code>CryptoBot.Domain/Prediction/PriceDirectionPredictor.cs</code> rồi restart API.
          </Typography.Paragraph>

          <Table<PredictionHistory>
            rowKey="time"
            size="small"
            style={{ marginTop: 12 }}
            dataSource={p.recent}
            pagination={{ pageSize: 10, hideOnSinglePage: true, size: 'small' }}
            scroll={{ x: 'max-content' }}
            columns={[
              { title: day ? 'Ngày (mở lúc)' : 'Nến (mở lúc)', dataIndex: 'time', render: formatVnUnix },
              { title: 'Đoán', dataIndex: 'predicted', render: (d: PriceDirection) => <DirectionTag d={d} /> },
              { title: 'Thực tế', dataIndex: 'actual', render: (d: PriceDirection | null) => <DirectionTag d={d} /> },
              {
                title: 'Giá đóng',
                key: 'close',
                align: 'right',
                render: (_, r) => `${fmtPrice(r.previousClose)} → ${fmtPrice(r.close)}`,
              },
              {
                title: 'Tăng/giảm',
                key: 'change',
                align: 'right',
                render: (_, r) => (
                  <span style={{ color: r.change > 0 ? 'var(--up)' : r.change < 0 ? 'var(--down)' : undefined, fontVariantNumeric: 'tabular-nums' }}>
                    {r.change > 0 ? '+' : ''}
                    {fmtPrice(r.change)} ({r.changePercent > 0 ? '+' : ''}
                    {r.changePercent.toFixed(2)}%)
                  </span>
                ),
              },
              {
                title: 'Kết quả',
                key: 'hit',
                render: (_, r) =>
                  r.actual == null ? '—' : r.actual === r.predicted ? <Tag color="green">Đúng</Tag> : <Tag color="red">Sai</Tag>,
              },
            ]}
          />
        </>
      )}
    </Card>
  );
}
