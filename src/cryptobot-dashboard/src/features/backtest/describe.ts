import {
  EMA_CROSS_SIGNAL,
  type BacktestDirection,
  type BacktestSettings,
  type BacktestSignal,
  type StrategyParameters,
  type TradeSide,
} from '../../api/client';

export const DIRECTION_LABELS: Record<BacktestDirection, string> = {
  LongOnly: 'Chỉ Long',
  ShortOnly: 'Chỉ Short',
  Both: 'Long + Short',
};

export const isEmaCross = (signal: BacktestSignal) => signal.type === EMA_CROSS_SIGNAL;

/** `EMA9/21 · 300` hoặc `MyRsi · Period 14 · Oversold 30 …`. */
export function fmtSignal(signal: BacktestSignal, parameters: StrategyParameters | null): string {
  if (isEmaCross(signal) && parameters)
    return `EMA${parameters.fastPeriod}/${parameters.slowPeriod} · ${parameters.lookbackCandles}`;
  const values = Object.entries(signal.parameters).map(([name, value]) => `${name} ${value}`);
  return [signal.type, ...values].join(' · ');
}

/** Tên ngắn của tín hiệu cho tiêu đề: `EMA9/21` hoặc `MyRsi`. */
export const signalName = (signal: BacktestSignal, parameters: StrategyParameters | null) =>
  isEmaCross(signal) && parameters ? `EMA${parameters.fastPeriod}/${parameters.slowPeriod}` : signal.type;

/** `Spot` hoặc `Futures 3x · Long + Short`, thêm ghi chú khi không đóng lệnh lúc đảo chiều. */
export function fmtMode(settings: BacktestSettings): string {
  const futures = settings.futures;
  if (!futures) return 'Spot';
  const reverse = futures.closeOnReverse ? '' : ' · giữ tới TP/SL';
  return `Futures ${futures.leverage}x · ${DIRECTION_LABELS[futures.direction]}${reverse}`;
}

/** Mức chốt lời, cắt lỗ và giá thanh lý của một lệnh, tính từ giá vào như lúc giả lập. */
export function tradeLevels(entryPrice: number, side: TradeSide, settings: BacktestSettings) {
  const direction = side === 'Long' ? 1 : -1;
  const futures = settings.futures;
  // Giống FuturesMath.LiquidationPrice: Long entry × (1 − 1/L) / (1 − mmr), Short entry × (1 + 1/L) / (1 + mmr).
  const liquidation = futures
    ? (entryPrice * (1 - direction / futures.leverage)) / (1 - (direction * futures.maintenanceMarginPercent) / 100)
    : null;
  return {
    takeProfit: settings.takeProfitPercent == null ? null : entryPrice * (1 + (direction * settings.takeProfitPercent) / 100),
    stopLoss: settings.stopLossPercent == null ? null : entryPrice * (1 - (direction * settings.stopLossPercent) / 100),
    // Long 1x có giá thanh lý 0: không vẽ.
    liquidation: liquidation != null && liquidation > 0 ? liquidation : null,
  };
}
