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
  /** Khối lượng quy ra USDT; null với nến lưu trước khi có dữ liệu này. */
  quoteVolume: number | null;
  /** Số giao dịch khớp trong nến; null với nến lưu trước khi có dữ liệu này. */
  tradeCount: number | null;
  isClosed: boolean;
  fastEma: number | null;
  slowEma: number | null;
  /** Mua/bán chủ động; null với nến lưu trước khi có dữ liệu này. */
  pressure: VolumePressure | null;
}

export type VolumePressureSide = 'Balanced' | 'BuyHeavy' | 'SellHeavy';

export interface VolumePressure {
  buyVolume: number;
  sellVolume: number;
  /** Tỷ trọng mua trong tổng khối lượng, 0–100. */
  buyPercent: number;
  /** Phía lớn hơn vượt phía nhỏ hơn bao nhiêu %; null khi phía nhỏ hơn bằng 0. */
  excessPercent: number | null;
  side: VolumePressureSide;
  alertExcessPercent: number;
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
  closeReason: 'Manual' | 'StopLoss' | 'TakeProfit' | 'Liquidation' | 'Signal' | null;
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

// ---- Quy tắc tự mở lệnh Paper theo mức giá hoặc theo chỉ báo ----

export type TriggerCondition = 'AtOrBelow' | 'AtOrAbove';
export type RuleTriggerType = 'Price' | 'Indicator';

export interface AutoRule {
  id: number;
  environment: 'Paper';
  symbol: string;
  side: PositionSide;
  triggerType: RuleTriggerType;
  condition: TriggerCondition | null;
  triggerPrice: number | null;
  indicatorName: string | null;
  margin: number;
  leverage: number;
  stopLossPercent: number;
  takeProfitPercent: number | null;
  status: 'Active' | 'Triggered' | 'Rejected' | 'Cancelled';
  createdAtUtc: string;
  triggeredAtUtc: string | null;
  triggeredMarkPrice: number | null;
  positionId: number | null;
  note: string | null;
  triggerDetail: string | null;
  /** Quy tắc theo chỉ báo: tự đóng vị thế khi chỉ báo đảo chiều. */
  closeOnReverse: boolean;
  closeWatchEndedAtUtc: string | null;
  closeDetail: string | null;
}

export interface AutoSummary {
  rules: AutoRule[];
  defaults: {
    margin: number;
    leverage: number;
    stopLossPercent: number;
    takeProfitPercent: number;
    dailyLossLimitPercent: number;
    maxActiveRules: number;
  };
  risk: { dayStartUtc: string; dayStartEquity: number; todayRealizedPnl: number; lossLimit: number; blocked: boolean };
}

/** Số tiền/giá gửi dạng chuỗi để không mất chính xác qua JSON number. */
export interface CreateAutoRule {
  symbol: string;
  side: PositionSide;
  triggerType: RuleTriggerType;
  /** Chỉ với triggerType = 'Price'. */
  condition: TriggerCondition | null;
  triggerPrice: string | null;
  /** Chỉ với triggerType = 'Indicator'. */
  indicatorName: string | null;
  /** Chỉ với triggerType = 'Indicator': tự đóng khi chỉ báo báo ngược hướng. */
  closeOnReverse: boolean;
  margin: string;
  leverage: number;
  stopLossPercent: string;
  takeProfitPercent: string | null;
}

export const autoApi = {
  summary: () => getJson<AutoSummary>('/api/paper/futures/auto-rules'),
  create: (req: CreateAutoRule) => postJson<AutoRule>('/api/paper/futures/auto-rules', req),
  cancel: (id: number) => postJson<{ cancelled: boolean }>(`/api/paper/futures/auto-rules/${id}/cancel`, {}),
};

// ---- Chỉ báo tự viết (CryptoBot.Domain/Indicators, cấu hình "Indicators") ----

export type IndicatorSignal = 'None' | 'Long' | 'Short';

export interface IndicatorLine {
  key: string;
  title: string;
  color: string;
  /** true: vẽ chung với nến; false: khung riêng bên dưới. */
  overlay: boolean;
  levels: number[];
}

export interface Indicator {
  name: string;
  type: string;
  description: string;
  symbol: string;
  interval: string;
  requiredCandles: number;
  lines: IndicatorLine[];
}

export interface IndicatorList {
  indicators: Indicator[];
  errors: { name: string; error: string }[];
  availableTypes: string[];
}

export interface IndicatorPoint {
  time: number; // Unix seconds, open time của nến đã đóng
  values: Record<string, number | null>;
  signal: IndicatorSignal;
}

export interface IndicatorMarker {
  time: number;
  signal: Exclude<IndicatorSignal, 'None'>;
  reason: string;
}

export interface IndicatorChart {
  indicator: Indicator;
  points: IndicatorPoint[];
  markers: IndicatorMarker[];
  latestReason: string | null;
}

export const indicatorApi = {
  list: () => getJson<IndicatorList>('/api/indicators'),
  chart: (name: string, limit = 300) =>
    getJson<IndicatorChart>(`/api/indicators/${encodeURIComponent(name)}/chart?limit=${limit}`),
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

/** Biên độ dự kiến sau horizonDays ngày từ biến động nến ngày (không dự đoán hướng). */
export interface DailyRange {
  symbol: string;
  horizonDays: number;
  /** Số thay đổi theo ngày dùng để tính σ. */
  days: number;
  price: number;
  /** Độ lệch chuẩn % thay đổi sau horizonDays ngày (σ). */
  typicalMovePercent: number;
  low68: number;
  high68: number;
  low95: number;
  high95: number;
  averageMovePercent: number;
  largestRisePercent: number;
  largestDropPercent: number;
  /** Các số liệu lịch sử (TB, mạnh nhất, % lần tăng) tính trên các đoạn dài horizonDays ngày. */
  upDaysPercent: number;
  samples: number;
  /** Chỉ có với 24 giờ và symbol có lịch sử dài hạn (DailyHistory). */
  similarDays: SimilarDays | null;
}

/** Dự báo "ngày tương tự" (CryptoBot.Domain/MarketData/SimilarDaysForecast.cs). */
export interface SimilarDays {
  historySymbol: string;
  exchange: string;
  /** Ngày (nến đã đóng) cuối cùng dùng để dự báo: hôm qua. */
  asOfDayUtc: string;
  /** Giá đóng cửa hôm qua trên Binance; khoảng giá tính từ đây, không phải từ giá hiện tại. */
  anchorPrice: number;
  /** Thời điểm đóng cửa được dự báo (cuối ngày hôm nay, UTC). */
  targetCloseUtc: string;
  /** Số ngày tương tự được dùng. */
  neighbors: number;
  /** % ngày tương tự có giá sau đó cao hơn. */
  upPercent: number;
  medianPercent: number;
  low68Percent: number;
  high68Percent: number;
  /** Giá: anchorPrice × (1 + %). */
  low68: number;
  high68: number;
  lean: 'Up' | 'Down' | 'Neutral';
  /** Chạy thử walk-forward trên lịch sử. */
  testedDays: number;
  hitRatePercent: number;
  /** Số ngày nghiêng rõ (≥ 60% hoặc ≤ 40%) và tỷ lệ đúng của chúng. */
  strongDays: number;
  strongHitRatePercent: number;
}

export const liveApi = {
  prices: (limit = 60) => getJson<LivePrices>(`/api/live/prices?limit=${limit}`),
  dailyRange: (symbol: string, days = 1) =>
    getJson<DailyRange>(`/api/live/daily-range?symbol=${encodeURIComponent(symbol)}&days=${days}`),
};

// ---- Dự đoán nến kế tiếp lên/xuống (CryptoBot.Domain/Prediction/PriceDirectionPredictor.cs) ----

export type PriceDirection = 'Up' | 'Down';

export interface PredictionHistory {
  time: number; // Unix seconds, open time của nến được đoán
  predicted: PriceDirection;
  score: number;
  /** null khi nến đóng đúng bằng giá nến trước. */
  actual: PriceDirection | null;
  previousClose: number;
  close: number;
  /** Giá đóng − giá đóng nến trước. */
  change: number;
  changePercent: number;
}

export interface Prediction {
  symbol: string;
  interval: string;
  /** Open time (Unix seconds) của nến được dự đoán. */
  nextCandleTime: number;
  lastClose: number;
  direction: PriceDirection;
  score: number;
  reason: string;
  backtestPredictions: number;
  backtestCorrect: number;
  hitRatePercent: number;
  recent: PredictionHistory[];
  settings: {
    trendPeriod: number;
    momentumPeriod: number;
    trendWeight: number;
    momentumWeight: number;
    pressureWeight: number;
  };
}

export interface HorizonAccuracy {
  /** Số nến sau tín hiệu. */
  candles: number;
  signals: number;
  correct: number;
  hitRatePercent: number;
  /** Trung bình % giá đi theo hướng tín hiệu (âm = đi ngược). */
  averageMovePercent: number;
}

export interface IndicatorSignalOutcome {
  time: number;
  direction: PriceDirection;
  close: number;
  /** % giá đi theo hướng tín hiệu sau 1 / 3 nến; null khi chưa đủ nến. */
  move1: number | null;
  move3: number | null;
  reason: string;
}

export interface IndicatorAccuracy {
  name: string;
  type: string;
  candles: number;
  fromTime: number | null;
  horizons: HorizonAccuracy[];
  recent: IndicatorSignalOutcome[];
}

export const predictionApi = {
  indicators: (symbol: string, interval: string) =>
    getJson<IndicatorAccuracy[]>(
      `/api/prediction/indicators?symbol=${encodeURIComponent(symbol)}&interval=${encodeURIComponent(interval)}`,
    ),
  get: (symbol: string, interval: string, history = 300) =>
    getJson<Prediction>(
      `/api/prediction?symbol=${encodeURIComponent(symbol)}&interval=${encodeURIComponent(interval)}&history=${history}`,
    ),
};

// ---- Chênh lệch mở → đóng cửa theo tháng (CryptoBot.Domain/MarketData/MonthlyChangeReport.cs) ----

export interface MonthChange {
  year: number;
  /** 1–12. */
  month: number;
  /** Giá mở cửa ngày đầu tiên có nến của tháng. */
  open: number;
  /** Giá đóng cửa ngày cuối cùng có nến (tháng đang chạy: giá gần nhất). */
  close: number;
  changePercent: number;
  days: number;
  complete: boolean;
}

export interface YearChange {
  year: number;
  open: number;
  close: number;
  changePercent: number;
  complete: boolean;
}

/** Cùng một tháng qua các năm, chỉ tính tháng trọn vẹn. */
export interface MonthStats {
  month: number;
  years: number;
  averageChangePercent: number;
  bestChangePercent: number;
  worstChangePercent: number;
  upCount: number;
  upPercent: number;
}

/** Sự kiện thị trường cùng tháng (marketevents.json), không khẳng định là nguyên nhân. */
export interface MonthEvent {
  year: number;
  month: number;
  title: string;
  detail: string | null;
  /** true: sự kiện chung của thị trường; false: riêng của symbol này. */
  marketWide: boolean;
}

export interface MonthlyHistory {
  symbol: string;
  exchange: string;
  /** Các symbol có dữ liệu lịch sử (DailyHistory:Symbols). */
  symbols: string[];
  firstDayUtc: string;
  lastDayUtc: string;
  months: MonthChange[];
  years: YearChange[];
  monthStats: MonthStats[];
  events: MonthEvent[];
}

export const historyApi = {
  monthly: (symbol: string) => getJson<MonthlyHistory>(`/api/history/monthly?symbol=${encodeURIComponent(symbol)}`),
};

// Báo cáo biến động hằng ngày (xem CryptoBot.Api/Endpoints/ReportEndpoints.cs).

export interface ReportCandle {
  openTimeUtc: string;
  open: number;
  close: number;
  changePercent: number;
  volume: number;
  /** Khối lượng gấp bao nhiêu lần trung bình các nến trước; null khi chưa đủ nến để so. */
  volumeRatio: number | null;
  buyPercent: number | null;
}

export interface DailyMoveReport {
  fromUtc: string;
  toUtc: string;
  candleCount: number;
  open: number;
  close: number;
  changePercent: number;
  high: number;
  highTimeUtc: string;
  low: number;
  lowTimeUtc: string;
  rangePercent: number;
  maxDrawdownPercent: number;
  maxRallyPercent: number;
  volume: number;
  buyPercent: number | null;
  biggestRise: ReportCandle;
  biggestDrop: ReportCandle;
  volumeSpikes: ReportCandle[];
}

export interface SavedDailyReport {
  symbol: string;
  interval: string;
  createdAtUtc: string;
  report: DailyMoveReport;
}

export interface DailyReportList {
  symbols: string[];
  /** Giờ lập báo cáo, dạng "06:00", theo `timeZone`. */
  runAt: string;
  timeZone: string;
  nextRunUtc: string;
  reports: SavedDailyReport[];
}

export interface DailyReportRun {
  periodEndUtc: string;
  created: number;
  errors: string[];
}

export const reportApi = {
  daily: (symbol: string, limit = 14) =>
    getJson<DailyReportList>(`/api/reports/daily?symbol=${encodeURIComponent(symbol)}&limit=${limit}`),
  run: () => postJson<DailyReportRun>('/api/reports/daily/run', {}),
};

// Tin tức từ RSS công khai (xem CryptoBot.Api/Endpoints/NewsEndpoints.cs).

export interface NewsArticle {
  id: number;
  source: string;
  title: string;
  url: string;
  summary: string;
  coins: string[];
  publishedAtUtc: string;
}

export interface NewsList {
  coins: string[];
  sources: string[];
  pollIntervalHours: number;
  articles: NewsArticle[];
}

export const newsApi = {
  list: (
    opts: { coin?: string; source?: string; search?: string; fromUtc?: string; toUtc?: string; limit?: number } = {},
  ) => {
    const q = new URLSearchParams();
    if (opts.coin) q.set('coin', opts.coin);
    if (opts.source) q.set('source', opts.source);
    if (opts.search) q.set('search', opts.search);
    if (opts.fromUtc) q.set('fromUtc', opts.fromUtc);
    if (opts.toUtc) q.set('toUtc', opts.toUtc);
    q.set('limit', String(opts.limit ?? 50));
    return getJson<NewsList>(`/api/news?${q}`);
  },
  sync: () => postJson<{ added: number; errors: string[] }>('/api/news/sync', {}),
};
