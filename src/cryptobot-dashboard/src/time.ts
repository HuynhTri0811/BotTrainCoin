// Backend lưu và trả về UTC; UI chỉ đổi sang giờ Việt Nam để hiển thị.
export const VN_TZ = 'Asia/Ho_Chi_Minh';

const dateTime = new Intl.DateTimeFormat('vi-VN', {
  timeZone: VN_TZ,
  year: 'numeric',
  month: '2-digit',
  day: '2-digit',
  hour: '2-digit',
  minute: '2-digit',
  hour12: false,
});

const timeOnly = new Intl.DateTimeFormat('vi-VN', {
  timeZone: VN_TZ,
  hour: '2-digit',
  minute: '2-digit',
  hour12: false,
});

const dayMonth = new Intl.DateTimeFormat('vi-VN', { timeZone: VN_TZ, day: '2-digit', month: '2-digit' });

export const formatVn = (iso: string | null | undefined) => (iso ? dateTime.format(new Date(iso)) : '—');
export const formatVnUnix = (seconds: number) => dateTime.format(new Date(seconds * 1000));
export const formatVnTick = (seconds: number, showDate: boolean) =>
  (showDate ? dayMonth : timeOnly).format(new Date(seconds * 1000));

export function ago(iso: string | null | undefined, nowIso: string): string {
  if (!iso) return 'chưa có';
  const sec = Math.max(0, Math.round((Date.parse(nowIso) - Date.parse(iso)) / 1000));
  if (sec < 60) return `${sec}s trước`;
  if (sec < 3600) return `${Math.round(sec / 60)} phút trước`;
  return `${Math.round(sec / 3600)} giờ trước`;
}

const withSeconds = new Intl.DateTimeFormat('vi-VN', {
  timeZone: VN_TZ,
  hour: '2-digit',
  minute: '2-digit',
  second: '2-digit',
  hour12: false,
});

export const formatVnSeconds = (iso: string) => withSeconds.format(new Date(iso));

// Việt Nam không có giờ mùa hè nên lệch UTC cố định +7 giờ.
const VN_OFFSET_MS = 7 * 3600 * 1000;

/**
 * Ô ngày giờ Excel không có múi giờ và thư viện ghi theo UTC: dời sẵn sang giờ Việt Nam để file hiện đúng giờ
 * như trên dashboard.
 */
export const excelVnDate = (iso: string | null | undefined) => (iso ? new Date(Date.parse(iso) + VN_OFFSET_MS) : null);
export const excelVnDateUnix = (seconds: number) => new Date(seconds * 1000 + VN_OFFSET_MS);

const fileDate = new Intl.DateTimeFormat('sv-SE', { timeZone: VN_TZ, year: 'numeric', month: '2-digit', day: '2-digit' });

/** Ngày hôm nay theo giờ Việt Nam dạng `2026-11-04`, dùng trong tên file xuất. */
export const todayVn = () => fileDate.format(new Date());
