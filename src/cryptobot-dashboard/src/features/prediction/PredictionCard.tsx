import { useState } from 'react';
import { Alert, Card, Col, Row, Segmented, Statistic, Table, Tag, Typography } from 'antd';
import { useQuery } from '@tanstack/react-query';
import { predictionApi, type PredictionHistory, type PriceDirection } from '../../api/client';
import { columnFilter } from '../../components/tableFilters';
import { fmtPrice2 as fmtPrice } from '../futures/format';
import { formatVnUnix } from '../../time';

const HISTORY = 300;

const DIRECTIONS = [
  { text: '▲ Lên', value: 'Up' },
  { text: '▼ Xuống', value: 'Down' },
];

function DirectionTag({ direction, big }: { direction: PriceDirection | null; big?: boolean }) {
  if (direction == null) return <Tag>Đứng giá</Tag>;
  const style = big ? { fontSize: 18, padding: '4px 12px' } : undefined;
  return direction === 'Up' ? (
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
  const query = useQuery({
    queryKey: ['prediction', symbol, queryInterval],
    queryFn: () => predictionApi.get(symbol, queryInterval, HISTORY),
    refetchInterval: day ? 60_000 : 15_000,
  });
  const prediction = query.data;
  const recentHits = prediction?.recent.filter((entry) => entry.actual != null && entry.actual === entry.predicted).length ?? 0;
  const recentCounted = prediction?.recent.filter((entry) => entry.actual != null).length ?? 0;

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
      loading={query.isPending}
    >
      {query.isError && <Alert type="error" showIcon title={`Không tải được dự đoán: ${query.error.message}`} />}
      {prediction && (
        <>
          <Row gutter={16} align="middle">
            <Col xs={24} md={8}>
              <Typography.Text type="secondary">
                {day ? 'Ngày' : 'Nến'} mở lúc {formatVnUnix(prediction.nextCandleTime)} sẽ đóng
              </Typography.Text>
              <div style={{ margin: '6px 0' }}>
                <DirectionTag direction={prediction.direction} big />
              </div>
              <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                so với giá đóng {day ? 'ngày' : 'nến'} trước {fmtPrice(prediction.lastClose)} · điểm {prediction.score.toFixed(4)}
              </Typography.Text>
            </Col>
            <Col xs={12} md={8}>
              <Statistic
                title={`Tỷ lệ đúng (chạy thử ${prediction.backtestPredictions} ${unit})`}
                value={prediction.hitRatePercent.toFixed(1)}
                suffix="%"
                styles={{ content: { color: prediction.hitRatePercent > 50 ? '#16a34a' : '#dc2626' } }}
              />
              <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                {prediction.backtestCorrect}/{prediction.backtestPredictions} lần đúng · đoán bừa ≈ 50%
              </Typography.Text>
            </Col>
            <Col xs={12} md={8}>
              <Statistic title={`Đúng ${recentCounted} ${unit} gần nhất`} value={`${recentHits}/${recentCounted}`} />
            </Col>
          </Row>

          <Typography.Paragraph style={{ margin: '12px 0 0', fontSize: 12 }}>
            <Typography.Text strong>Lý do: </Typography.Text>
            {prediction.reason}
          </Typography.Paragraph>
          <Typography.Paragraph type="secondary" style={{ margin: '4px 0 0', fontSize: 12 }}>
            Tham số: xu hướng TB {prediction.settings.trendPeriod} {unit} × {prediction.settings.trendWeight}, đà {prediction.settings.momentumPeriod}{' '}
            {unit} × {prediction.settings.momentumWeight}, lực mua/bán × {prediction.settings.pressureWeight}. Sửa trong{' '}
            <code>CryptoBot.Domain/Prediction/PriceDirectionPredictor.cs</code> rồi restart API.
          </Typography.Paragraph>

          <Table<PredictionHistory>
            rowKey="time"
            size="small"
            style={{ marginTop: 12 }}
            dataSource={prediction.recent}
            pagination={{ pageSize: 10, hideOnSinglePage: true, size: 'small' }}
            scroll={{ x: 'max-content' }}
            columns={[
              { title: day ? 'Ngày (mở lúc)' : 'Nến (mở lúc)', dataIndex: 'time', render: formatVnUnix },
              {
                title: 'Đoán',
                dataIndex: 'predicted',
                ...columnFilter<PredictionHistory>((entry) => entry.predicted, DIRECTIONS),
                render: (direction: PriceDirection) => <DirectionTag direction={direction} />,
              },
              {
                title: 'Thực tế',
                dataIndex: 'actual',
                ...columnFilter<PredictionHistory>((entry) => entry.actual ?? 'Flat', [
                  ...DIRECTIONS,
                  { text: 'Đứng giá', value: 'Flat' },
                ]),
                render: (direction: PriceDirection | null) => <DirectionTag direction={direction} />,
              },
              {
                title: 'Giá đóng',
                key: 'close',
                align: 'right',
                render: (_, entry) => `${fmtPrice(entry.previousClose)} → ${fmtPrice(entry.close)}`,
              },
              {
                title: 'Tăng/giảm',
                key: 'change',
                align: 'right',
                render: (_, entry) => (
                  <span style={{ color: entry.change > 0 ? '#16a34a' : entry.change < 0 ? '#dc2626' : undefined, fontVariantNumeric: 'tabular-nums' }}>
                    {entry.change > 0 ? '+' : ''}
                    {fmtPrice(entry.change)} ({entry.changePercent > 0 ? '+' : ''}
                    {entry.changePercent.toFixed(2)}%)
                  </span>
                ),
              },
              {
                title: 'Kết quả',
                key: 'hit',
                ...columnFilter<PredictionHistory>(
                  (entry) => (entry.actual == null ? 'None' : entry.actual === entry.predicted ? 'Hit' : 'Miss'),
                  [
                    { text: 'Đúng', value: 'Hit' },
                    { text: 'Sai', value: 'Miss' },
                  ],
                ),
                render: (_, entry) =>
                  entry.actual == null ? '—' : entry.actual === entry.predicted ? <Tag color="green">Đúng</Tag> : <Tag color="red">Sai</Tag>,
              },
            ]}
          />
        </>
      )}
    </Card>
  );
}
