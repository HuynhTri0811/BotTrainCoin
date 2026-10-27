import { Alert, Card, Col, Row, Statistic, Table, Tag, Typography } from 'antd';
import type { ChartPoint, VolumePressure } from '../../api/client';
import { formatVnUnix } from '../../time';

/** Số phiên dùng để tính mức trung bình so sánh. */
const AVERAGE_OVER = 20;

const count = new Intl.NumberFormat('en-US');
const coin = new Intl.NumberFormat('en-US', { maximumFractionDigits: 3 });
const compactUsdt = new Intl.NumberFormat('en-US', { notation: 'compact', maximumFractionDigits: 2 });

const fmtCount = (v: number | null) => (v == null ? '—' : count.format(v));
const fmtQuote = (v: number | null) => (v == null ? '—' : `${compactUsdt.format(v)} USDT`);
const fmtExcess = (p: VolumePressure) => (p.excessPercent == null ? '100% một phía' : `+${p.excessPercent.toFixed(0)}%`);

/** Thanh tỷ trọng mua (xanh) / bán (đỏ) kèm cảnh báo khi một phía áp đảo. */
function PressureCell({ p }: { p: VolumePressure | null }) {
  if (!p) return <>—</>;
  return (
    <span style={{ display: 'inline-flex', alignItems: 'center', gap: 8 }}>
      <span style={{ display: 'inline-flex', width: 80, height: 8, borderRadius: 4, overflow: 'hidden', background: 'var(--down)' }}>
        <span style={{ width: `${p.buyPercent}%`, background: 'var(--up)' }} />
      </span>
      <span style={{ fontVariantNumeric: 'tabular-nums' }}>
        {p.buyPercent.toFixed(0)}/{(100 - p.buyPercent).toFixed(0)}
      </span>
      {p.side === 'SellHeavy' && <Tag color="red">Bán áp đảo {fmtExcess(p)}</Tag>}
      {p.side === 'BuyHeavy' && <Tag color="green">Mua áp đảo {fmtExcess(p)}</Tag>}
    </span>
  );
}

/** Cảnh báo phiên vừa đóng và phiên đang chạy khi một phía vượt ngưỡng. */
function PressureAlert({ candle, running }: { candle: ChartPoint | undefined; running: boolean }) {
  const p = candle?.pressure;
  if (!candle || !p || p.side === 'Balanced') return null;
  const sell = p.side === 'SellHeavy';
  const which = running ? 'Phiên đang chạy' : 'Phiên vừa đóng';
  return (
    <Alert
      showIcon
      type={sell ? 'error' : 'success'}
      style={{ marginBottom: 12 }}
      title={`${which} (${formatVnUnix(candle.time)}): ${sell ? 'bán' : 'mua'} chủ động lớn hơn ${sell ? 'mua' : 'bán'} ${fmtExcess(p)}`}
      description={
        `Mua ${coin.format(p.buyVolume)} · Bán ${coin.format(p.sellVolume)} (ngưỡng cảnh báo ${p.alertExcessPercent}%).` +
        (running ? ' Phiên chưa đóng nên số liệu còn thay đổi.' : '')
      }
    />
  );
}

interface SessionRow {
  time: number;
  tradeCount: number | null;
  volume: number;
  quoteVolume: number | null;
  up: boolean;
  isClosed: boolean;
  pressure: VolumePressure | null;
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
  const running = candles.at(-1)?.isClosed === false ? candles.at(-1) : undefined;
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
        pressure: c.pressure,
        vsAverage: c.tradeCount != null && avg ? (c.tradeCount / avg - 1) * 100 : null,
      };
    })
    .reverse();

  return (
    <Card size="small" title={`Giao dịch theo phiên ${interval} · ${symbol}`}>
      <PressureAlert candle={last} running={false} />
      <PressureAlert candle={running} running />
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
            render: (v: number, r) => <span style={{ color: r.up ? 'var(--up)' : 'var(--down)' }}>{coin.format(v)}</span>,
          },
          { title: 'Khối lượng (USDT)', dataIndex: 'quoteVolume', align: 'right', render: fmtQuote },
          {
            title: 'Mua / Bán chủ động (%)',
            key: 'pressure',
            render: (_, r) => <PressureCell p={r.pressure} />,
          },
          {
            title: `So với TB ${AVERAGE_OVER} phiên`,
            dataIndex: 'vsAverage',
            align: 'right',
            render: (v: number | null, r) =>
              v == null || !r.isClosed ? (
                '—'
              ) : (
                <span style={{ color: v >= 0 ? 'var(--up)' : 'var(--text-muted)' }}>
                  {v > 0 ? '+' : ''}
                  {v.toFixed(0)}%
                </span>
              ),
          },
        ]}
      />
      <Typography.Paragraph type="secondary" style={{ fontSize: 12, margin: '8px 0 0' }}>
        Mỗi nến {interval} là một phiên. Số giao dịch là số lệnh khớp trên sàn Spot trong phiên; khối lượng màu xanh là
        phiên giá tăng, màu đỏ là phiên giá giảm. Phiên đang chạy còn tăng tiếp nên không so với trung bình. Mua/Bán chủ động
        là lệnh khớp ngay vào giá đang chờ (taker); cảnh báo khi một phía lớn hơn phía kia từ ngưỡng trở lên
        (cấu hình <code>MarketData:VolumeAlertExcessPercent</code>).
      </Typography.Paragraph>
    </Card>
  );
}
