import type { ColumnsType, FilterValue } from 'antd/es/table/interface';

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

/** Bộ lọc do API xử lý: chỉ chọn một giá trị, đọc lại bằng `picked` trong `onChange` của Table rồi gọi API. */
export function serverFilter(options: FilterOption[]) {
  return { filters: options, filterMultiple: false };
}

export const picked = (selected: FilterValue | null | undefined) => selected?.[0] as string | undefined;

/** Lựa chọn lấy từ chính dữ liệu, dùng cho cột không có danh sách cố định như symbol. */
export function optionsFrom<T>(rows: T[], value: (row: T) => string): FilterOption[] {
  return [...new Set(rows.map(value))].sort().map((value) => ({ text: value, value: value }));
}

export type TableFilters = Record<string, FilterValue | null>;

/**
 * Các dòng còn lại sau bộ lọc `columnFilter` đang chọn, đúng như Table hiển thị (lấy `filters` từ `onChange` của
 * Table). Cột lọc phía API (`serverFilter`) không có `onFilter` nên bỏ qua. Cột có bộ lọc cần `key` hoặc `dataIndex`.
 */
export function applyColumnFilters<T>(rows: T[], columns: ColumnsType<T>, filters: TableFilters): T[] {
  const active = columns.flatMap((column) => {
    const key = column.key ?? ('dataIndex' in column ? column.dataIndex : undefined);
    const selected = key == null ? null : filters[String(key)];
    return column.onFilter && selected?.length ? [{ onFilter: column.onFilter, selected }] : [];
  });
  return rows.filter((row) => active.every(({ onFilter, selected }) => selected.some((value) => onFilter(value, row))));
}
