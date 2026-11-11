import { useEffect, useRef } from 'react';
import { BacktestDetail } from '../features/backtest/BacktestDetail';
import { BacktestHistoryCard } from '../features/backtest/BacktestHistoryCard';
import { backtestDetailHref, pageHref } from '../routes';

interface Props {
  /** Chiến lược đang chọn trên dashboard, cho bộ lọc "chỉ chiến lược này"; undefined thì ẩn bộ lọc. */
  strategyId: number | undefined;
  /** Lần chạy đang mở từ #hash; null = danh sách. */
  openedId: number | null;
}

/** Tab Lịch sử backtest: danh sách mọi lần chạy, bấm Xem để mở trang chi tiết. */
export function BacktestHistoryPage({ strategyId, openedId }: Props) {
  // Mở từ danh sách thì quay lại bằng lịch sử trình duyệt, để nút trên trang và nút Back của trình duyệt khớp nhau.
  const openedFromList = useRef(false);
  // Vị trí cuộn của danh sách lúc bấm Xem, để quay lại đúng chỗ.
  const listScroll = useRef(0);

  useEffect(() => {
    if (openedId != null) return;
    openedFromList.current = false;
    window.scrollTo({ top: listScroll.current });
  }, [openedId]);

  const back = () => {
    if (openedFromList.current) window.history.back();
    // Mở thẳng từ link: thay mục hiện tại, không chồng thêm mục lịch sử.
    else window.location.replace(pageHref('backtest-history'));
  };

  return (
    <>
      {/* Danh sách vẫn mount khi xem chi tiết: giữ bộ lọc, dòng đã chọn và trang của bảng. */}
      <div hidden={openedId != null}>
        <BacktestHistoryCard
          strategyId={strategyId}
          onOpen={(id) => {
            listScroll.current = window.scrollY;
            openedFromList.current = true;
            window.location.hash = backtestDetailHref(id);
          }}
        />
      </div>
      {openedId != null && <BacktestDetail key={openedId} id={openedId} onBack={back} />}
    </>
  );
}
