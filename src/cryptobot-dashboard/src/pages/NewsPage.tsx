import { useState } from 'react';
import { Alert, Button, Card, Segmented, Space, Spin, Typography, message } from 'antd';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { newsApi } from '../api/client';
import { NewsList } from '../features/news/NewsList';

const ALL = 'Tất cả';

/** Tin tức crypto lấy từ RSS công khai (NewsWorker, mặc định mỗi 5 giờ), lọc theo coin. */
export function NewsPage() {
  const [coin, setCoin] = useState(ALL);
  const queryClient = useQueryClient();
  const q = useQuery({
    queryKey: ['news', coin],
    queryFn: () => newsApi.list({ coin: coin === ALL ? undefined : coin, limit: 100 }),
    refetchInterval: 5 * 60_000,
  });
  const sync = useMutation({
    mutationFn: newsApi.sync,
    onSuccess: (r) => {
      if (r.errors.length > 0) message.warning(r.errors.join(' · '));
      else message.success(r.added > 0 ? `Thêm ${r.added} tin mới.` : 'Không có tin mới.');
      queryClient.invalidateQueries({ queryKey: ['news'] });
    },
    onError: (e) => message.error(e.message),
  });
  const d = q.data;

  return (
    <Card
      size="small"
      title={
        <Segmented
          size="small"
          value={coin}
          onChange={(v) => setCoin(v as string)}
          options={[ALL, ...(d?.coins ?? ['BTC', 'ETH', 'ETC'])]}
        />
      }
      extra={
        <Space wrap>
          {d && (
            <Typography.Text type="secondary" style={{ fontSize: 12 }}>
              {d.sources.join(', ')} · đồng bộ mỗi {d.pollIntervalHours} giờ
            </Typography.Text>
          )}
          <Button size="small" loading={sync.isPending} onClick={() => sync.mutate()}>
            Đồng bộ ngay
          </Button>
        </Space>
      }
    >
      {q.isPending ? (
        <Spin />
      ) : q.isError ? (
        <Alert type="error" showIcon title={`Không tải được tin: ${q.error.message}`} />
      ) : (
        <NewsList articles={d?.articles ?? []} />
      )}
      <Typography.Paragraph type="secondary" style={{ margin: '12px 0 0', fontSize: 12 }}>
        Chỉ lưu tiêu đề, tóm tắt ngắn và link tới bài gốc. Coin được gắn theo từ khóa trong tiêu đề/tóm tắt (bitcoin,
        ether, ethereum classic…), có thể sót hoặc nhầm.
      </Typography.Paragraph>
    </Card>
  );
}
