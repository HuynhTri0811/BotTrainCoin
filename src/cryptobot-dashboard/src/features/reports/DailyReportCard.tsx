import { Card, Col, Row, Statistic, Table, Tag, Typography } from 'antd';
import type { ReportCandle, SavedDailyReport } from '../../api/client';
import { formatVn } from '../../time';
import { fmtPrice2 } from '../futures/format';

export const fmtPct2 = (v: number) => `${v > 0 ? '+' : ''}${v.toFixed(2)}%`;
export const pctColor = (v: number) => (v > 0 ? '#16a34a' : v < 0 ? '#dc2626' : undefined);
const volume = new Intl.NumberFormat('en-US', { maximumFractionDigits: 0 });

/** Một dòng mô tả nến: giờ, giá mở → đóng và % thay đổi. */
function CandleLine({ label, c }: { label: string; c: ReportCandle }) {
  return (
    <Typography.Text>
      {label}: <b>{formatVn(c.openTimeUtc)}</b>, {fmtPrice2(c.open)} → {fmtPrice2(c.close)}{' '}
      <span style={{ color: pctColor(c.changePercent), fontWeight: 600 }}>({fmtPct2(c.changePercent)})</span>
      {c.volumeRatio != null && <> · khối lượng ×{c.volumeRatio.toFixed(1)} trung bình</>}
    </Typography.Text>
  );
}

/** Báo cáo biến động của một symbol trong khoảng (thường 24 giờ) kết thúc lúc giờ lập báo cáo. */
export function DailyReportCard({ saved }: { saved: SavedDailyReport }) {
  const r = saved.report;
  const coin = saved.symbol.replace(/USDT$/, '');
  const verb = r.changePercent > 0 ? 'tăng' : r.changePercent < 0 ? 'giảm' : 'đi ngang';

  return (
    <Card size="small" title={`${coin} · ${formatVn(r.fromUtc)} → ${formatVn(r.toUtc)}`}>
      <Typography.Paragraph>
        {coin} {verb}{' '}
        <Typography.Text strong style={{ color: pctColor(r.changePercent) }}>
          {fmtPct2(r.changePercent)}
        </Typography.Text>{' '}
        ({fmtPrice2(r.open)} → {fmtPrice2(r.close)}). Cao nhất {fmtPrice2(r.high)} lúc {formatVn(r.highTimeUtc)}, thấp
        nhất {fmtPrice2(r.low)} lúc {formatVn(r.lowTimeUtc)}.
        {r.volumeSpikes.length > 0 && (
          <> Có {r.volumeSpikes.length} giờ khối lượng bất thường.</>
        )}
      </Typography.Paragraph>

      <Row gutter={[16, 16]}>
        <Col xs={12} md={4}>
          <Statistic title="Thay đổi" value={fmtPct2(r.changePercent)} styles={{ content: { color: pctColor(r.changePercent) } }} />
        </Col>
        <Col xs={12} md={4}>
          <Statistic title="Biên độ cao–thấp" value={`${r.rangePercent.toFixed(2)}%`} />
        </Col>
        <Col xs={12} md={4}>
          <Statistic title="Giảm sâu nhất từ đỉnh" value={fmtPct2(r.maxDrawdownPercent)} styles={{ content: { color: pctColor(r.maxDrawdownPercent) } }} />
        </Col>
        <Col xs={12} md={4}>
          <Statistic title="Hồi mạnh nhất từ đáy" value={fmtPct2(r.maxRallyPercent)} styles={{ content: { color: pctColor(r.maxRallyPercent) } }} />
        </Col>
        <Col xs={12} md={4}>
          <Statistic title="Mua chủ động" value={r.buyPercent == null ? '—' : `${r.buyPercent.toFixed(1)}%`} />
        </Col>
        <Col xs={12} md={4}>
          <Statistic title={`Khối lượng (${coin})`} value={volume.format(r.volume)} />
        </Col>
      </Row>

      <div style={{ display: 'flex', flexDirection: 'column', gap: 4, margin: '16px 0' }}>
        <CandleLine label="Giờ tăng mạnh nhất" c={r.biggestRise} />
        <CandleLine label="Giờ giảm mạnh nhất" c={r.biggestDrop} />
      </div>

      {r.volumeSpikes.length > 0 && (
        <Table
          size="small"
          rowKey="openTimeUtc"
          pagination={false}
          dataSource={r.volumeSpikes}
          columns={[
            { title: 'Giờ khối lượng bất thường', dataIndex: 'openTimeUtc', render: formatVn },
            {
              title: 'Giá',
              render: (_, c) => `${fmtPrice2(c.open)} → ${fmtPrice2(c.close)}`,
            },
            {
              title: 'Thay đổi',
              dataIndex: 'changePercent',
              render: (v: number) => <span style={{ color: pctColor(v) }}>{fmtPct2(v)}</span>,
            },
            {
              title: '× trung bình',
              dataIndex: 'volumeRatio',
              render: (v: number) => <Tag color={v >= 5 ? 'red' : 'orange'}>×{v.toFixed(1)}</Tag>,
            },
            {
              title: 'Bên chiếm ưu thế',
              dataIndex: 'buyPercent',
              render: (v: number | null) =>
                v == null ? '—' : v >= 50 ? `Mua ${v.toFixed(1)}%` : `Bán ${(100 - v).toFixed(1)}%`,
            },
          ]}
        />
      )}

      <Typography.Paragraph type="secondary" style={{ margin: '8px 0 0', fontSize: 12 }}>
        Từ {r.candleCount} nến {saved.interval} đã đóng của Binance, lập lúc {formatVn(saved.createdAtUtc)}. "Giảm sâu
        nhất từ đỉnh" so đáy của mỗi nến với đỉnh của các nến trước nó. Khối lượng bất thường là giờ có khối lượng gấp
        từ 3 lần trung bình 20 nến trước. Báo cáo chỉ mô tả chuyện đã xảy ra, không dự đoán.
      </Typography.Paragraph>
    </Card>
  );
}
