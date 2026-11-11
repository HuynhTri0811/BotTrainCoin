import { useMemo } from 'react';
import { Alert, Space, Spin, Typography } from 'antd';
import { useQuery } from '@tanstack/react-query';
import { backtestApi, type BacktestResult, type BacktestTrade } from '../../api/client';
import { formatVn } from '../../time';
import { fmtPrice2, fmtSignedPercent } from '../futures/format';
import { BacktestChart } from './BacktestChart';
import { EMA_FAST_COLOR, EMA_SLOW_COLOR, ENTRY_COLOR, SL_COLOR, TP_COLOR } from './tradeOverlay';

interface Props {
  result: BacktestResult;
  trade: BacktestTrade;
  /** Vị trí của lệnh trong `result.trades`. */
  tradeIndex: number;
}

/** Chart nến thật của một lệnh (từ 30 nến trước lúc vào đến 30 nến sau lúc ra), kèm EMA, giá vào và TP/SL. */
export function BacktestTradeDetail({ result, trade, tradeIndex }: Props) {
  const chart = useQuery({
    queryKey: ['backtest-trade-chart', result.id, tradeIndex],
    queryFn: () => backtestApi.tradeChart(result.id, tradeIndex),
    // Nến đã đóng không đổi.
    staleTime: Infinity,
  });
  const trades = useMemo(
    () => [{ entryTimeUtc: trade.entryTimeUtc, entryPrice: trade.entryPrice, exit: trade }],
    [trade],
  );
  const { settings, parameters } = result;
  const takeProfit = settings.takeProfitPercent == null ? null : trade.entryPrice * (1 + settings.takeProfitPercent / 100);
  const stopLoss = settings.stopLossPercent == null ? null : trade.entryPrice * (1 - settings.stopLossPercent / 100);

  return (
    <Space orientation="vertical" style={{ width: '100%' }}>
      <Typography.Text>
        Mua lúc <b>{formatVn(trade.entryTimeUtc)}</b> giá <b style={{ color: ENTRY_COLOR }}>{fmtPrice2(trade.entryPrice)}</b>
        {takeProfit != null && (
          <>
            {' '}· chốt lời ở <b style={{ color: TP_COLOR }}>{fmtPrice2(takeProfit)}</b>
          </>
        )}
        {stopLoss != null && (
          <>
            {' '}· cắt lỗ ở <b style={{ color: SL_COLOR }}>{fmtPrice2(stopLoss)}</b>
          </>
        )}
        {' '}→ bán lúc <b>{formatVn(trade.exitTimeUtc)}</b> giá <b>{fmtPrice2(trade.exitPrice)}</b> ({fmtSignedPercent(trade.returnPercent)}).
      </Typography.Text>
      {chart.isPending && <Spin />}
      {chart.isError && <Alert type="error" showIcon title={chart.error.message} />}
      {chart.data && (
        <>
          <BacktestChart bars={chart.data.bars} trades={trades} settings={settings} parameters={parameters} priceHeight={340} />
          <Typography.Text type="secondary">
            {chart.data.candlesPerBar > 1 ? `Mỗi cây nến gộp ${chart.data.candlesPerBar} nến ${result.interval}. ` : `Nến ${result.interval} thật. `}
            <span style={{ color: EMA_FAST_COLOR }}>— EMA{parameters.fastPeriod}</span> ·{' '}
            <span style={{ color: EMA_SLOW_COLOR }}>— EMA{parameters.slowPeriod}</span>: BUY khi EMA{parameters.fastPeriod} cắt lên,
            SELL khi cắt xuống. <span style={{ color: ENTRY_COLOR }}>— giá vào</span>
            {takeProfit != null && <span style={{ color: TP_COLOR }}> · - - chốt lời</span>}
            {stopLoss != null && <span style={{ color: SL_COLOR }}> · - - cắt lỗ</span>}.
          </Typography.Text>
        </>
      )}
    </Space>
  );
}
