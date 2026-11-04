import { useState } from 'react';
import { Button, message } from 'antd';
import type { ExportResult } from './excel';

/**
 * Nút "Xuất Excel". `onExport` tải dữ liệu nếu cần, ghi file và trả về số dòng đã xuất; 0 dòng thì không tạo file
 * mà báo người dùng, dữ liệu bị cắt ở giới hạn API thì cảnh báo để người dùng biết file thiếu dòng cũ.
 */
export function ExportExcelButton({
  onExport,
  disabled,
  size = 'small',
}: {
  onExport: () => Promise<ExportResult>;
  disabled?: boolean;
  size?: 'small' | 'middle';
}) {
  const [busy, setBusy] = useState(false);

  const run = async () => {
    setBusy(true);
    try {
      const { count, truncatedAt } = await onExport();
      if (count === 0) message.info('Không có dòng nào để xuất.');
      else if (truncatedAt != null)
        message.warning(
          `Đã xuất ${count} dòng. API trả tối đa ${truncatedAt} dòng mới nhất mỗi lần nên file có thể thiếu dòng cũ hơn.`,
          8,
        );
    } catch (error) {
      message.error(`Không xuất được Excel: ${error instanceof Error ? error.message : String(error)}`);
    } finally {
      setBusy(false);
    }
  };

  return (
    <Button size={size} loading={busy} disabled={disabled} onClick={run}>
      Xuất Excel
    </Button>
  );
}
