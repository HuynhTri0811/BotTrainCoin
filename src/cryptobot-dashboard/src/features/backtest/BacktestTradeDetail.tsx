import { useMemo } from 'react';
import { Alert, Space, Spin, Typography } from 'antd';
import { useQuery } from '@tanstack/react-query';
import { backtestApi, type BacktestResult, type BacktestTrade } from '../../api/client';
import { formatVn } from '../../time';
import { fmtPrice2, fmtSignedPercent } from '../futures/format';
import { BacktestChart } from './BacktestChart';
import { tradeLevels } from './describe';
import { EMA_FAST_COLOR, EMA_SLOW_COLOR, LIQUIDATION_COLOR, SL_COLOR, TP_COLOR, sideColor } from './tradeOverlay';

interface Props {
  result: BacktestResult;
  trade: BacktestTrade;
}

/**
 * Chart nến thật của một lệnh (từ 30 nến trước lúc vào đến 30 nến sau lúc ra), kèm EMA (nếu tín hiệu là EMA), giá
 * vào, TP/SL và giá thanh lý (Futures).
 */
export function BacktestTradeDetail({ result, trade }: Props) {
  const chart = useQuery({
    queryKey: ['backtest-trade-chart', result.id, trade.entryTimeUtc, trade.exitTimeUtc],
    // Gửi giờ vào/ra đã có: server chỉ đọc thông số lần chạy, không giải nén lại toàn bộ kết quả.
    queryFn: () => backtestApi.tradeChart(result.id, trade.entryTimeUtc, trade.exitTimeUtc),
    // Nến đã đóng không đổi.
    staleTime: Infinity,
  });
  const trades = useMemo(
    () => [{ entryTimeUtc: trade.entryTimeUtc, entryPrice: trade.entryPrice, side: trade.side, exit: trade }],
    [trade],
  );
  const { settings, parameters } = result;
  const futures = settings.futures != null;
  const { takeProfit, stopLoss, liquidation } = tradeLevels(trade.entryPrice, trade.side, settings);
  const entryVerb = futures ? `Mở ${trade.side}` : 'Mua';
  const exitVerb = futures ? 'đóng' : 'bán';

  return (
    <Space orientation="vertical" style={{ width: '100%' }}>
      <Typography.Text>
        {entryVerb} lúc <b>{formatVn(trade.entryTimeUtc)}</b> giá{' '}
        <b style={{ color: sideColor(trade.side) }}>{fmtPrice2(trade.entryPrice)}</b>
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
        {liquidation != null && (
          <>
            {' '}· thanh lý ở <b style={{ color: LIQUIDATION_COLOR }}>{fmtPrice2(liquidation)}</b>
          </>
        )}
        {' '}→ {exitVerb} lúc <b>{formatVn(trade.exitTimeUtc)}</b> giá <b>{fmtPrice2(trade.exitPrice)}</b> (
        {fmtSignedPercent(trade.returnPercent)}
        {futures && ' trên margin'}).
      </Typography.Text>
      {chart.isPending && <Spin />}
      {chart.isError && <Alert type="error" showIcon title={chart.error.message} />}
      {chart.data && (
        <>
          <BacktestChart bars={chart.data.bars} trades={trades} settings={settings} parameters={parameters} priceHeight={340} />
          <Typography.Text type="secondary">
            {chart.data.candlesPerBar > 1 ? `Mỗi cây nến gộp ${chart.data.candlesPerBar} nến ${result.interval}. ` : `Nến ${result.interval} thật. `}
            {parameters ? (
              <>
                <span style={{ color: EMA_FAST_COLOR }}>— EMA{parameters.fastPeriod}</span> ·{' '}
                <span style={{ color: EMA_SLOW_COLOR }}>— EMA{parameters.slowPeriod}</span>:{' '}
                {futures
                  ? `Long khi EMA${parameters.fastPeriod} cắt lên, Short khi cắt xuống.`
                  : `BUY khi EMA${parameters.fastPeriod} cắt lên, SELL khi cắt xuống.`}{' '}
              </>
            ) : (
              `Tín hiệu từ chỉ báo ${result.signal.type}. `
            )}
            <span style={{ color: sideColor(trade.side) }}>— giá vào</span>
            {takeProfit != null && <span style={{ color: TP_COLOR }}> · - - chốt lời</span>}
            {stopLoss != null && <span style={{ color: SL_COLOR }}> · - - cắt lỗ</span>}
            {liquidation != null && <span style={{ color: LIQUIDATION_COLOR }}> · ··· thanh lý</span>}.
          </Typography.Text>
        </>
      )}
    </Space>
  );
}
