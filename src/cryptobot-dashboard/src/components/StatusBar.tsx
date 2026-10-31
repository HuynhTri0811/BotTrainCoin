import { Alert, Space, Tag, Tooltip, Typography } from 'antd';
import type { Status } from '../api/client';
import { ago, formatVn } from '../time';

const envColor = { Paper: 'blue', Testnet: 'gold', Live: 'red' } as const;

export function StatusBar({ status }: { status: Status }) {
  const staleFeeds = status.feeds.filter((feed) => feed.isStale);

  return (
    <Space orientation="vertical" style={{ width: '100%' }} size="small">
      <Space wrap size="small">
        <Tag color={envColor[status.environment]} style={{ fontSize: 14, padding: '2px 10px', fontWeight: 600 }}>
          {status.environment.toUpperCase()}
        </Tag>
        <Tag color={status.decisionMode === 'SignalOnly' ? 'default' : 'purple'}>{status.decisionMode}</Tag>
        {status.liveLocked && <Tag>Live bị khóa</Tag>}
        {status.feeds.map((feed) => (
          <Tooltip
            key={feed.key}
            title={
              <>
                Sync thành công: {formatVn(feed.lastSuccessAtUtc)}
                <br />
                Nến đóng mới nhất: {formatVn(feed.latestClosedOpenTimeUtc)}
                {feed.lastError && (
                  <>
                    <br />
                    Lỗi: {feed.lastError}
                  </>
                )}
              </>
            }
          >
            <Tag color={feed.isStale ? 'error' : 'success'}>
              {feed.key.replace('Binance:', '')} · {feed.isStale ? 'STALE' : ago(feed.lastSuccessAtUtc, status.serverTimeUtc)}
            </Tag>
          </Tooltip>
        ))}
        <Typography.Text type="secondary">Giờ hiển thị: Việt Nam (UTC+7)</Typography.Text>
      </Space>
      {staleFeeds.length > 0 && (
        <Alert
          type="warning"
          showIcon
          title={`Dữ liệu stale: ${staleFeeds.map((feed) => feed.key).join(', ')}`}
          description={staleFeeds.map((feed) => feed.lastError).filter(Boolean).join(' · ') || undefined}
        />
      )}
    </Space>
  );
}
