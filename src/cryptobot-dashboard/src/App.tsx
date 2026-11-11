import { useEffect, useState } from 'react';
import { Alert, Layout, Menu, Segmented, Space, Spin, Typography } from 'antd';
import { useQuery } from '@tanstack/react-query';
import { api } from './api/client';
import { StatusBar } from './components/StatusBar';
import { BacktestHistoryPage } from './pages/BacktestHistoryPage';
import { BacktestPage } from './pages/BacktestPage';
import { HistoryPage } from './pages/HistoryPage';
import { MarketPage } from './pages/MarketPage';
import { NewsPage } from './pages/NewsPage';
import { PredictionPage } from './pages/PredictionPage';
import { ReportsPage } from './pages/ReportsPage';
import { SignalsPage } from './pages/SignalsPage';
import { TradingPage } from './pages/TradingPage';
import { PAGES, routeFromHash, type Route } from './routes';

export function App() {
  const status = useQuery({ queryKey: ['status'], queryFn: api.status, refetchInterval: 10_000 });
  const [route, setRoute] = useState<Route>(routeFromHash);
  const { page } = route;
  const [selected, setSelected] = useState<number | null>(null);

  useEffect(() => {
    const onHash = () => setRoute(routeFromHash());
    window.addEventListener('hashchange', onHash);
    return () => window.removeEventListener('hashchange', onHash);
  }, []);

  const strategies = status.data?.strategies.filter((strategy) => strategy.enabled) ?? [];
  const current = strategies.find((strategy) => strategy.id === selected) ?? strategies[0];
  // Lịch sử theo tháng, báo cáo ngày và tin tức có danh sách riêng, không theo cặp/khung nến của chiến lược.
  // Lịch sử backtest chỉ dùng chiến lược cho bộ lọc, nên vẫn mở được khi chưa có chiến lược nào.
  const usesStrategy = page !== 'history' && page !== 'reports' && page !== 'news' && page !== 'backtest-history';
  // Trang chi tiết một lần backtest đã cố định cặp/khung nến: không cần chọn chiến lược.
  const showStrategyPicker =
    strategies.length > 1 && (usesStrategy || (page === 'backtest-history' && route.backtestId == null));

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
          items={PAGES.map((page) => ({ key: page.key, label: page.label }))}
        />
      </Layout.Sider>
      <Layout style={{ background: '#f8fafc' }}>
        <Layout.Content style={{ maxWidth: 1280, width: '100%', margin: '0 auto', padding: 16 }}>
          <Space orientation="vertical" size="middle" style={{ width: '100%' }}>
            <Space wrap style={{ justifyContent: 'space-between', width: '100%' }}>
              <Typography.Title level={3} style={{ margin: 0 }}>
                {PAGES.find((entry) => entry.key === page)?.label}
              </Typography.Title>
              {showStrategyPicker && (
                <Segmented
                  value={current?.id}
                  onChange={(value) => setSelected(value as number)}
                  options={strategies.map((strategy) => ({ label: `${strategy.symbol} ${strategy.interval}`, value: strategy.id }))}
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
            {page === 'news' && <NewsPage />}
            {page === 'backtest-history' && <BacktestHistoryPage strategyId={current?.id} openedId={route.backtestId} />}
            {usesStrategy && current && (
              <>
                {page === 'market' && <MarketPage key={current.id} strategy={current} />}
                {page === 'trading' && <TradingPage key={current.id} strategy={current} />}
                {page === 'prediction' && <PredictionPage key={current.id} strategy={current} />}
                {page === 'signals' && <SignalsPage key={current.id} strategy={current} />}
                {page === 'backtest' && <BacktestPage key={current.id} strategy={current} />}
              </>
            )}
          </Space>
        </Layout.Content>
      </Layout>
    </Layout>
  );
}
