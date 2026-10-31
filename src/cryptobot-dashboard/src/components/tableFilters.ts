export interface FilterOption {
  text: string;
  value: string;
}

/**
 * Bộ lọc trên tiêu đề cột antd: so khớp `value(row)` với lựa chọn. Lọc trên các dòng đã tải về, không gọi lại API.
 * Trải vào định nghĩa cột: `{ title: 'Hướng', ...columnFilter((p) => p.side, SIDES) }`.
 */
export function columnFilter<T>(value: (row: T) => string, options: FilterOption[]) {
  return {
    filters: options,
    onFilter: (selected: unknown, row: T) => value(row) === selected,
  };
}

/** Lựa chọn lấy từ chính dữ liệu, dùng cho cột không có danh sách cố định như symbol. */
export function optionsFrom<T>(rows: T[], value: (row: T) => string): FilterOption[] {
  return [...new Set(rows.map(value))].sort().map((v) => ({ text: v, value: v }));
}
