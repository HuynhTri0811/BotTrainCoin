const usdt = new Intl.NumberFormat('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
const price = new Intl.NumberFormat('en-US', { maximumFractionDigits: 8 });

export const fmtUsdt = (value: number | null | undefined) => (value == null ? '—' : `${usdt.format(value)} USDT`);
export const fmtPrice = (value: number | null | undefined) => (value == null ? '—' : price.format(value));
export const fmtSigned = (value: number | null | undefined) =>
  value == null ? '—' : `${value > 0 ? '+' : ''}${usdt.format(value)}`;
export const pnlColor = (value: number | null | undefined) =>
  value == null || value === 0 ? undefined : value > 0 ? '#16a34a' : '#dc2626';

const price2 = new Intl.NumberFormat('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
/** Giá hiển thị 2 chữ số lẻ (mark price của sàn có tới 8 chữ số lẻ). */
export const fmtPrice2 = (value: number | null | undefined) => (value == null ? '—' : price2.format(value));
