import { useEffect, useState } from 'react';
import { Alert, Card, Flex, Progress, Space, Statistic, Tag, Tooltip, Typography } from 'antd';
import { useQuery } from '@tanstack/react-query';
import { liveApi, type LiveSample } from '../../api/client';
import { fmtPrice2 as fmtPrice } from '../futures/format';
import { DailyRangeBar } from './DailyRangeBar';
import { Sparkline } from './Sparkline';

interface Row extends LiveSample {
  key: string;
  deltaPct: number | null;
}

const pct = (value: number) => `${value > 0 ? '+' : ''}${value.toFixed(3)}%`;
const deltaColor = (value: number | null) => (value == null || value === 0 ? undefined : value > 0 ? '#16a34a' : '#dc2626');

export function LivePricePanel({ symbol }: { symbol: string }) {
  const live = useQuery({
    queryKey: ['live-prices'],
    queryFn: () => liveApi.prices(60),
    refetchInterval: 3_000,
  });

  // Đồng hồ cục bộ chỉ để đếm ngược hiển thị; mốc thời gian lấy giá do backend quyết định.
  const [now, setNow] = useState(() => Date.now());
  useEffect(() => {
    const id = setInterval(() => setNow(Date.now()), 1_000);
    return () => clearInterval(id);
  }, []);

  const interval = live.data?.intervalSeconds ?? 15;
  const samples = live.data?.symbols.find((entry) => entry.symbol === symbol)?.samples ?? [];
  const latest = samples.at(-1);

  const rows: Row[] = samples
    .map((sample, index) => {
      const prev = samples[index - 1]?.spotPrice;
      const deltaPct = prev && sample.spotPrice ? ((sample.spotPrice - prev) / prev) * 100 : null;
      return { ...sample, key: sample.timeUtc, deltaPct };
    })
    .reverse();

  // Độ lệch giữa giờ máy và giờ server, để đếm ngược không sai khi đồng hồ máy lệch.
  const skew = live.data ? Date.parse(live.data.serverTimeUtc) - (live.dataUpdatedAt || now) : 0;
  const sinceLast = latest ? (now + skew - Date.parse(latest.timeUtc)) / 1000 : null;
  const remaining = sinceLast == null ? null : Math.max(0, Math.ceil(interval - sinceLast));
  const late = sinceLast != null && sinceLast > interval * 2;
  const progress = sinceLast == null ? 0 : Math.min(100, (sinceLast / interval) * 100);

  const lastChange = rows[0]?.deltaPct ?? null;
  const spread = latest?.spotPrice && latest.markPrice ? latest.markPrice - latest.spotPrice : null;

  return (
    <Card
      size="small"
      title={
        <Space>
          <span>Giá trực tiếp</span>
          <Tag color="processing">mỗi {interval}s</Tag>
          <Typography.Text type="secondary" style={{ fontWeight: 400 }}>
            {symbol} · nguồn Binance
          </Typography.Text>
        </Space>
      }
      extra={
        late ? (
          <Tag color="error">Chậm {Math.round(sinceLast!)}s</Tag>
        ) : (
          <Tooltip title="Thời gian đến lần lấy giá tiếp theo">
            <Typography.Text type="secondary">
              Lần tới: {remaining == null ? '—' : remaining === 0 ? 'đang lấy…' : `${remaining}s`}
            </Typography.Text>
          </Tooltip>
        )
      }
    >
      {live.isError && <Alert type="error" showIcon title="Không đọc được giá trực tiếp" description={live.error.message} />}

      <Progress percent={progress} showInfo={false} size="small" status={late ? 'exception' : 'active'} />

      <Flex wrap gap={24} align="center" style={{ margin: '8px 0 12px' }}>
        <Statistic
          title="Spot (giá gần nhất)"
          value={latest?.spotPrice != null ? fmtPrice(latest.spotPrice) : '—'}
          styles={{ content: { fontSize: 26, color: deltaColor(lastChange) } }}
          suffix={
            lastChange != null ? (
              <span style={{ fontSize: 13, color: deltaColor(lastChange) }}> {pct(lastChange)}</span>
            ) : null
          }
        />
        <Statistic
          title="Mark (Futures)"
          value={latest?.markPrice != null ? fmtPrice(latest.markPrice) : '—'}
          styles={{ content: { fontSize: 20 } }}
        />
        <Statistic
          title="Mark − Spot"
          value={spread != null ? `${spread > 0 ? '+' : ''}${spread.toFixed(2)}` : '—'}
          styles={{ content: { fontSize: 20 } }}
        />
        <Sparkline values={samples.flatMap((sample) => (sample.spotPrice != null ? [sample.spotPrice] : []))} />
      </Flex>

      <DailyRangeBar symbol={symbol} />

      {latest?.error && <Alert type="warning" showIcon title={latest.error} style={{ marginBottom: 8 }} />}
    </Card>
  );
}
