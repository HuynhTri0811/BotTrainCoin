import { BacktestDetail } from '../features/backtest/BacktestDetail';
import { BacktestHistoryCard } from '../features/backtest/BacktestHistoryCard';

export const BACKTEST_HISTORY_PAGE = 'backtest-history';

/** `#backtest-history/14`: link tới trang chi tiết, tải lại hoặc gửi link vẫn mở đúng lần chạy. */
export const backtestDetailHash = (id: number) => `${BACKTEST_HISTORY_PAGE}/${id}`;

interface Props {
  /** Chiến lược đang chọn trên dashboard, cho bộ lọc "chỉ chiến lược này". */
  strategyId: number;
  /** Lần chạy đang mở từ #hash; null = danh sách. */
  openedId: number | null;
}

/** Tab Lịch sử backtest: danh sách mọi lần chạy, bấm Xem để mở trang chi tiết. */
export function BacktestHistoryPage({ strategyId, openedId }: Props) {
  if (openedId != null)
    return (
      <BacktestDetail
        id={openedId}
        onBack={() => {
          window.location.hash = BACKTEST_HISTORY_PAGE;
        }}
      />
    );

  return (
    <BacktestHistoryCard
      strategyId={strategyId}
      onOpen={(id) => {
        window.location.hash = backtestDetailHash(id);
      }}
    />
  );
}
