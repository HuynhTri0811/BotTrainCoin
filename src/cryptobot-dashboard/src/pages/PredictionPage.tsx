import { Space } from 'antd';
import type { Strategy } from '../api/client';
import { IndicatorAccuracyCard } from '../features/prediction/IndicatorAccuracyCard';
import { PredictionCard } from '../features/prediction/PredictionCard';

/** Dự đoán nến kế tiếp và tỷ lệ đúng tín hiệu của các chỉ báo tự viết. */
export function PredictionPage({ strategy }: { strategy: Strategy }) {
  return (
    <Space orientation="vertical" size="middle" style={{ width: '100%' }}>
      <PredictionCard symbol={strategy.symbol} interval={strategy.interval} />
      <IndicatorAccuracyCard symbol={strategy.symbol} interval={strategy.interval} />
    </Space>
  );
}
