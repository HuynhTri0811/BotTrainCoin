const usdt = new Intl.NumberFormat('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
const price = new Intl.NumberFormat('en-US', { maximumFractionDigits: 8 });

export const fmtUsdt = (v: number | null | undefined) => (v == null ? '—' : `${usdt.format(v)} USDT`);
export const fmtPrice = (v: number | null | undefined) => (v == null ? '—' : price.format(v));
export const fmtSigned = (v: number | null | undefined) =>
  v == null ? '—' : `${v > 0 ? '+' : ''}${usdt.format(v)}`;
export const pnlColor = (v: number | null | undefined) =>
  v == null || v === 0 ? undefined : v > 0 ? 'var(--up)' : 'var(--down)';

const price2 = new Intl.NumberFormat('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
/** Giá hiển thị 2 chữ số lẻ (mark price của sàn có tới 8 chữ số lẻ). */
export const fmtPrice2 = (v: number | null | undefined) => (v == null ? '—' : price2.format(v));
