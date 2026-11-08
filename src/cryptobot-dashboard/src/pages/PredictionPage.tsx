import { Space } from 'antd';
import { useQuery } from '@tanstack/react-query';
import { api, type Strategy } from '../api/client';
import { SessionStatsCard } from '../features/market/SessionStatsCard';
import { IndicatorAccuracyCard } from '../features/prediction/IndicatorAccuracyCard';
import { PredictionCard } from '../features/prediction/PredictionCard';

const REFRESH_MS = 15_000;

/** Dự đoán nến kế tiếp, tỷ lệ đúng tín hiệu của các chỉ báo tự viết và thống kê giao dịch theo phiên. */
export function PredictionPage({ strategy }: { strategy: Strategy }) {
  // Cùng queryKey với trang Biểu đồ nên chuyển trang dùng lại cache, không tải thêm.
  const chart = useQuery({
    queryKey: ['chart', strategy.id],
    queryFn: () => api.chart(strategy.id, 300),
    refetchInterval: REFRESH_MS,
  });

  return (
    <Space orientation="vertical" size="middle" style={{ width: '100%' }}>
      <PredictionCard symbol={strategy.symbol} interval={strategy.interval} />
      <IndicatorAccuracyCard symbol={strategy.symbol} interval={strategy.interval} />
      {chart.data && chart.data.candles.length > 0 && (
        <SessionStatsCard candles={chart.data.candles} symbol={strategy.symbol} interval={strategy.interval} />
      )}
    </Space>
  );
}
