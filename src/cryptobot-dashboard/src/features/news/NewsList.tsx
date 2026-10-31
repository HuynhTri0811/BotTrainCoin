import { Empty, Space, Tag, Typography } from 'antd';
import type { NewsArticle } from '../../api/client';
import { formatVn } from '../../time';

/** Danh sách tin: giờ đăng, nguồn, coin được nhắc, tiêu đề (link tới bài gốc) và tóm tắt. */
export function NewsList({ articles, showSummary = true }: { articles: NewsArticle[]; showSummary?: boolean }) {
  if (articles.length === 0) return <Empty image={Empty.PRESENTED_IMAGE_SIMPLE} description="Không có tin." />;

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 12 }}>
      {articles.map((article) => (
        <div key={article.id} style={{ borderBottom: '1px solid #f1f5f9', paddingBottom: 8 }}>
          <Space size={4} wrap style={{ fontSize: 12 }}>
            <Typography.Text type="secondary">{formatVn(article.publishedAtUtc)}</Typography.Text>
            <Tag>{article.source}</Tag>
            {article.coins.map((coin) => (
              <Tag key={coin} color={coin === 'BTC' ? 'orange' : coin === 'ETH' ? 'blue' : 'green'}>
                {coin}
              </Tag>
            ))}
          </Space>
          <div>
            <Typography.Link href={article.url} target="_blank" rel="noreferrer" strong>
              {article.title}
            </Typography.Link>
          </div>
          {showSummary && article.summary && (
            <Typography.Paragraph type="secondary" style={{ margin: 0 }} ellipsis={{ rows: 2, expandable: true }}>
              {article.summary}
            </Typography.Paragraph>
          )}
        </div>
      ))}
    </div>
  );
}
