import { Flex, Tooltip, Typography } from 'antd';
import { useQuery } from '@tanstack/react-query';
import { liveApi } from '../../api/client';
import { fmtPrice2 as fmtPrice } from '../futures/format';

const signed = (v: number, digits = 2) => `${v > 0 ? '+' : ''}${v.toFixed(digits)}`;

/**
 * Sau 24 giờ giá có thể tăng/giảm bao nhiêu: khoảng đậm là ±1σ (~68% số ngày), khoảng nhạt là ±2σ (~95%).
 * Vạch giữa là giá hiện tại. Đây là biên độ, không phải dự đoán hướng.
 */
export function DailyRangeBar({ symbol }: { symbol: string }) {
  const range = useQuery({
    queryKey: ['daily-range', symbol],
    queryFn: () => liveApi.dailyRange(symbol),
    refetchInterval: 15_000,
  });
  const r = range.data;
  if (range.isError)
    return <Typography.Text type="secondary">Biên độ 24h: không đọc được ({range.error.message})</Typography.Text>;
  if (!r) return null;

  // Vị trí trên thanh: hai đầu là khoảng 95%.
  const span = r.high95 - r.low95 || 1;
  const at = (v: number) => `${((v - r.low95) / span) * 100}%`;
  const diff = (v: number) => `${signed(v - r.price)} (${signed(((v - r.price) / r.price) * 100)}%)`;

  return (
    <div style={{ marginTop: 4 }}>
      <Flex justify="space-between" align="baseline" wrap gap={8}>
        <Typography.Text strong>Sau 24 giờ giá có thể ở đâu?</Typography.Text>
        <Typography.Text type="secondary" style={{ fontSize: 12 }}>
          Tính từ {r.days} ngày gần nhất · thường dao động ±{r.typicalMovePercent.toFixed(2)}%/ngày
        </Typography.Text>
      </Flex>

      <div style={{ position: 'relative', height: 14, margin: '10px 0 6px', borderRadius: 7, background: '#e2e8f0' }}>
        <Tooltip title={`~68% số ngày: ${fmtPrice(r.low68)} – ${fmtPrice(r.high68)}`}>
          <div
            style={{
              position: 'absolute',
              top: 0,
              bottom: 0,
              left: at(r.low68),
              right: `calc(100% - ${at(r.high68)})`,
              borderRadius: 7,
              background: 'linear-gradient(90deg, #fca5a5, #e2e8f0 50%, #86efac)',
            }}
          />
        </Tooltip>
        <div style={{ position: 'absolute', top: -3, bottom: -3, left: at(r.price), width: 2, background: '#0f172a' }} />
      </div>

      <Flex justify="space-between" wrap gap={12} style={{ fontVariantNumeric: 'tabular-nums' }}>
        <Bound label="Giảm mạnh (~95%)" price={r.low95} diff={diff(r.low95)} color="#dc2626" />
        <Bound label="Giảm thường (~68%)" price={r.low68} diff={diff(r.low68)} color="#dc2626" />
        <Bound label="Tăng thường (~68%)" price={r.high68} diff={diff(r.high68)} color="#16a34a" align="end" />
        <Bound label="Tăng mạnh (~95%)" price={r.high95} diff={diff(r.high95)} color="#16a34a" align="end" />
      </Flex>

      <Typography.Paragraph type="secondary" style={{ fontSize: 12, margin: '6px 0 0' }}>
        Trong {r.days} ngày qua: {r.upDaysPercent.toFixed(0)}% số ngày tăng, trung bình mỗi ngày đi{' '}
        {r.averageMovePercent.toFixed(2)}%, ngày tăng mạnh nhất {signed(r.largestRisePercent)}%, giảm mạnh nhất{' '}
        {signed(r.largestDropPercent)}%. Đây là biên độ dựa trên quá khứ, không đoán được tăng hay giảm; ngày có tin lớn
        có thể vượt khoảng 95%.
      </Typography.Paragraph>
    </div>
  );
}

function Bound({ label, price, diff, color, align }: { label: string; price: number; diff: string; color: string; align?: 'end' }) {
  return (
    <div style={{ textAlign: align === 'end' ? 'right' : 'left' }}>
      <Typography.Text type="secondary" style={{ fontSize: 12 }}>
        {label}
      </Typography.Text>
      <div style={{ fontSize: 16 }}>{fmtPrice(price)}</div>
      <div style={{ fontSize: 12, color }}>{diff}</div>
    </div>
  );
}
