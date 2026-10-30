import { useEffect, useState } from 'react';
import { Alert, Layout, Menu, Segmented, Space, Spin, Typography } from 'antd';
import { useQuery } from '@tanstack/react-query';
import { api } from './api/client';
import { StatusBar } from './components/StatusBar';
import { HistoryPage } from './pages/HistoryPage';
import { MarketPage } from './pages/MarketPage';
import { PredictionPage } from './pages/PredictionPage';
import { ReportsPage } from './pages/ReportsPage';
import { SignalsPage } from './pages/SignalsPage';
import { TradingPage } from './pages/TradingPage';

const PAGES = [
  { key: 'market', label: 'Biểu đồ' },
  { key: 'trading', label: 'Giao dịch Paper' },
  { key: 'prediction', label: 'Dự đoán' },
  { key: 'signals', label: 'Tín hiệu' },
  { key: 'history', label: 'Lịch sử theo tháng' },
  { key: 'reports', label: 'Báo cáo ngày' },
] as const;

type Page = (typeof PAGES)[number]['key'];

/** Trang hiện tại nằm trong #hash để tải lại hoặc gửi link vẫn mở đúng trang. */
function pageFromHash(): Page {
  const hash = window.location.hash.slice(1);
  return PAGES.find((p) => p.key === hash)?.key ?? 'market';
}

export function App() {
  const status = useQuery({ queryKey: ['status'], queryFn: api.status, refetchInterval: 10_000 });
  const [page, setPage] = useState<Page>(pageFromHash);
  const [selected, setSelected] = useState<number | null>(null);

  useEffect(() => {
    const onHash = () => setPage(pageFromHash());
    window.addEventListener('hashchange', onHash);
    return () => window.removeEventListener('hashchange', onHash);
  }, []);

  const strategies = status.data?.strategies.filter((s) => s.enabled) ?? [];
  const current = strategies.find((s) => s.id === selected) ?? strategies[0];
  // Lịch sử theo tháng và báo cáo ngày có danh sách symbol riêng, không theo cặp/khung nến của chiến lược.
  const usesStrategy = page !== 'history' && page !== 'reports';

  return (
    <Layout style={{ minHeight: '100vh' }}>
      <Layout.Sider breakpoint="lg" collapsedWidth={0} width={200} theme="light" style={{ borderRight: '1px solid #e5e7eb' }}>
        <Typography.Title level={4} style={{ margin: 0, padding: '16px 24px' }}>
          CryptoBot
        </Typography.Title>
        <Menu
          mode="inline"
          selectedKeys={[page]}
          style={{ borderInlineEnd: 'none' }}
          onClick={({ key }) => {
            window.location.hash = key;
          }}
          items={PAGES.map((p) => ({ key: p.key, label: p.label }))}
        />
      </Layout.Sider>
      <Layout style={{ background: '#f8fafc' }}>
        <Layout.Content style={{ maxWidth: 1280, width: '100%', margin: '0 auto', padding: 16 }}>
          <Space orientation="vertical" size="middle" style={{ width: '100%' }}>
            <Space wrap style={{ justifyContent: 'space-between', width: '100%' }}>
              <Typography.Title level={3} style={{ margin: 0 }}>
                {PAGES.find((p) => p.key === page)?.label}
              </Typography.Title>
              {usesStrategy && strategies.length > 1 && (
                <Segmented
                  value={current?.id}
                  onChange={(v) => setSelected(v as number)}
                  options={strategies.map((s) => ({ label: `${s.symbol} ${s.interval}`, value: s.id }))}
                />
              )}
            </Space>

            {status.isPending && <Spin />}
            {status.isError && (
              <Alert type="error" showIcon title="Không kết nối được backend" description={status.error.message} />
            )}
            {status.data && <StatusBar status={status.data} />}

            {page === 'history' && <HistoryPage />}
            {page === 'reports' && <ReportsPage />}
            {usesStrategy && current && (
              <>
                {page === 'market' && <MarketPage key={current.id} strategy={current} />}
                {page === 'trading' && <TradingPage key={current.id} strategy={current} />}
                {page === 'prediction' && <PredictionPage key={current.id} strategy={current} />}
                {page === 'signals' && <SignalsPage key={current.id} strategy={current} />}
              </>
            )}
          </Space>
        </Layout.Content>
      </Layout>
    </Layout>
  );
}
