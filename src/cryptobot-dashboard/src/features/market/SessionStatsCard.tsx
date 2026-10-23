import { Card, Col, Row, Statistic, Table, Tag, Typography } from 'antd';
import type { ChartPoint } from '../../api/client';
import { formatVnUnix } from '../../time';

/** Số phiên dùng để tính mức trung bình so sánh. */
const AVERAGE_OVER = 20;

const count = new Intl.NumberFormat('en-US');
const coin = new Intl.NumberFormat('en-US', { maximumFractionDigits: 3 });
const compactUsdt = new Intl.NumberFormat('en-US', { notation: 'compact', maximumFractionDigits: 2 });

const fmtCount = (v: number | null) => (v == null ? '—' : count.format(v));
const fmtQuote = (v: number | null) => (v == null ? '—' : `${compactUsdt.format(v)} USDT`);

interface SessionRow {
  time: number;
  tradeCount: number | null;
  volume: number;
  quoteVolume: number | null;
  up: boolean;
  isClosed: boolean;
  /** Số giao dịch so với trung bình các phiên đã đóng trước đó (%); null khi thiếu dữ liệu. */
  vsAverage: number | null;
}

/**
 * Mỗi nến là một phiên (15m: phiên 15 phút). Bảng cho biết trong phiên có bao nhiêu giao dịch khớp,
 * khối lượng bao nhiêu (coin và USDT), và đông hơn hay vắng hơn mức trung bình gần đây.
 */
export function SessionStatsCard({ candles, symbol, interval }: { candles: ChartPoint[]; symbol: string; interval: string }) {
  const closed = candles.filter((c) => c.isClosed && c.tradeCount != null);
  const recent = closed.slice(-AVERAGE_OVER);
  const avgTrades = recent.length ? recent.reduce((s, c) => s + c.tradeCount!, 0) / recent.length : null;
  const avgQuote = recent.length ? recent.reduce((s, c) => s + (c.quoteVolume ?? 0), 0) / recent.length : null;
  const last = closed.at(-1);
  const baseAsset = symbol.replace(/USDT$/, '');

  const rows: SessionRow[] = candles
    .slice(-50)
    .map((c) => {
      const before = closed.filter((p) => p.time < c.time).slice(-AVERAGE_OVER);
      const avg = before.length ? before.reduce((s, p) => s + p.tradeCount!, 0) / before.length : null;
      return {
        time: c.time,
        tradeCount: c.tradeCount,
        volume: c.volume,
        quoteVolume: c.quoteVolume,
        up: c.close >= c.open,
        isClosed: c.isClosed,
        vsAverage: c.tradeCount != null && avg ? (c.tradeCount / avg - 1) * 100 : null,
      };
    })
    .reverse();

  return (
    <Card size="small" title={`Giao dịch theo phiên ${interval} · ${symbol}`}>
      <Row gutter={16}>
        <Col xs={12} md={6}>
          <Statistic title="Số giao dịch phiên vừa đóng" value={fmtCount(last?.tradeCount ?? null)} />
        </Col>
        <Col xs={12} md={6}>
          <Statistic title="Khối lượng phiên vừa đóng" value={fmtQuote(last?.quoteVolume ?? null)} />
        </Col>
        <Col xs={12} md={6}>
          <Statistic
            title={`TB số giao dịch / phiên (${recent.length} phiên)`}
            value={avgTrades == null ? '—' : count.format(Math.round(avgTrades))}
          />
        </Col>
        <Col xs={12} md={6}>
          <Statistic title={`TB khối lượng / phiên (${recent.length} phiên)`} value={fmtQuote(avgQuote)} />
        </Col>
      </Row>

      <Table<SessionRow>
        rowKey="time"
        size="small"
        style={{ marginTop: 12 }}
        dataSource={rows}
        pagination={{ pageSize: 10, hideOnSinglePage: true, size: 'small' }}
        scroll={{ x: 'max-content' }}
        locale={{ emptyText: 'Chưa có dữ liệu giao dịch' }}
        columns={[
          {
            title: 'Phiên (mở lúc)',
            key: 'time',
            render: (_, r) => (
              <span>
                {formatVnUnix(r.time)} {!r.isClosed && <Tag>Đang chạy</Tag>}
              </span>
            ),
          },
          {
            title: 'Số giao dịch',
            dataIndex: 'tradeCount',
            align: 'right',
            render: fmtCount,
          },
          {
            title: `Khối lượng (${baseAsset})`,
            dataIndex: 'volume',
            align: 'right',
            render: (v: number, r) => <span style={{ color: r.up ? '#16a34a' : '#dc2626' }}>{coin.format(v)}</span>,
          },
          { title: 'Khối lượng (USDT)', dataIndex: 'quoteVolume', align: 'right', render: fmtQuote },
          {
            title: `So với TB ${AVERAGE_OVER} phiên`,
            dataIndex: 'vsAverage',
            align: 'right',
            render: (v: number | null, r) =>
              v == null || !r.isClosed ? (
                '—'
              ) : (
                <span style={{ color: v >= 0 ? '#16a34a' : '#64748b' }}>
                  {v > 0 ? '+' : ''}
                  {v.toFixed(0)}%
                </span>
              ),
          },
        ]}
      />
      <Typography.Paragraph type="secondary" style={{ fontSize: 12, margin: '8px 0 0' }}>
        Mỗi nến {interval} là một phiên. Số giao dịch là số lệnh khớp trên sàn Spot trong phiên; khối lượng màu xanh là
        phiên giá tăng, màu đỏ là phiên giá giảm. Phiên đang chạy còn tăng tiếp nên không so với trung bình.
      </Typography.Paragraph>
    </Card>
  );
}
