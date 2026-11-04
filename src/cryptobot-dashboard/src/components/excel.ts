import { todayVn } from '../time';

export type ExcelValue = string | number | boolean | Date | null | undefined;

/** Một cột của file Excel: tiêu đề, cách lấy giá trị từ một dòng, định dạng số/ngày và độ rộng (ký tự). */
export interface ExcelColumn<T> {
  header: string;
  value: (row: T) => ExcelValue;
  format?: string;
  width?: number;
}

/** Định dạng ô dùng chung. Số vẫn là số trong Excel (cộng, lọc, vẽ biểu đồ được), chỉ đổi cách hiển thị. */
export const EXCEL_FORMAT = {
  dateTime: 'dd/mm/yyyy hh:mm',
  /** Tiền (USDT) hoặc khối lượng, 2 chữ số lẻ. */
  amount: '#,##0.00',
  signedAmount: '+#,##0.00;-#,##0.00;0.00',
  price: '#,##0.00######',
  /** Giá trị đã là phần trăm (2.5 nghĩa là 2,5%), nên không dùng định dạng `%` của Excel (nhân 100). */
  percent: '0.00"%"',
  signedPercent: '+0.00"%";-0.00"%";0.00"%"',
} as const;

/**
 * Ghi `rows` ra file `<fileName>-<ngày VN>.xlsx` và mở hộp thoại tải về. Dòng tiêu đề in đậm và đứng yên khi cuộn.
 * Ô trống (`null`/`undefined`) để trống thay vì `—` để Excel tính toán được. Trả về số dòng đã ghi; không có dòng
 * nào thì không tạo file.
 */
export async function downloadExcel<T>(fileName: string, sheet: string, rows: T[], columns: ExcelColumn<T>[]) {
  if (rows.length === 0) return 0;
  // Chỉ tải thư viện khi bấm xuất, không làm nặng bundle chính.
  const { default: writeExcelFile } = await import('write-excel-file/browser');
  await writeExcelFile(rows, {
    sheet,
    stickyRowsCount: 1,
    columns: columns.map((column) => ({
      width: column.width,
      header: { value: column.header, fontWeight: 'bold' },
      cell: (row: T) => {
        const value = column.value(row);
        return value == null ? null : { value, format: column.format };
      },
    })),
  }).toFile(`${fileName}-${todayVn()}.xlsx`);
  return rows.length;
}
