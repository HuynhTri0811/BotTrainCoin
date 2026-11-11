import { useEffect, useState } from 'react';
import { Alert, Button, Card, Flex, Popconfirm, Space, Spin, Typography } from 'antd';
import { useQuery } from '@tanstack/react-query';
import { ApiError, backtestApi } from '../../api/client';
import { BacktestNote } from './BacktestNote';
import { BacktestResultView } from './BacktestResultView';
import { backtestQueryKey, useBacktestMutations } from './useBacktestMutations';

interface Props {
  id: number;
  /** Quay lại danh sách (cũng gọi sau khi xóa lần chạy này). */
  onBack: () => void;
}

const isNotFound = (error: Error) => error instanceof ApiError && error.status === 404;

/** Trang chi tiết một lần backtest đã lưu: thống kê, chart, danh sách lệnh, sửa ghi chú, xóa. */
export function BacktestDetail({ id, onBack }: Props) {
  // Đã xóa: tắt query để bỏ cache xong không tải lại lần chạy vừa xóa trong lúc chờ quay về danh sách.
  const [deleted, setDeleted] = useState(false);
  const detail = useQuery({
    queryKey: backtestQueryKey(id),
    queryFn: () => backtestApi.get(id),
    enabled: !deleted,
    // Lần chạy đã lưu không đổi (ghi chú được cập nhật thẳng vào cache), không cần tải lại khi focus.
    staleTime: Infinity,
    // Không có lần chạy này thì báo ngay, không thử lại.
    retry: (failureCount, error) => !isNotFound(error) && failureCount < 1,
  });
  const { setNote, remove } = useBacktestMutations({
    onDeleted: () => {
      setDeleted(true);
      onBack();
    },
  });

  // Mở từ giữa danh sách dài: đưa về đầu trang chi tiết.
  useEffect(() => {
    window.scrollTo({ top: 0 });
  }, [id]);

  const result = detail.data;
  return (
    <Space orientation="vertical" size="middle" style={{ width: '100%' }}>
      <Card size="small">
        <Flex wrap gap={12} align="center" justify="space-between">
          <Flex wrap gap={12} align="center">
            <Button onClick={onBack}>← Lịch sử backtest</Button>
            <Typography.Text strong>Lần chạy #{id}</Typography.Text>
            {result && <BacktestNote note={result.note} onSave={(note) => setNote.mutate({ id, note })} />}
          </Flex>
          {result && (
            <Popconfirm title={`Xóa lần chạy #${id}?`} okText="Xóa" cancelText="Giữ" onConfirm={() => remove.mutate(id)}>
              <Button danger loading={remove.isPending}>
                Xóa
              </Button>
            </Popconfirm>
          )}
        </Flex>
      </Card>

      {detail.isLoading && <Spin />}
      {detail.isError && (
        <Alert
          type="error"
          showIcon
          title={isNotFound(detail.error) ? `Không có lần backtest #${id} (có thể đã bị xóa).` : detail.error.message}
        />
      )}
      {result && (
        <>
          <BacktestResultView result={result} />
          <Alert
            type="info"
            showIcon
            title="Kết quả trong quá khứ không bảo đảm cho tương lai. Mô phỏng không tính trượt giá, độ sâu sổ lệnh và funding."
          />
        </>
      )}
    </Space>
  );
}
