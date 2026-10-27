import { Card, Radio, Space, Typography } from 'antd';
import { useTheme, type ThemeMode } from '../theme';

const MODES: { value: ThemeMode; label: string; description: string }[] = [
  { value: 'light', label: 'Sáng', description: 'Nền trắng.' },
  { value: 'dark', label: 'Tối', description: 'Nền tối, dễ nhìn khi theo dõi giá ban đêm.' },
  { value: 'system', label: 'Theo hệ thống', description: 'Đổi theo chế độ sáng/tối của máy.' },
];

/** Cài đặt của dashboard; lưu trong trình duyệt này. */
export function SettingsPage() {
  const { mode, setMode, isDark } = useTheme();

  return (
    <Card size="small" title="Giao diện" style={{ maxWidth: 560 }}>
      <Radio.Group value={mode} onChange={(e) => setMode(e.target.value as ThemeMode)}>
        <Space orientation="vertical" size="middle">
          {MODES.map((m) => (
            <Radio key={m.value} value={m.value}>
              <Typography.Text strong>{m.label}</Typography.Text>
              <div>
                <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                  {m.description}
                </Typography.Text>
              </div>
            </Radio>
          ))}
        </Space>
      </Radio.Group>
      <Typography.Paragraph type="secondary" style={{ margin: '16px 0 0', fontSize: 12 }}>
        Đang dùng: {isDark ? 'Tối' : 'Sáng'}. Lựa chọn được lưu trong trình duyệt này.
      </Typography.Paragraph>
    </Card>
  );
}
