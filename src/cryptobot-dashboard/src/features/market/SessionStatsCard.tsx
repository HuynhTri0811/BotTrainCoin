import { Alert, Card, Col, Row, Statistic, Table, Tag, Typography } from 'antd';
import type { ChartPoint, VolumePressure } from '../../api/client';
import { formatVnUnix } from '../../time';

/** Số phiên dùng để tính mức trung bình so sánh. */
const AVERAGE_OVER = 20;

const count = new Intl.NumberFormat('en-US');
const coin = new Intl.NumberFormat('en-US', { maximumFractionDigits: 3 });
const compactUsdt = new Intl.NumberFormat('en-US', { notation: 'compact', maximumFractionDigits: 2 });

const fmtCount = (value: number | null) => (value == null ? '—' : count.format(value));
const fmtQuote = (value: number | null) => (value == null ? '—' : `${compactUsdt.format(value)} USDT`);
const fmtExcess = (percent: VolumePressure) => (percent.excessPercent == null ? '100% một phía' : `+${percent.excessPercent.toFixed(0)}%`);

/** Thanh tỷ trọng mua (xanh) / bán (đỏ) kèm cảnh báo khi một phía áp đảo. */
function PressureCell({ percent }: { percent: VolumePressure | null }) {
  if (!percent) return <>—</>;
  return (
    <span style={{ display: 'inline-flex', alignItems: 'center', gap: 8 }}>
      <span style={{ display: 'inline-flex', width: 80, height: 8, borderRadius: 4, overflow: 'hidden', background: '#dc2626' }}>
        <span style={{ width: `${percent.buyPercent}%`, background: '#16a34a' }} />
      </span>
      <span style={{ fontVariantNumeric: 'tabular-nums' }}>
        {percent.buyPercent.toFixed(0)}/{(100 - percent.buyPercent).toFixed(0)}
      </span>
      {percent.side === 'SellHeavy' && <Tag color="red">Bán áp đảo {fmtExcess(percent)}</Tag>}
      {percent.side === 'BuyHeavy' && <Tag color="green">Mua áp đảo {fmtExcess(percent)}</Tag>}
    </span>
  );
}

/** Cảnh báo phiên vừa đóng và phiên đang chạy khi một phía vượt ngưỡng. */
function PressureAlert({ candle, running }: { candle: ChartPoint | undefined; running: boolean }) {
  const percent = candle?.pressure;
  if (!candle || !percent || percent.side === 'Balanced') return null;
  const sell = percent.side === 'SellHeavy';
  const which = running ? 'Phiên đang chạy' : 'Phiên vừa đóng';
  return (
    <Alert
      showIcon
      type={sell ? 'error' : 'success'}
      style={{ marginBottom: 12 }}
      title={`${which} (${formatVnUnix(candle.time)}): ${sell ? 'bán' : 'mua'} chủ động lớn hơn ${sell ? 'mua' : 'bán'} ${fmtExcess(percent)}`}
      description={
        `Mua ${coin.format(percent.buyVolume)} · Bán ${coin.format(percent.sellVolume)} (ngưỡng cảnh báo ${percent.alertExcessPercent}%).` +
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
  const closed = candles.filter((point) => point.isClosed && point.tradeCount != null);
  const recent = closed.slice(-AVERAGE_OVER);
  const avgTrades = recent.length ? recent.reduce((sum, candle) => sum + candle.tradeCount!, 0) / recent.length : null;
  const avgQuote = recent.length ? recent.reduce((sum, candle) => sum + (candle.quoteVolume ?? 0), 0) / recent.length : null;
  const last = closed.at(-1);
  const running = candles.at(-1)?.isClosed === false ? candles.at(-1) : undefined;
  const baseAsset = symbol.replace(/USDT$/, '');

  const rows: SessionRow[] = candles
    .slice(-50)
    .map((point) => {
      const before = closed.filter((previous) => previous.time < point.time).slice(-AVERAGE_OVER);
      const avg = before.length ? before.reduce((sum, previous) => sum + previous.tradeCount!, 0) / before.length : null;
      return {
        time: point.time,
        tradeCount: point.tradeCount,
        volume: point.volume,
        quoteVolume: point.quoteVolume,
        up: point.close >= point.open,
        isClosed: point.isClosed,
        pressure: point.pressure,
        vsAverage: point.tradeCount != null && avg ? (point.tradeCount / avg - 1) * 100 : null,
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
            render: (_, row) => (
              <span>
                {formatVnUnix(row.time)} {!row.isClosed && <Tag>Đang chạy</Tag>}
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
            render: (value: number, row) => <span style={{ color: row.up ? '#16a34a' : '#dc2626' }}>{coin.format(value)}</span>,
          },
          { title: 'Khối lượng (USDT)', dataIndex: 'quoteVolume', align: 'right', render: fmtQuote },
          {
            title: 'Mua / Bán chủ động (%)',
            key: 'pressure',
            render: (_, row) => <PressureCell percent={row.pressure} />,
          },
          {
            title: `So với TB ${AVERAGE_OVER} phiên`,
            dataIndex: 'vsAverage',
            align: 'right',
            render: (value: number | null, row) =>
              value == null || !row.isClosed ? (
                '—'
              ) : (
                <span style={{ color: value >= 0 ? '#16a34a' : '#64748b' }}>
                  {value > 0 ? '+' : ''}
                  {value.toFixed(0)}%
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
