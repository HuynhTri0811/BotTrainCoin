import { useState } from 'react';
import { Alert, Button, Card, Input, Segmented, Select, Space, Spin, Typography, message } from 'antd';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { newsApi } from '../api/client';
import { NewsList } from '../features/news/NewsList';

const ALL = 'Tất cả';

/** Tin tức crypto lấy từ RSS công khai (NewsWorker, mặc định mỗi 5 giờ), lọc theo coin, nguồn và từ khóa. */
export function NewsPage() {
  const [coin, setCoin] = useState(ALL);
  const [source, setSource] = useState<string | undefined>();
  const [search, setSearch] = useState('');
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

  // Nguồn và từ khóa lọc trên 100 tin đã tải về, coin thì lọc từ backend.
  const keyword = search.trim().toLowerCase();
  const articles = (d?.articles ?? []).filter(
    (a) =>
      (source === undefined || a.source === source) &&
      (keyword === '' || a.title.toLowerCase().includes(keyword) || a.summary.toLowerCase().includes(keyword)),
  );

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
      <Space wrap style={{ marginBottom: 12 }}>
        <Input.Search
          size="small"
          allowClear
          placeholder="Tìm trong tiêu đề, tóm tắt"
          style={{ width: 260 }}
          value={search}
          onChange={(e) => setSearch(e.target.value)}
        />
        <Select<string>
          size="small"
          allowClear
          placeholder="Mọi nguồn"
          style={{ minWidth: 140 }}
          value={source}
          onChange={setSource}
          options={(d?.sources ?? []).map((s) => ({ value: s, label: s }))}
        />
        {d && (
          <Typography.Text type="secondary" style={{ fontSize: 12 }}>
            {articles.length}/{d.articles.length} tin
          </Typography.Text>
        )}
      </Space>
      {q.isPending ? (
        <Spin />
      ) : q.isError ? (
        <Alert type="error" showIcon title={`Không tải được tin: ${q.error.message}`} />
      ) : (
        <NewsList articles={articles} />
      )}
      <Typography.Paragraph type="secondary" style={{ margin: '12px 0 0', fontSize: 12 }}>
        Chỉ lưu tiêu đề, tóm tắt ngắn và link tới bài gốc. Coin được gắn theo từ khóa trong tiêu đề/tóm tắt (bitcoin,
        ether, ethereum classic…), có thể sót hoặc nhầm.
      </Typography.Paragraph>
    </Card>
  );
}
