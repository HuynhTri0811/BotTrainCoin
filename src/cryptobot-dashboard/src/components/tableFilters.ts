import type { Key } from 'react';
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
 * Key antd dùng cho bộ lọc của cột (giống `getColumnKey` của antd): `key`, rồi `dataIndex` (mảng nối bằng `.`), rồi vị
 * trí cột (`0`, `1-0` cho cột con).
 */
function filterKey<T>(column: ColumnsType<T>[number], position: string): string {
  if (column.key != null) return String(column.key);
  // antd chỉ dùng dataIndex khi nó truthy: `dataIndex: 0` hay `''` rơi về key theo vị trí.
  if ('dataIndex' in column && column.dataIndex) {
    return Array.isArray(column.dataIndex) ? column.dataIndex.join('.') : String(column.dataIndex);
  }
  return position;
}

type Column<T> = ColumnsType<T>[number];

/**
 * Dựng lại cây cột, gọi `visit` cho từng cột (cả cột con, sau khi đã đổi cột con) với key bộ lọc antd dùng cho cột đó.
 * Mọi chỗ cần key bộ lọc đi qua đây để bảng và file xuất luôn tính key giống nhau.
 */
function mapColumns<T>(
  columns: ColumnsType<T>,
  visit: (column: Column<T>, key: string) => Column<T>,
  parent?: string,
): ColumnsType<T> {
  return columns.map((column, index) => {
    const position = parent == null ? String(index) : `${parent}-${index}`;
    const withChildren =
      'children' in column ? { ...column, children: mapColumns(column.children, visit, position) } : column;
    return visit(withChildren, filterKey(column, position));
  });
}

type ActiveFilter<T> = { onFilter: (value: boolean | Key, row: T) => boolean; selected: FilterValue };

function activeFilters<T>(columns: ColumnsType<T>, filters: TableFilters): ActiveFilter<T>[] {
  const active: ActiveFilter<T>[] = [];
  mapColumns(columns, (column, key) => {
    const selected = filters[key];
    if (column.onFilter && selected?.length) active.push({ onFilter: column.onFilter, selected });
    return column;
  });
  return active;
}

/**
 * Các dòng còn lại sau bộ lọc `columnFilter` đang chọn, đúng như Table hiển thị (lấy `filters` từ `onChange` của
 * Table). Cột lọc phía API (`serverFilter`) không có `onFilter` nên bỏ qua. Trong một cột: khớp một lựa chọn là đủ;
 * giữa các cột: phải khớp tất cả (như antd).
 */
export function applyColumnFilters<T>(rows: T[], columns: ColumnsType<T>, filters: TableFilters): T[] {
  const active = activeFilters(columns, filters);
  return rows.filter((row) => active.every(({ onFilter, selected }) => selected.some((value) => onFilter(value, row))));
}

/**
 * Gắn `filteredValue` lấy từ `filters` cho mọi cột có bộ lọc (antd đòi tất cả cùng controlled). Table khi đó hiển thị
 * đúng bộ lọc đang lưu, kể cả sau khi mount lại (đổi symbol, đang tải, lỗi), nên bảng và file xuất dùng chung một
 * state: lưu `filters` từ `onChange` của Table rồi truyền vào đây và vào `applyColumnFilters`.
 */
export function withFilteredValues<T>(columns: ColumnsType<T>, filters: TableFilters): ColumnsType<T> {
  return mapColumns(columns, (column, key) => {
    const filterable = column.filters != null || column.filterDropdown != null || column.onFilter != null;
    return filterable ? { ...column, filteredValue: filters[key] ?? null } : column;
  });
}
