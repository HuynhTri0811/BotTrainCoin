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
