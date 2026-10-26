import { Col, Row, Space } from 'antd';
import { useQuery } from '@tanstack/react-query';
import { futuresApi, type Strategy } from '../api/client';
import { AutoOrdersCard } from '../features/auto/AutoOrdersCard';
import { PositionsCard } from '../features/futures/PositionsCard';
import { TradePanel } from '../features/futures/TradePanel';
import { LivePricePanel } from '../features/live/LivePricePanel';

/** Paper futures: mở lệnh tay, quy tắc tự mở lệnh và vị thế / lịch sử. */
export function TradingPage({ strategy }: { strategy: Strategy }) {
  const account = useQuery({
    queryKey: ['futures-account'],
    queryFn: futuresApi.account,
    refetchInterval: 2_000,
  });

  return (
    <Space orientation="vertical" size="middle" style={{ width: '100%' }}>
      <LivePricePanel symbol={strategy.symbol} />
      <Row gutter={[16, 16]}>
        <Col xs={24} lg={12}>
          <TradePanel symbol={strategy.symbol} account={account.data} />
        </Col>
        <Col xs={24} lg={12}>
          <AutoOrdersCard symbol={strategy.symbol} account={account.data} />
        </Col>
      </Row>
      <PositionsCard account={account.data} />
    </Space>
  );
}
