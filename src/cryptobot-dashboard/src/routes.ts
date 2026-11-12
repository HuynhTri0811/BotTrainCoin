/** Các trang của dashboard và cách đọc/tạo #hash: mọi link giữa các trang đều đi qua đây. */
export const PAGES = [
  { key: 'market', label: 'Biểu đồ' },
  { key: 'trading', label: 'Giao dịch Paper' },
  { key: 'prediction', label: 'Dự đoán' },
  { key: 'signals', label: 'Tín hiệu' },
  { key: 'backtest', label: 'Backtest' },
  { key: 'backtest-history', label: 'Lịch sử backtest' },
  { key: 'history', label: 'Lịch sử theo tháng' },
  { key: 'reports', label: 'Báo cáo ngày' },
  { key: 'news', label: 'Tin tức' },
  { key: 'settings', label: 'Cài đặt' },
] as const;

export type Page = (typeof PAGES)[number]['key'];

export interface Route {
  page: Page;
  /** Lần backtest đang mở ở `#backtest-history/{id}`; null với mọi trang khác. */
  backtestId: number | null;
}

/** Trang hiện tại nằm trong #hash để tải lại hoặc gửi link vẫn mở đúng trang. */
export function routeFromHash(): Route {
  const [key, param] = window.location.hash.slice(1).split('/');
  const page = PAGES.find((entry) => entry.key === key)?.key ?? 'market';
  // Chỉ trang lịch sử backtest có phần sau `/`; ở trang khác thì bỏ qua để không đổi giao diện.
  const id = Number(param);
  const backtestId = page === 'backtest-history' && param && Number.isInteger(id) && id > 0 ? id : null;
  return { page, backtestId };
}

/** `#trang`, dùng cho href. */
export const pageHref = (page: Page) => `#${page}`;

/** `#backtest-history/14`: trang chi tiết một lần backtest. */
export const backtestDetailHref = (id: number) => `#${'backtest-history' satisfies Page}/${id}`;
