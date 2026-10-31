import { useDeferredValue, useState } from 'react';
import { Alert, Button, Card, Input, Segmented, Select, Space, Spin, Typography, message } from 'antd';
import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { newsApi } from '../api/client';
import { NewsList } from '../features/news/NewsList';

const ALL = 'Tất cả';

/** Tin tức crypto lấy từ RSS công khai (NewsWorker, mặc định mỗi 5 giờ), lọc theo coin, nguồn và từ khóa. */
export function NewsPage() {
  const [coin, setCoin] = useState(ALL);
  const [source, setSource] = useState<string | undefined>();
  const [search, setSearch] = useState('');
  const keyword = useDeferredValue(search.trim());
  const queryClient = useQueryClient();
  const query = useQuery({
    queryKey: ['news', coin, source, keyword],
    queryFn: () => newsApi.list({ coin: coin === ALL ? undefined : coin, source, search: keyword, limit: 100 }),
    placeholderData: keepPreviousData,
    refetchInterval: 5 * 60_000,
  });
  const sync = useMutation({
    mutationFn: newsApi.sync,
    onSuccess: (result) => {
      if (result.errors.length > 0) message.warning(result.errors.join(' · '));
      else message.success(result.added > 0 ? `Thêm ${result.added} tin mới.` : 'Không có tin mới.');
      queryClient.invalidateQueries({ queryKey: ['news'] });
    },
    onError: (error) => message.error(error.message),
  });
  const data = query.data;

  return (
    <Card
      size="small"
      title={
        <Segmented
          size="small"
          value={coin}
          onChange={(value) => setCoin(value as string)}
          options={[ALL, ...(data?.coins ?? ['BTC', 'ETH', 'ETC'])]}
        />
      }
      extra={
        <Space wrap>
          {data && (
            <Typography.Text type="secondary" style={{ fontSize: 12 }}>
              {data.sources.join(', ')} · đồng bộ mỗi {data.pollIntervalHours} giờ
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
          onChange={(error) => setSearch(error.target.value)}
        />
        <Select<string>
          size="small"
          allowClear
          placeholder="Mọi nguồn"
          style={{ minWidth: 140 }}
          value={source}
          onChange={setSource}
          options={(data?.sources ?? []).map((symbol) => ({ value: symbol, label: symbol }))}
        />
        {data && (
          <Typography.Text type="secondary" style={{ fontSize: 12 }}>
            {data.articles.length} tin
          </Typography.Text>
        )}
      </Space>
      {query.isPending ? (
        <Spin />
      ) : query.isError ? (
        <Alert type="error" showIcon title={`Không tải được tin: ${query.error.message}`} />
      ) : (
        <NewsList articles={data?.articles ?? []} />
      )}
      <Typography.Paragraph type="secondary" style={{ margin: '12px 0 0', fontSize: 12 }}>
        Chỉ lưu tiêu đề, tóm tắt ngắn và link tới bài gốc. Coin được gắn theo từ khóa trong tiêu đề/tóm tắt (bitcoin,
        ether, ethereum classic…), có thể sót hoặc nhầm.
      </Typography.Paragraph>
    </Card>
  );
}
