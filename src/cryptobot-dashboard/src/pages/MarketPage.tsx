import { useState } from 'react';
import { Alert, Card, Col, Empty, Row, Select, Space, Spin, Table, Tag, Tooltip, Typography } from 'antd';
import { useQuery } from '@tanstack/react-query';
import { api, futuresApi, indicatorApi, type Signal, type Strategy } from '../api/client';
import { AutoOrdersCard } from '../features/auto/AutoOrdersCard';
import { LivePricePanel } from '../features/live/LivePricePanel';
import { PositionsCard } from '../features/futures/PositionsCard';
import { TradePanel } from '../features/futures/TradePanel';
import { CandleChart, EMA_COLORS, INDICATOR_MARKER_COLOR } from '../features/market/CandleChart';
import { SessionStatsCard } from '../features/market/SessionStatsCard';
import { IndicatorAccuracyCard } from '../features/prediction/IndicatorAccuracyCard';
import { PredictionCard } from '../features/prediction/PredictionCard';
import { formatVn } from '../time';

const REFRESH_MS = 15_000;

const priceFormat = new Intl.NumberFormat('en-US', { maximumFractionDigits: 8 });

export function MarketPage({ strategy }: { strategy: Strategy }) {
  const chart = useQuery({
    queryKey: ['chart', strategy.id],
    queryFn: () => api.chart(strategy.id, 300),
    refetchInterval: REFRESH_MS,
  });
  const signals = useQuery({
    queryKey: ['signals', strategy.id],
    queryFn: () => api.signals(strategy.id, 50),
    refetchInterval: REFRESH_MS,
  });

  // Chỉ báo tự viết cùng symbol và khung nến với chart đang xem.
  const indicators = useQuery({ queryKey: ['indicators'], queryFn: indicatorApi.list, staleTime: 60_000 });
  const matching = (indicators.data?.indicators ?? []).filter(
    (i) => i.symbol === strategy.symbol && i.interval === strategy.interval,
  );
  const [picked, setPicked] = useState<string | null | undefined>(undefined);
  // undefined = chưa chọn: mặc định chỉ báo đầu tiên; null = người dùng tắt.
  const indicatorName = picked === undefined ? (matching[0]?.name ?? null) : picked;
  const indicatorChart = useQuery({
    queryKey: ['indicator-chart', indicatorName],
    queryFn: () => indicatorApi.chart(indicatorName!, 300),
    enabled: indicatorName != null,
    refetchInterval: REFRESH_MS,
  });

  const account = useQuery({
    queryKey: ['futures-account'],
    queryFn: futuresApi.account,
    refetchInterval: 2_000,
  });
  const openPosition = account.data?.openPositions.find((p) => p.symbol === strategy.symbol);

  const { fastPeriod, slowPeriod, lookbackCandles } = strategy.parameters;
  const fastLabel = `EMA${fastPeriod}`;
  const slowLabel = `EMA${slowPeriod}`;

  return (
    <Space orientation="vertical" size="middle" style={{ width: '100%' }}>
      <LivePricePanel symbol={strategy.symbol} />
      <Row gutter={[16, 16]}>
        <Col xs={24} lg={16}>
          <Card
            size="small"
            title={
              <Space wrap>
                <span>
                  {strategy.symbol} · {strategy.interval}
                </span>
                <Typography.Text type="secondary" style={{ fontWeight: 400 }}>
                  {strategy.name} v{strategy.version} · cửa sổ {lookbackCandles} nến
                </Typography.Text>
              </Space>
            }
            extra={
              <Space size="small" wrap>
                <Tag color={EMA_COLORS.fast}>{fastLabel}</Tag>
                <Tag color={EMA_COLORS.slow}>{slowLabel}</Tag>
                <Select<string | null>
                  size="small"
                  value={indicatorName}
                  onChange={(v) => setPicked(v ?? null)}
                  allowClear
                  placeholder={matching.length ? 'Chỉ báo của tôi' : `Chưa có chỉ báo ${strategy.interval}`}
                  disabled={matching.length === 0}
                  style={{ minWidth: 170 }}
                  options={matching.map((i) => ({ value: i.name, label: `${i.name} (${i.type})` }))}
                />
              </Space>
            }
          >
            {chart.isPending ? (
              <div style={{ height: 480, display: 'grid', placeItems: 'center' }}>
                <Spin />
              </div>
            ) : chart.isError ? (
              <Empty description={`Không tải được chart: ${chart.error.message}`} />
            ) : chart.data.candles.length === 0 ? (
              <Empty description="Chưa có nến. Backend đang tải dữ liệu lần đầu." />
            ) : (
              <CandleChart
                candles={chart.data.candles}
                signals={chart.data.signals}
                fastLabel={fastLabel}
                slowLabel={slowLabel}
                position={openPosition}
                indicator={indicatorName != null ? indicatorChart.data : undefined}
              />
            )}
            {indicatorName != null && indicatorChart.data && (
              <Typography.Paragraph style={{ margin: '8px 0 0', fontSize: 12 }}>
                <Tooltip title={indicatorChart.data.indicator.description}>
                  <Tag color={INDICATOR_MARKER_COLOR}>{indicatorChart.data.indicator.name}</Tag>
                </Tooltip>
                Nến đóng gần nhất: {indicatorChart.data.latestReason ?? '—'} · {indicatorChart.data.markers.length} tín
                hiệu trong khung đang xem (chấm tím L = Long, S = Short).
              </Typography.Paragraph>
            )}
            {indicatorChart.isError && (
              <Alert type="error" showIcon style={{ marginTop: 8 }} title={`Không tải được chỉ báo: ${indicatorChart.error.message}`} />
            )}
            {(indicators.data?.errors ?? []).map((e) => (
              <Alert
                key={e.name}
                type="warning"
                showIcon
                style={{ marginTop: 8 }}
                title={`Chỉ báo "${e.name}" bị bỏ qua: ${e.error}`}
              />
            ))}
            <Typography.Paragraph type="secondary" style={{ margin: '8px 0 0', fontSize: 12 }}>
              Đánh giá đến nến mở lúc {formatVn(strategy.lastEvaluatedCandleOpenTimeUtc)}. Nến xám là nến đang chạy,
              chưa được chiến lược sử dụng. Cột dưới đáy là khối lượng mỗi nến. Mũi tên là tín hiệu trên nến đã đóng, không phải lệnh đã khớp.
              Nến là giá Spot; vị thế futures khớp theo mark price Futures nên có thể lệch nhẹ.
            </Typography.Paragraph>
          </Card>
        </Col>
        <Col xs={24} lg={8}>
          <Space orientation="vertical" size="middle" style={{ width: '100%' }}>
            <TradePanel symbol={strategy.symbol} account={account.data} />
            <AutoOrdersCard symbol={strategy.symbol} account={account.data} />
          </Space>
        </Col>
      </Row>

      <PredictionCard symbol={strategy.symbol} interval={strategy.interval} />
      <IndicatorAccuracyCard symbol={strategy.symbol} interval={strategy.interval} />

      {chart.data && chart.data.candles.length > 0 && (
        <SessionStatsCard candles={chart.data.candles} symbol={strategy.symbol} interval={strategy.interval} />
      )}

      <PositionsCard account={account.data} />

      <Card size="small" title="Tín hiệu gần đây">
        <Table<Signal>
          rowKey="id"
          size="small"
          loading={signals.isPending}
          dataSource={signals.data ?? []}
          pagination={{ pageSize: 10, hideOnSinglePage: true }}
          scroll={{ x: true }}
          columns={[
            {
              title: 'Hành động',
              dataIndex: 'action',
              render: (a: Signal['action']) => <Tag color={a === 'Buy' ? 'green' : 'red'}>{a.toUpperCase()}</Tag>,
            },
            { title: 'Nến (mở)', dataIndex: 'candleOpenTimeUtc', render: formatVn },
            { title: 'Close', dataIndex: 'candleClose', align: 'right', render: (v: number) => priceFormat.format(v) },
            {
              title: `${fastLabel} / ${slowLabel}`,
              key: 'ema',
              align: 'right',
              render: (_, s) => `${priceFormat.format(s.fastEma)} / ${priceFormat.format(s.slowEma)}`,
            },
            { title: 'Lý do', dataIndex: 'reason' },
            {
              title: 'Loại',
              dataIndex: 'isBackfill',
              render: (b: boolean) =>
                b ? <Tag>Backfill</Tag> : <Tag color="processing">Mới</Tag>,
            },
            { title: 'Tạo lúc', dataIndex: 'createdAtUtc', render: formatVn },
          ]}
        />
      </Card>
    </Space>
  );
}
