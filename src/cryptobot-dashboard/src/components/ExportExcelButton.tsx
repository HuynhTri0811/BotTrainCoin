import { useState } from 'react';
import { Button, message } from 'antd';

/**
 * Nút "Xuất Excel". `onExport` tải dữ liệu nếu cần, ghi file và trả về số dòng đã xuất; 0 dòng thì không tạo file
 * mà báo người dùng.
 */
export function ExportExcelButton({
  onExport,
  disabled,
  size = 'small',
}: {
  onExport: () => Promise<number>;
  disabled?: boolean;
  size?: 'small' | 'middle';
}) {
  const [busy, setBusy] = useState(false);

  const run = async () => {
    setBusy(true);
    try {
      const count = await onExport();
      if (count === 0) message.info('Không có dòng nào để xuất.');
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
