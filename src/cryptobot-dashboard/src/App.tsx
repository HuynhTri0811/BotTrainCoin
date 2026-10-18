import { useState } from 'react';
import { Alert, Layout, Segmented, Space, Spin, Typography } from 'antd';
import { useQuery } from '@tanstack/react-query';
import { api } from './api/client';
import { StatusBar } from './components/StatusBar';
import { MarketPage } from './pages/MarketPage';

export function App() {
  const status = useQuery({ queryKey: ['status'], queryFn: api.status, refetchInterval: 10_000 });
  const [selected, setSelected] = useState<number | null>(null);

  const strategies = status.data?.strategies.filter((s) => s.enabled) ?? [];
  const current = strategies.find((s) => s.id === selected) ?? strategies[0];

  return (
    <Layout style={{ minHeight: '100vh', background: '#f8fafc' }}>
      <Layout.Content style={{ maxWidth: 1280, width: '100%', margin: '0 auto', padding: 16 }}>
        <Space orientation="vertical" size="middle" style={{ width: '100%' }}>
          <Typography.Title level={3} style={{ margin: 0 }}>
            CryptoBot
          </Typography.Title>

          {status.isPending && <Spin />}
          {status.isError && (
            <Alert type="error" showIcon title="Không kết nối được backend" description={status.error.message} />
          )}
          {status.data && <StatusBar status={status.data} />}

          {strategies.length > 1 && (
            <Segmented
              value={current?.id}
              onChange={(v) => setSelected(v as number)}
              options={strategies.map((s) => ({ label: `${s.symbol} ${s.interval}`, value: s.id }))}
            />
          )}

          {current && <MarketPage key={current.id} strategy={current} />}
        </Space>
      </Layout.Content>
    </Layout>
  );
}
