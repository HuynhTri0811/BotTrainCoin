import { useState } from 'react';
import { Flex, Segmented, Tooltip, Typography } from 'antd';
import { useQuery } from '@tanstack/react-query';
import { liveApi, type SimilarDays } from '../../api/client';
import { fmtPrice2 as fmtPrice } from '../futures/format';
import { formatVn } from '../../time';

const signed = (value: number, digits = 2) => `${value > 0 ? '+' : ''}${value.toFixed(digits)}`;

const HORIZONS = [
  { value: 1, label: '24 giờ' },
  { value: 7, label: '7 ngày' },
  { value: 30, label: '30 ngày' },
];

/**
 * Sau 24 giờ / 7 ngày / 30 ngày giá có thể tăng/giảm bao nhiêu: khoảng đậm là ±1σ (~68% trường hợp),
 * khoảng nhạt là ±2σ (~95%). Vạch giữa là giá hiện tại. Đây là biên độ, không phải dự đoán hướng.
 */
export function DailyRangeBar({ symbol }: { symbol: string }) {
  const [days, setDays] = useState(1);
  const range = useQuery({
    queryKey: ['daily-range', symbol, days],
    queryFn: () => liveApi.dailyRange(symbol, days),
    refetchInterval: 15_000,
    placeholderData: (prev) => prev,
  });
  const dailyRange = range.data;
  const label = HORIZONS.find((horizon) => horizon.value === days)?.label ?? `${days} ngày`;
  const period = days === 1 ? 'ngày' : `đoạn ${days} ngày`;
  if (range.isError)
    return <Typography.Text type="secondary">Biên độ {label}: không đọc được ({range.error.message})</Typography.Text>;
  if (!dailyRange) return null;

  // Vị trí trên thanh: hai đầu là khoảng 95%.
  const span = dailyRange.high95 - dailyRange.low95 || 1;
  const at = (value: number) => `${((value - dailyRange.low95) / span) * 100}%`;
  const diff = (value: number) => `${signed(value - dailyRange.price)} (${signed(((value - dailyRange.price) / dailyRange.price) * 100)}%)`;

  return (
    <div style={{ marginTop: 4 }}>
      <Flex justify="space-between" align="baseline" wrap gap={8}>
        <Flex align="center" gap={8} wrap>
          <Typography.Text strong>Sau {label} giá có thể ở đâu?</Typography.Text>
          <Segmented<number> size="small" value={days} onChange={setDays} options={HORIZONS} />
        </Flex>
        <Typography.Text type="secondary" style={{ fontSize: 12 }}>
          Tính từ {dailyRange.days} ngày gần nhất · thường dao động ±{dailyRange.typicalMovePercent.toFixed(2)}%
          {days === 1 ? '/ngày' : ` sau ${days} ngày`}
        </Typography.Text>
      </Flex>

      <div style={{ position: 'relative', height: 14, margin: '10px 0 6px', borderRadius: 7, background: '#e2e8f0' }}>
        <Tooltip title={`~68% trường hợp: ${fmtPrice(dailyRange.low68)} – ${fmtPrice(dailyRange.high68)}`}>
          <div
            style={{
              position: 'absolute',
              top: 0,
              bottom: 0,
              left: at(dailyRange.low68),
              right: `calc(100% - ${at(dailyRange.high68)})`,
              borderRadius: 7,
              background: 'linear-gradient(90deg, #fca5a5, #e2e8f0 50%, #86efac)',
            }}
          />
        </Tooltip>
        <div style={{ position: 'absolute', top: -3, bottom: -3, left: at(dailyRange.price), width: 2, background: '#0f172a' }} />
      </div>
      {dailyRange.similarDays && <SimilarBand similar={dailyRange.similarDays} at={at} price={dailyRange.price} />}

      <Flex justify="space-between" wrap gap={12} style={{ fontVariantNumeric: 'tabular-nums' }}>
        <Bound label="Giảm mạnh (~95%)" price={dailyRange.low95} diff={diff(dailyRange.low95)} color="#dc2626" />
        <Bound label="Giảm thường (~68%)" price={dailyRange.low68} diff={diff(dailyRange.low68)} color="#dc2626" />
        <Bound label="Tăng thường (~68%)" price={dailyRange.high68} diff={diff(dailyRange.high68)} color="#16a34a" align="end" />
        <Bound label="Tăng mạnh (~95%)" price={dailyRange.high95} diff={diff(dailyRange.high95)} color="#16a34a" align="end" />
      </Flex>

      <Typography.Paragraph type="secondary" style={{ fontSize: 12, margin: '6px 0 0' }}>
        Trong {dailyRange.samples} {period} của lịch sử: {dailyRange.upDaysPercent.toFixed(0)}% số lần giá tăng, trung bình giá đi{' '}
        {dailyRange.averageMovePercent.toFixed(2)}%, tăng mạnh nhất {signed(dailyRange.largestRisePercent)}%, giảm mạnh nhất{' '}
        {signed(dailyRange.largestDropPercent)}%. Đây là biên độ dựa trên quá khứ, không đoán được tăng hay giảm; giai đoạn có tin
        lớn hoặc xu hướng mạnh có thể vượt khoảng 95%.
      </Typography.Paragraph>
      {dailyRange.similarDays && <SimilarSummary similar={dailyRange.similarDays} />}
    </div>
  );
}

const LEAN = {
  Up: { text: 'Nghiêng tăng', color: '#16a34a', band: 'rgba(22, 163, 74, 0.55)' },
  Down: { text: 'Nghiêng giảm', color: '#dc2626', band: 'rgba(220, 38, 38, 0.55)' },
  Neutral: { text: 'Trung lập', color: '#64748b', band: 'rgba(100, 116, 139, 0.45)' },
} as const;

/** Khoảng ~68% của các ngày tương tự, cùng thang với thanh biên độ ở trên (bị cắt ở hai đầu khoảng 95%). */
function SimilarBand({ similar, at, price }: { similar: SimilarDays; at: (value: number) => string; price: number }) {
  const lean = LEAN[similar.lean];
  const clamp = (value: string) => `${Math.min(100, Math.max(0, parseFloat(value)))}%`;
  return (
    <Tooltip title={`Ngày tương tự, ~68%: ${fmtPrice(similar.low68)} – ${fmtPrice(similar.high68)}`}>
      <div style={{ position: 'relative', height: 6, margin: '0 0 6px' }}>
        <div
          style={{
            position: 'absolute',
            top: 0,
            bottom: 0,
            left: clamp(at(similar.low68)),
            right: `calc(100% - ${clamp(at(similar.high68))})`,
            borderRadius: 3,
            background: lean.band,
          }}
        />
        <div style={{ position: 'absolute', top: -2, bottom: -2, left: clamp(at(price)), width: 2, background: '#0f172a' }} />
      </div>
    </Tooltip>
  );
}

function SimilarSummary({ similar }: { similar: SimilarDays }) {
  const lean = LEAN[similar.lean];
  return (
    <div style={{ marginTop: 8, padding: '8px 10px', borderRadius: 6, background: '#f1f5f9', fontSize: 12 }}>
      <Flex justify="space-between" wrap gap={8} align="baseline">
        <span>
          <Typography.Text strong>Ngày tương tự · giá đóng cửa hôm nay: </Typography.Text>
          <span style={{ fontWeight: 600, color: lean.color }}>
            {similar.upPercent.toFixed(0)}% tăng · {lean.text}
          </span>
        </span>
        <span style={{ fontVariantNumeric: 'tabular-nums' }}>
          ~68%: {fmtPrice(similar.low68)} ({signed(similar.low68Percent)}%) – {fmtPrice(similar.high68)} ({signed(similar.high68Percent)}%)
          · trung vị {signed(similar.medianPercent)}%
        </span>
      </Flex>
      <Typography.Paragraph type="secondary" style={{ fontSize: 12, margin: '4px 0 0' }}>
        Dự báo giá đóng cửa lúc {formatVn(similar.targetCloseUtc)}, tính % từ giá đóng hôm qua {fmtPrice(similar.anchorPrice)}{' '}
        (không phải từ giá hiện tại, vì phần giá đã chạy trong ngày đã nằm trong đó). {similar.neighbors} ngày trong lịch sử{' '}
        {similar.historySymbol} ({similar.exchange}) giống hôm qua nhất về khoảng cách
        tới EMA200/EMA50, RSI, % thay đổi 7 ngày và 1 ngày. Chạy thử trên {similar.testedDays} ngày: đúng{' '}
        {similar.hitRatePercent.toFixed(1)}%; riêng {similar.strongDays} ngày nghiêng rõ (≥ 60% hoặc ≤ 40%) đúng{' '}
        {similar.strongHitRatePercent.toFixed(1)}%. Chỉ tin khi nghiêng rõ, và đây vẫn là thống kê quá khứ.
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
