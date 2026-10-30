import { Empty, Space, Tag, Typography } from 'antd';
import type { NewsArticle } from '../../api/client';
import { formatVn } from '../../time';

/** Danh sách tin: giờ đăng, nguồn, coin được nhắc, tiêu đề (link tới bài gốc) và tóm tắt. */
export function NewsList({ articles, showSummary = true }: { articles: NewsArticle[]; showSummary?: boolean }) {
  if (articles.length === 0) return <Empty image={Empty.PRESENTED_IMAGE_SIMPLE} description="Không có tin." />;

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 12 }}>
      {articles.map((a) => (
        <div key={a.id} style={{ borderBottom: '1px solid #f1f5f9', paddingBottom: 8 }}>
          <Space size={4} wrap style={{ fontSize: 12 }}>
            <Typography.Text type="secondary">{formatVn(a.publishedAtUtc)}</Typography.Text>
            <Tag>{a.source}</Tag>
            {a.coins.map((c) => (
              <Tag key={c} color={c === 'BTC' ? 'orange' : c === 'ETH' ? 'blue' : 'green'}>
                {c}
              </Tag>
            ))}
          </Space>
          <div>
            <Typography.Link href={a.url} target="_blank" rel="noreferrer" strong>
              {a.title}
            </Typography.Link>
          </div>
          {showSummary && a.summary && (
            <Typography.Paragraph type="secondary" style={{ margin: 0 }} ellipsis={{ rows: 2, expandable: true }}>
              {a.summary}
            </Typography.Paragraph>
          )}
        </div>
      ))}
    </div>
  );
}
