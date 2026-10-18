// Hợp đồng REST của backend (xem CryptoBot.Api/Endpoints/BotEndpoints.cs).
// Số tiền/giá chỉ dùng để hiển thị; mọi tính toán nằm ở backend.

export interface StrategyParameters {
  fastPeriod: number;
  slowPeriod: number;
  lookbackCandles: number;
}

export interface Strategy {
  id: number;
  name: string;
  type: string;
  version: number;
  exchange: string;
  symbol: string;
  interval: string;
  enabled: boolean;
  lastEvaluatedCandleOpenTimeUtc: string | null;
  parameters: StrategyParameters;
}

export interface FeedStatus {
  key: string;
  isStale: boolean;
  lastSuccessAtUtc: string | null;
  lastAttemptAtUtc: string | null;
  lastError: string | null;
  consecutiveFailures: number;
  latestClosedOpenTimeUtc: string | null;
  recentIssues: string[];
}

export interface Status {
  decisionMode: 'SignalOnly' | 'Auto';
  environment: 'Paper' | 'Testnet' | 'Live';
  liveLocked: boolean;
  serverTimeUtc: string;
  feeds: FeedStatus[];
  strategies: Strategy[];
}

export interface ChartPoint {
  time: number; // Unix seconds, UTC
  open: number;
  high: number;
  low: number;
  close: number;
  volume: number;
  isClosed: boolean;
  fastEma: number | null;
  slowEma: number | null;
}

export interface Signal {
  id: number;
  strategyInstanceId: number;
  configVersion: number;
  symbol: string;
  interval: string;
  candleOpenTimeUtc: string;
  time: number;
  action: 'Buy' | 'Sell';
  reason: string;
  candleClose: number;
  fastEma: number;
  slowEma: number;
  createdAtUtc: string;
  isBackfill: boolean;
}

export interface ChartData {
  strategy: Strategy;
  candles: ChartPoint[];
  signals: Signal[];
}

async function getJson<T>(url: string): Promise<T> {
  const res = await fetch(url, { headers: { Accept: 'application/json' }, cache: 'no-store' });
  if (!res.ok) throw new Error(`${res.status} ${res.statusText} — ${url}`);
  return (await res.json()) as T;
}

export const api = {
  status: () => getJson<Status>('/api/status'),
  chart: (strategyId: number, limit = 300) =>
    getJson<ChartData>(`/api/strategies/${strategyId}/chart?limit=${limit}`),
  signals: (strategyId: number, limit = 50) =>
    getJson<Signal[]>(`/api/signals?strategyId=${strategyId}&limit=${limit}`),
};

// ---- Paper futures (môi trường nằm trong đường dẫn /api/paper/...) ----

export type PositionSide = 'Long' | 'Short';

export interface FuturesPosition {
  id: number;
  environment: 'Paper';
  symbol: string;
  side: PositionSide;
  leverage: number;
  quantity: number;
  entryPrice: number;
  margin: number;
  liquidationPrice: number;
  stopLoss: number;
  takeProfit: number | null;
  openFee: number;
  closeFee: number;
  status: 'Open' | 'Closed' | 'Liquidated';
  closeReason: 'Manual' | 'StopLoss' | 'TakeProfit' | 'Liquidation' | null;
  exitPrice: number | null;
  realizedPnl: number | null;
  netPnl: number | null;
  openedAtUtc: string;
  closedAtUtc: string | null;
  markPrice: number | null;
  unrealizedPnl: number | null;
  roePercent: number | null;
}

export interface FuturesAccount {
  environment: 'Paper';
  walletBalance: number;
  usedMargin: number;
  unrealizedPnl: number;
  equity: number;
  availableBalance: number;
  openPositions: FuturesPosition[];
  markPrices: { symbol: string; price: number; timeUtc: string }[];
  rules: { maxLeverage: number; takerFeeRate: number; slippageRate: number; symbols: string[] };
}

/** Số tiền gửi dạng chuỗi để không mất chính xác qua JSON number. */
export interface OpenRequest {
  commandId: string;
  symbol: string;
  side: PositionSide;
  leverage: number;
  margin: string;
  stopLoss: string;
  takeProfit: string | null;
}

export interface FuturesQuote {
  accepted: boolean;
  errors: string[];
  symbol: string;
  side: PositionSide;
  leverage: number;
  markPrice: number | null;
  fillPrice: number | null;
  quantity: number | null;
  notional: number | null;
  margin: number | null;
  openFee: number | null;
  liquidationPrice: number | null;
  lossAtStop: number | null;
  profitAtTakeProfit: number | null;
  availableBalance: number;
}

export class ApiError extends Error {
  constructor(
    message: string,
    public readonly details: string[] = [],
  ) {
    super(message);
  }
}

async function postJson<T>(url: string, body: unknown): Promise<T> {
  const res = await fetch(url, {
    method: 'POST',
    cache: 'no-store',
    headers: { 'Content-Type': 'application/json', Accept: 'application/json' },
    body: JSON.stringify(body),
  });
  const data = await res.json().catch(() => null);
  if (!res.ok) {
    const details: string[] = data?.errors ?? (data?.error ? [data.error] : []);
    throw new ApiError(details[0] ?? `${res.status} ${res.statusText}`, details);
  }
  return data as T;
}

export const futuresApi = {
  account: () => getJson<FuturesAccount>('/api/paper/futures/account'),
  history: (limit = 50) => getJson<FuturesPosition[]>(`/api/paper/futures/positions/history?limit=${limit}`),
  quote: (req: OpenRequest) => postJson<FuturesQuote>('/api/paper/futures/quote', req),
  open: (req: OpenRequest) =>
    postJson<{ accepted: boolean; duplicate: boolean; position: FuturesPosition }>('/api/paper/futures/orders', req),
  close: (id: number, commandId: string) =>
    postJson<FuturesPosition>(`/api/paper/futures/positions/${id}/close`, { commandId }),
};

// ---- Bảng giá trực tiếp (Spot + Mark, mỗi 15s) ----

export interface LiveSample {
  timeUtc: string;
  spotPrice: number | null;
  markPrice: number | null;
  spotLatencyMs: number | null;
  markLatencyMs: number | null;
  error: string | null;
}

export interface LivePrices {
  intervalSeconds: number;
  serverTimeUtc: string;
  symbols: { symbol: string; samples: LiveSample[] }[];
}

export const liveApi = {
  prices: (limit = 60) => getJson<LivePrices>(`/api/live/prices?limit=${limit}`),
};
