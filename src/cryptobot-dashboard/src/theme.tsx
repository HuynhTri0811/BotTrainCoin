import { createContext, useContext, useEffect, useState, type ReactNode } from 'react';
import { ConfigProvider, theme } from 'antd';
import viVN from 'antd/locale/vi_VN';
import './theme.css';

export type ThemeMode = 'light' | 'dark' | 'system';

// Cùng khóa với đoạn script trong index.html.
const STORAGE_KEY = 'cryptobot-theme';

function readMode(): ThemeMode {
  try {
    const v = localStorage.getItem(STORAGE_KEY);
    return v === 'light' || v === 'dark' ? v : 'system';
  } catch {
    return 'system';
  }
}

const systemQuery = () => window.matchMedia('(prefers-color-scheme: dark)');

interface ThemeState {
  mode: ThemeMode;
  setMode: (mode: ThemeMode) => void;
  isDark: boolean;
}

const ThemeContext = createContext<ThemeState>({ mode: 'system', setMode: () => {}, isDark: false });

/** Sáng / Tối / Theo hệ thống. Lựa chọn lưu trong trình duyệt; "Theo hệ thống" đổi ngay khi máy đổi. */
export function ThemeProvider({ children }: { children: ReactNode }) {
  const [mode, setModeState] = useState<ThemeMode>(readMode);
  const [systemDark, setSystemDark] = useState(() => systemQuery().matches);

  useEffect(() => {
    const query = systemQuery();
    const onChange = (e: MediaQueryListEvent) => setSystemDark(e.matches);
    query.addEventListener('change', onChange);
    return () => query.removeEventListener('change', onChange);
  }, []);

  const isDark = mode === 'dark' || (mode === 'system' && systemDark);
  useEffect(() => {
    document.documentElement.dataset.theme = isDark ? 'dark' : 'light';
  }, [isDark]);

  const setMode = (next: ThemeMode) => {
    setModeState(next);
    try {
      if (next === 'system') localStorage.removeItem(STORAGE_KEY);
      else localStorage.setItem(STORAGE_KEY, next);
    } catch {
      // Trình duyệt chặn lưu: vẫn đổi được trong phiên này.
    }
  };

  return (
    <ThemeContext.Provider value={{ mode, setMode, isDark }}>
      <ConfigProvider locale={viVN} theme={{ algorithm: isDark ? theme.darkAlgorithm : theme.defaultAlgorithm }}>
        {children}
      </ConfigProvider>
    </ThemeContext.Provider>
  );
}

export const useTheme = () => useContext(ThemeContext);

/** Biểu đồ nến vẽ trên canvas nên không đọc được CSS variable: dùng bảng màu riêng cho từng chế độ. */
const CHART_LIGHT = {
  background: '#ffffff',
  text: '#334155',
  grid: '#f1f5f9',
  border: '#e2e8f0',
  up: '#16a34a',
  down: '#dc2626',
  upVolume: 'rgba(22,163,74,0.35)',
  downVolume: 'rgba(220,38,38,0.35)',
  running: '#94a3b8',
  runningVolume: '#cbd5e1',
  level: '#94a3b8',
  entry: '#475569',
  liquidation: '#9333ea',
};

const CHART_DARK: typeof CHART_LIGHT = {
  background: '#141414',
  text: '#cbd5e1',
  grid: '#1f242d',
  border: '#303030',
  up: '#22c55e',
  down: '#f87171',
  upVolume: 'rgba(34,197,94,0.35)',
  downVolume: 'rgba(248,113,113,0.35)',
  running: '#64748b',
  runningVolume: '#475569',
  level: '#64748b',
  entry: '#cbd5e1',
  liquidation: '#c084fc',
};

export type ChartColors = typeof CHART_LIGHT;

export const useChartColors = (): ChartColors => (useTheme().isDark ? CHART_DARK : CHART_LIGHT);
