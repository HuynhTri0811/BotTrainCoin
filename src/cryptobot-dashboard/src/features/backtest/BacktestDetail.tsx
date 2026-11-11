import { useEffect } from 'react';
import { Alert, Button, Card, Flex, Popconfirm, Space, Spin, Typography, message } from 'antd';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { backtestApi, type BacktestResult } from '../../api/client';
import { BacktestResultView } from './BacktestResultView';

interface Props {
  id: number;
  /** Quay lại danh sách (cũng gọi sau khi xóa lần chạy này). */
  onBack: () => void;
}

/** Trang chi tiết một lần backtest đã lưu: thống kê, chart, danh sách lệnh, sửa ghi chú, xóa. */
export function BacktestDetail({ id, onBack }: Props) {
  const queryClient = useQueryClient();
  const detail = useQuery({ queryKey: ['backtest', id], queryFn: () => backtestApi.get(id) });

  // Mở từ giữa danh sách dài: đưa về đầu trang chi tiết.
  useEffect(() => {
    window.scrollTo({ top: 0 });
  }, [id]);

  const setNote = useMutation({
    mutationFn: (note: string) => backtestApi.setNote(id, note),
    onSuccess: (saved) => {
      queryClient.setQueryData<BacktestResult>(['backtest', id], (current) => current && { ...current, note: saved.note });
      queryClient.invalidateQueries({ queryKey: ['backtests'] });
    },
    onError: (error) => message.error(error.message),
  });
  const remove = useMutation({
    mutationFn: () => backtestApi.remove(id),
    onSuccess: () => {
      queryClient.removeQueries({ queryKey: ['backtest', id] });
      queryClient.invalidateQueries({ queryKey: ['backtests'] });
      message.success(`Đã xóa lần chạy #${id}.`);
      onBack();
    },
    onError: (error) => message.error(error.message),
  });

  const result = detail.data;
  return (
    <Space orientation="vertical" size="middle" style={{ width: '100%' }}>
      <Card size="small">
        <Flex wrap gap={12} align="center" justify="space-between">
          <Flex wrap gap={12} align="center">
            <Button onClick={onBack}>← Lịch sử backtest</Button>
            <Typography.Text strong>Lần chạy #{id}</Typography.Text>
            {result && (
              <Typography.Text
                type={result.note ? undefined : 'secondary'}
                editable={{
                  text: result.note ?? '',
                  maxLength: 500,
                  tooltip: 'Sửa ghi chú',
                  onChange: (value) => {
                    if (value !== (result.note ?? '')) setNote.mutate(value);
                  },
                }}
              >
                {result.note ?? 'thêm ghi chú'}
              </Typography.Text>
            )}
          </Flex>
          {result && (
            <Popconfirm title={`Xóa lần chạy #${id}?`} okText="Xóa" cancelText="Giữ" onConfirm={() => remove.mutate()}>
              <Button danger loading={remove.isPending}>
                Xóa
              </Button>
            </Popconfirm>
          )}
        </Flex>
      </Card>

      {detail.isPending && <Spin />}
      {detail.isError && <Alert type="error" showIcon title={detail.error.message} />}
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
