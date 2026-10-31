import { Card, Col, Row, Spin, Statistic, Table, Tag, Typography } from 'antd';
import { useQuery } from '@tanstack/react-query';
import { newsApi, type ReportCandle, type SavedDailyReport } from '../../api/client';
import { formatVn } from '../../time';
import { fmtPrice2 } from '../futures/format';
import { NewsList } from '../news/NewsList';

export const fmtPct2 = (value: number) => `${value > 0 ? '+' : ''}${value.toFixed(2)}%`;
export const pctColor = (value: number) => (value > 0 ? '#16a34a' : value < 0 ? '#dc2626' : undefined);
const volume = new Intl.NumberFormat('en-US', { maximumFractionDigits: 0 });

/** Một dòng mô tả nến: giờ, giá mở → đóng và % thay đổi. */
function CandleLine({ label, candle }: { label: string; candle: ReportCandle }) {
  return (
    <Typography.Text>
      {label}: <b>{formatVn(candle.openTimeUtc)}</b>, {fmtPrice2(candle.open)} → {fmtPrice2(candle.close)}{' '}
      <span style={{ color: pctColor(candle.changePercent), fontWeight: 600 }}>({fmtPct2(candle.changePercent)})</span>
      {candle.volumeRatio != null && <> · khối lượng ×{candle.volumeRatio.toFixed(1)} trung bình</>}
    </Typography.Text>
  );
}

/** Báo cáo biến động của một symbol trong khoảng (thường 24 giờ) kết thúc lúc giờ lập báo cáo. */
export function DailyReportCard({ saved }: { saved: SavedDailyReport }) {
  const result = saved.report;
  const coin = saved.symbol.replace(/USDT$/, '');
  const verb = result.changePercent > 0 ? 'tăng' : result.changePercent < 0 ? 'giảm' : 'đi ngang';
  // Tin nhắc tới coin này, đăng trong đúng khoảng báo cáo: gợi ý nguyên nhân, không khẳng định.
  const news = useQuery({
    queryKey: ['report-news', coin, result.fromUtc, result.toUtc],
    queryFn: () => newsApi.list({ coin, fromUtc: result.fromUtc, toUtc: result.toUtc, limit: 10 }),
  });

  return (
    <Card size="small" title={`${coin} · ${formatVn(result.fromUtc)} → ${formatVn(result.toUtc)}`}>
      <Typography.Paragraph>
        {coin} {verb}{' '}
        <Typography.Text strong style={{ color: pctColor(result.changePercent) }}>
          {fmtPct2(result.changePercent)}
        </Typography.Text>{' '}
        ({fmtPrice2(result.open)} → {fmtPrice2(result.close)}). Cao nhất {fmtPrice2(result.high)} lúc {formatVn(result.highTimeUtc)}, thấp
        nhất {fmtPrice2(result.low)} lúc {formatVn(result.lowTimeUtc)}.
        {result.volumeSpikes.length > 0 && (
          <> Có {result.volumeSpikes.length} giờ khối lượng bất thường.</>
        )}
      </Typography.Paragraph>

      <Row gutter={[16, 16]}>
        <Col xs={12} md={4}>
          <Statistic title="Thay đổi" value={fmtPct2(result.changePercent)} styles={{ content: { color: pctColor(result.changePercent) } }} />
        </Col>
        <Col xs={12} md={4}>
          <Statistic title="Biên độ cao–thấp" value={`${result.rangePercent.toFixed(2)}%`} />
        </Col>
        <Col xs={12} md={4}>
          <Statistic title="Giảm sâu nhất từ đỉnh" value={fmtPct2(result.maxDrawdownPercent)} styles={{ content: { color: pctColor(result.maxDrawdownPercent) } }} />
        </Col>
        <Col xs={12} md={4}>
          <Statistic title="Hồi mạnh nhất từ đáy" value={fmtPct2(result.maxRallyPercent)} styles={{ content: { color: pctColor(result.maxRallyPercent) } }} />
        </Col>
        <Col xs={12} md={4}>
          <Statistic title="Mua chủ động" value={result.buyPercent == null ? '—' : `${result.buyPercent.toFixed(1)}%`} />
        </Col>
        <Col xs={12} md={4}>
          <Statistic title={`Khối lượng (${coin})`} value={volume.format(result.volume)} />
        </Col>
      </Row>

      <div style={{ display: 'flex', flexDirection: 'column', gap: 4, margin: '16px 0' }}>
        <CandleLine label="Giờ tăng mạnh nhất" candle={result.biggestRise} />
        <CandleLine label="Giờ giảm mạnh nhất" candle={result.biggestDrop} />
      </div>

      {result.volumeSpikes.length > 0 && (
        <Table
          size="small"
          rowKey="openTimeUtc"
          pagination={false}
          dataSource={result.volumeSpikes}
          columns={[
            { title: 'Giờ khối lượng bất thường', dataIndex: 'openTimeUtc', render: formatVn },
            {
              title: 'Giá',
              render: (_, candle) => `${fmtPrice2(candle.open)} → ${fmtPrice2(candle.close)}`,
            },
            {
              title: 'Thay đổi',
              dataIndex: 'changePercent',
              render: (value: number) => <span style={{ color: pctColor(value) }}>{fmtPct2(value)}</span>,
            },
            {
              title: '× trung bình',
              dataIndex: 'volumeRatio',
              render: (value: number) => <Tag color={value >= 5 ? 'red' : 'orange'}>×{value.toFixed(1)}</Tag>,
            },
            {
              title: 'Bên chiếm ưu thế',
              dataIndex: 'buyPercent',
              render: (value: number | null) =>
                value == null ? '—' : value >= 50 ? `Mua ${value.toFixed(1)}%` : `Bán ${(100 - value).toFixed(1)}%`,
            },
          ]}
        />
      )}

      <Typography.Title level={5} style={{ marginTop: 16 }}>
        Tin nhắc tới {coin} trong khoảng này
      </Typography.Title>
      {news.isPending ? <Spin size="small" /> : <NewsList articles={news.data?.articles ?? []} showSummary={false} />}

      <Typography.Paragraph type="secondary" style={{ margin: '8px 0 0', fontSize: 12 }}>
        Từ {result.candleCount} nến {saved.interval} đã đóng của Binance, lập lúc {formatVn(saved.createdAtUtc)}. "Giảm sâu
        nhất từ đỉnh" so đáy của mỗi nến với đỉnh của các nến trước nó. Khối lượng bất thường là giờ có khối lượng gấp
        từ 3 lần trung bình 20 nến trước. Báo cáo chỉ mô tả chuyện đã xảy ra, không dự đoán; tin cùng khoảng thời gian không chắc là nguyên nhân.
      </Typography.Paragraph>
    </Card>
  );
}
