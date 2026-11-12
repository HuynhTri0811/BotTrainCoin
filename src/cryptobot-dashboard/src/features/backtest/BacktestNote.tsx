import { Typography } from 'antd';

interface Props {
  note: string | null;
  /** Gọi khi ghi chú thật sự đổi (chuỗi trống = xóa ghi chú). */
  onSave: (note: string) => void;
}

/** Ghi chú của một lần backtest, bấm để sửa tại chỗ. */
export function BacktestNote({ note, onSave }: Props) {
  return (
    <Typography.Text
      type={note ? undefined : 'secondary'}
      editable={{
        text: note ?? '',
        maxLength: 500,
        tooltip: 'Sửa ghi chú',
        onChange: (value) => {
          if (value !== (note ?? '')) onSave(value);
        },
      }}
    >
      {note ?? 'thêm ghi chú'}
    </Typography.Text>
  );
}
