interface Props {
  values: number[];
  width?: number;
  height?: number;
  color?: string;
}

/** Đường giá nhỏ gọn, tự co giãn theo min/max của dữ liệu. */
export function Sparkline({ values, width = 220, height = 48, color = '#2563eb' }: Props) {
  if (values.length < 2) return <svg width={width} height={height} aria-hidden />;

  const min = Math.min(...values);
  const max = Math.max(...values);
  const span = max - min || 1;
  const pad = 3;
  const points = values
    .map((v, i) => {
      const x = pad + (i / (values.length - 1)) * (width - pad * 2);
      const y = pad + (1 - (v - min) / span) * (height - pad * 2);
      return `${x.toFixed(1)},${y.toFixed(1)}`;
    })
    .join(' ');
  const last = points.split(' ').at(-1)!.split(',');

  return (
    <svg width={width} height={height} role="img" aria-label="Biến động giá gần đây">
      <polyline points={points} fill="none" stroke={color} strokeWidth={1.5} strokeLinejoin="round" />
      <circle cx={last[0]} cy={last[1]} r={3} fill={color} />
    </svg>
  );
}
