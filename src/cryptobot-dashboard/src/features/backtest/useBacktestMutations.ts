import { message } from 'antd';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { backtestApi, type BacktestResult } from '../../api/client';

/** Khóa cache kết quả đầy đủ của một lần backtest (trang chi tiết). */
export const backtestQueryKey = (id: number) => ['backtest', id] as const;

/**
 * Sửa ghi chú và xóa một lần backtest, dùng chung cho danh sách và trang chi tiết: cùng cập nhật cache chi tiết và
 * tải lại danh sách.
 */
export function useBacktestMutations({ onDeleted }: { onDeleted?: (id: number) => void } = {}) {
  const queryClient = useQueryClient();
  const refreshList = () => queryClient.invalidateQueries({ queryKey: ['backtests'] });

  const setNote = useMutation({
    mutationFn: ({ id, note }: { id: number; note: string }) => backtestApi.setNote(id, note),
    onSuccess: (saved, { id }) => {
      queryClient.setQueryData<BacktestResult>(backtestQueryKey(id), (current) => current && { ...current, note: saved.note });
      refreshList();
    },
    onError: (error) => message.error(error.message),
  });

  const remove = useMutation({
    mutationFn: (id: number) => backtestApi.remove(id),
    onSuccess: (_, id) => {
      // Gọi trước khi bỏ cache: trang chi tiết tắt query của nó, nếu không query sẽ tải lại lần chạy vừa xóa.
      onDeleted?.(id);
      queryClient.removeQueries({ queryKey: backtestQueryKey(id) });
      refreshList();
    },
    onError: (error) => message.error(error.message),
  });

  return { setNote, remove };
}
