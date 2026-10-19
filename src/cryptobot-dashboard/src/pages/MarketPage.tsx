import { Card, Col, Empty, Row, Space, Spin, Table, Tag, Typography } from 'antd';
import { useQuery } from '@tanstack/react-query';
import { api, futuresApi, type Signal, type Strategy } from '../api/client';
import { AutoOrdersCard } from '../features/auto/AutoOrdersCard';
import { PositionsCard } from '../features/futures/PositionsCard';
import { TradePanel } from '../features/futures/TradePanel';
import { CandleChart, EMA_COLORS } from '../features/market/CandleChart';
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
              <Space size="small">
                <Tag color={EMA_COLORS.fast}>{fastLabel}</Tag>
                <Tag color={EMA_COLORS.slow}>{slowLabel}</Tag>
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
              />
            )}
            <Typography.Paragraph type="secondary" style={{ margin: '8px 0 0', fontSize: 12 }}>
              Đánh giá đến nến mở lúc {formatVn(strategy.lastEvaluatedCandleOpenTimeUtc)}. Nến xám là nến đang chạy,
              chưa được chiến lược sử dụng. Mũi tên là tín hiệu trên nến đã đóng, không phải lệnh đã khớp.
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
