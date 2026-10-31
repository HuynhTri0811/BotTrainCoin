import { useState } from 'react';
import { Alert, Card, Empty, Segmented, Space, Spin, Tooltip, Typography } from 'antd';
import { useQuery } from '@tanstack/react-query';
import { historyApi, type MonthStats } from '../api/client';
import { MoveEventsCard } from '../features/history/MoveEventsCard';
import { fmtPct, MONTHS, MonthlyHeatmap, pctColor } from '../features/history/MonthlyHeatmap';

const day = new Intl.DateTimeFormat('vi-VN', { timeZone: 'UTC', day: '2-digit', month: '2-digit', year: 'numeric' });

/** Cột trung bình mỗi tháng quanh mốc 0: lên là tăng, xuống là giảm. */
function MonthAverageBars({ stats }: { stats: MonthStats[] }) {
  const byMonth = new Map(stats.map((monthStats) => [monthStats.month, monthStats]));
  const max = Math.max(1, ...stats.map((monthStats) => Math.abs(monthStats.averageChangePercent)));
  const half = 90; // px mỗi phía của mốc 0

  return (
    <div style={{ overflowX: 'auto' }}>
      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(12, minmax(48px, 1fr))', gap: 6, minWidth: 600 }}>
        {MONTHS.map((month) => {
          const stats = byMonth.get(month);
          const value = stats?.averageChangePercent ?? 0;
          const barHeight = (Math.abs(value) / max) * half;
          return (
            <Tooltip
              key={month}
              title={
                stats
                  ? `T${month}: trung bình ${fmtPct(value)} qua ${stats.years} năm · tăng ${stats.upCount}/${stats.years} năm`
                  : `T${month}: chưa có tháng trọn vẹn`
              }
            >
              <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'center', fontSize: 12 }}>
                <div style={{ height: half, display: 'flex', alignItems: 'flex-end', width: '100%', justifyContent: 'center' }}>
                  {value > 0 && <div style={{ width: '60%', maxWidth: 32, height: barHeight, background: '#16a34a', borderRadius: '4px 4px 0 0' }} />}
                </div>
                <div style={{ height: 1, width: '100%', background: '#94a3b8' }} />
                <div style={{ height: half, display: 'flex', alignItems: 'flex-start', width: '100%', justifyContent: 'center' }}>
                  {value < 0 && <div style={{ width: '60%', maxWidth: 32, height: barHeight, background: '#dc2626', borderRadius: '0 0 4px 4px' }} />}
                </div>
                <span style={{ fontWeight: 600, color: pctColor(value), fontVariantNumeric: 'tabular-nums' }}>
                  {stats ? fmtPct(value) : '—'}
                </span>
                <span style={{ color: '#64748b' }}>T{month}</span>
              </div>
            </Tooltip>
          );
        })}
      </div>
    </div>
  );
}

/** Chênh lệch giá mở cửa → đóng cửa theo từng tháng của từng năm, từ nến ngày lịch sử (Bitfinex từ 2016). */
export function HistoryPage() {
  const [symbol, setSymbol] = useState('BTCUSD');
  const query = useQuery({
    queryKey: ['history-monthly', symbol],
    queryFn: () => historyApi.monthly(symbol),
    refetchInterval: 5 * 60_000,
  });
  const data = query.data;
  const symbols = data?.symbols ?? [symbol];

  const ranked = [...(data?.monthStats ?? [])].sort((first, second) => second.averageChangePercent - first.averageChangePercent);
  const best = ranked[0];
  const worst = ranked[ranked.length - 1];
  const mostUp = [...(data?.monthStats ?? [])].sort((first, second) => second.upPercent - first.upPercent)[0];

  return (
    <Space orientation="vertical" size="middle" style={{ width: '100%' }}>
      <Card
        size="small"
        title={`Chênh lệch giá mở → đóng cửa theo tháng · ${symbol.replace(/USD$/, '')}`}
        extra={
          <Segmented
            size="small"
            value={symbol}
            onChange={(value) => setSymbol(value as string)}
            options={symbols.map((symbol) => ({ value: symbol, label: symbol.replace(/USD$/, '') }))}
          />
        }
      >
        {query.isPending ? (
          <div style={{ height: 320, display: 'grid', placeItems: 'center' }}>
            <Spin />
          </div>
        ) : query.isError ? (
          <Alert type="error" showIcon title={`Không tải được dữ liệu: ${query.error.message}`} />
        ) : !data || data.months.length === 0 ? (
          <Empty description="Chưa có nến ngày." />
        ) : (
          <Space orientation="vertical" size="middle" style={{ width: '100%' }}>
            {best && worst && mostUp && (
              <Typography.Paragraph style={{ margin: 0 }}>
                Từ {day.format(new Date(data.firstDayUtc))}, tháng tăng trung bình mạnh nhất là{' '}
                <Typography.Text strong style={{ color: pctColor(best.averageChangePercent) }}>
                  T{best.month} ({fmtPct(best.averageChangePercent)})
                </Typography.Text>
                , yếu nhất là{' '}
                <Typography.Text strong style={{ color: pctColor(worst.averageChangePercent) }}>
                  T{worst.month} ({fmtPct(worst.averageChangePercent)})
                </Typography.Text>
                . T{mostUp.month} tăng nhiều năm nhất: {mostUp.upCount}/{mostUp.years} năm.
              </Typography.Paragraph>
            )}
            <MonthlyHeatmap data={data} />
            <Typography.Paragraph type="secondary" style={{ margin: 0, fontSize: 12 }}>
              Mỗi ô = (giá đóng cửa ngày cuối tháng − giá mở cửa ngày đầu tháng) / giá mở cửa. Màu càng đậm chênh lệch
              càng lớn (đậm nhất từ ±30%). Chấm tím là tháng có sự kiện ghi nhận. * là tháng/năm chưa trọn, không tính vào các dòng thống kê. Rê chuột vào ô
              để xem giá. Nến ngày {data.exchange} theo giờ UTC, cập nhật đến {day.format(new Date(data.lastDayUtc))}; sàn bỏ
              qua ngày không có giao dịch nên vài tháng đầu có thể thiếu ngày.
            </Typography.Paragraph>
          </Space>
        )}
      </Card>

      {data && data.months.length > 0 && <MoveEventsCard key={data.symbol} data={data} />}

      {data && data.monthStats.length > 0 && (
        <Card size="small" title={`Trung bình mỗi tháng qua các năm · ${symbol.replace(/USD$/, '')}`}>
          <MonthAverageBars stats={data.monthStats} />
          <Typography.Paragraph type="secondary" style={{ margin: '8px 0 0', fontSize: 12 }}>
            Trung bình cộng % chênh lệch của cùng một tháng qua các năm. Một tháng tăng rất mạnh có thể kéo trung bình
            lên, nên xem thêm dòng "Số năm tăng" trong bảng. Đây là thống kê quá khứ, không phải dự đoán.
          </Typography.Paragraph>
        </Card>
      )}
    </Space>
  );
}
