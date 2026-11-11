import { Alert, Card, Col, Descriptions, Row, Statistic, Table, Tag, Typography } from 'antd';
import type { BacktestExitReason, BacktestResult, BacktestTrade } from '../../api/client';
import { EXCEL_FORMAT, downloadExcel, type ExcelColumn } from '../../components/excel';
import { ExportExcelButton } from '../../components/ExportExcelButton';
import { excelVnDate, formatVn } from '../../time';
import { fmtPrice2, fmtSigned, fmtUsdt, pnlColor } from '../futures/format';
import { BacktestChart, EXIT_COLORS } from './BacktestChart';

const percent = new Intl.NumberFormat('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
const fmtPercent = (value: number) => `${percent.format(value)}%`;
const fmtSignedPercent = (value: number) => `${value > 0 ? '+' : ''}${percent.format(value)}%`;
const quantity = new Intl.NumberFormat('en-US', { maximumFractionDigits: 6 });

const EXIT_LABELS: Record<BacktestExitReason, string> = {
  Signal: 'SELL (EMA cắt xuống)',
  TakeProfit: 'Chốt lời',
  StopLoss: 'Cắt lỗ',
};

const excelColumns: ExcelColumn<BacktestTrade>[] = [
  { header: 'Vào lúc', value: (trade) => excelVnDate(trade.entryTimeUtc), format: EXCEL_FORMAT.dateTime, width: 17 },
  { header: 'Giá vào', value: (trade) => trade.entryPrice, format: EXCEL_FORMAT.price, width: 12 },
  { header: 'Ra lúc', value: (trade) => excelVnDate(trade.exitTimeUtc), format: EXCEL_FORMAT.dateTime, width: 17 },
  { header: 'Giá ra', value: (trade) => trade.exitPrice, format: EXCEL_FORMAT.price, width: 12 },
  { header: 'Lý do thoát', value: (trade) => EXIT_LABELS[trade.exitReason], width: 20 },
  { header: 'Số coin', value: (trade) => trade.quantity, format: '0.000000', width: 12 },
  { header: 'Phí (USDT)', value: (trade) => trade.fees, format: EXCEL_FORMAT.amount, width: 11 },
  { header: 'Lãi/lỗ sau phí (USDT)', value: (trade) => trade.netPnl, format: EXCEL_FORMAT.signedAmount, width: 18 },
  { header: 'Lãi/lỗ %', value: (trade) => trade.returnPercent, format: EXCEL_FORMAT.signedPercent, width: 10 },
];

/** Bảng thống kê, chart điểm vào/ra và danh sách lệnh của một lần backtest. */
export function BacktestResultView({ result }: { result: BacktestResult }) {
  const stats = result.statistics;
  const name = `EMA${result.parameters.fastPeriod}/${result.parameters.slowPeriod} · ${result.symbol} ${result.interval}`;

  if (result.candleCount === 0)
    return <Alert type="warning" showIcon title="Không có nến nào trong khoảng thời gian đã chọn." />;

  return (
    <>
      <Card size="small" title={`Kết quả · ${name}`}>
        <Row gutter={[16, 16]}>
          <Col xs={12} md={6}>
            <Statistic title={`Tổng số lệnh đã đóng (${stats.wins} thắng · ${stats.losses} thua)`} value={stats.totalTrades} />
          </Col>
          <Col xs={12} md={6}>
            <Statistic title="Tỷ lệ thắng" value={fmtPercent(stats.winRatePercent)} />
          </Col>
          <Col xs={12} md={6}>
            <Statistic
              title="Lợi nhuận ròng (sau phí)"
              value={fmtSigned(stats.netProfit)}
              suffix={`USDT · ${fmtSignedPercent(stats.netProfitPercent)}`}
              styles={{ content: { color: pnlColor(stats.netProfit) } }}
            />
          </Col>
          <Col xs={12} md={6}>
            <Statistic
              title="Sụt giảm tối đa (Max Drawdown)"
              value={fmtUsdt(stats.maxDrawdown)}
              suffix={`· ${fmtPercent(stats.maxDrawdownPercent)}`}
              styles={{ content: { color: '#dc2626' } }}
            />
          </Col>
          <Col xs={12} md={6}>
            <Statistic title="Vốn cuối" value={fmtUsdt(stats.endingEquity)} />
          </Col>
          <Col xs={12} md={6}>
            <Statistic title="Tổng phí đã trả" value={fmtUsdt(stats.totalFees)} />
          </Col>
          <Col xs={12} md={6}>
            <Statistic title="Profit factor" value={stats.profitFactor == null ? '—' : stats.profitFactor.toFixed(2)} />
          </Col>
          <Col xs={12} md={6}>
            <Statistic
              title={`Mua và giữ ${result.symbol} cùng kỳ`}
              value={fmtSignedPercent(stats.buyAndHoldPercent)}
              styles={{ content: { color: pnlColor(stats.buyAndHoldPercent) } }}
            />
          </Col>
        </Row>
        <Descriptions size="small" column={{ xs: 1, md: 2 }} style={{ marginTop: 16 }}>
          <Descriptions.Item label="Khoảng nến">
            {formatVn(result.firstCandleUtc)} → {formatVn(result.lastCandleUtc)}
          </Descriptions.Item>
          <Descriptions.Item label="Số nến">
            {result.candleCount.toLocaleString('vi-VN')} (thêm {result.warmupCandles} nến warm-up phía trước)
          </Descriptions.Item>
          <Descriptions.Item label="Tín hiệu của bot">
            {result.buySignals} BUY · {result.sellSignals} SELL
          </Descriptions.Item>
          <Descriptions.Item label="Nến bỏ qua vì cửa sổ thiếu nến">
            {result.skippedGapWindows.toLocaleString('vi-VN')}
          </Descriptions.Item>
          {result.openTrade && (
            <Descriptions.Item label="Lệnh còn mở ở nến cuối">
              Vào {formatVn(result.openTrade.entryTimeUtc)} @ {fmtPrice2(result.openTrade.entryPrice)} · tạm tính{' '}
              <span style={{ color: pnlColor(result.openTrade.unrealizedPnl) }}>
                {fmtSigned(result.openTrade.unrealizedPnl)} USDT
              </span>{' '}
              (không tính vào số lệnh và lợi nhuận ròng)
            </Descriptions.Item>
          )}
        </Descriptions>
      </Card>

      <Card
        size="small"
        title="Điểm vào/ra lệnh"
        extra={
          result.chart.candlesPerBar > 1 && (
            <Typography.Text type="secondary">Mỗi cây nến gộp {result.chart.candlesPerBar} nến {result.interval}</Typography.Text>
          )
        }
      >
        <BacktestChart result={result} />
        <Typography.Text type="secondary">
          <span style={{ color: '#2563eb' }}>▲ vào lệnh</span> · <span style={{ color: EXIT_COLORS.Signal }}>▼ SELL</span> ·{' '}
          <span style={{ color: EXIT_COLORS.TakeProfit }}>▼ chốt lời</span> · <span style={{ color: EXIT_COLORS.StopLoss }}>▼ cắt lỗ</span>.
          Khung dưới: tổng vốn. Kéo/cuộn chuột trên biểu đồ để phóng to một đoạn.
        </Typography.Text>
      </Card>

      <Card
        size="small"
        title={`Danh sách lệnh (${result.trades.length})`}
        extra={
          <ExportExcelButton
            onExport={async () => ({
              count: await downloadExcel(`backtest-${result.symbol}-${result.interval}`, 'Lệnh', result.trades, excelColumns),
            })}
          />
        }
      >
        <Table<BacktestTrade>
          rowKey="entryTimeUtc"
          size="small"
          dataSource={result.trades}
          pagination={{ pageSize: 20, showSizeChanger: false, hideOnSinglePage: true }}
          scroll={{ x: true }}
          columns={[
            { title: 'Vào lúc', dataIndex: 'entryTimeUtc', render: formatVn },
            { title: 'Giá vào', dataIndex: 'entryPrice', align: 'right', render: fmtPrice2 },
            { title: 'Ra lúc', dataIndex: 'exitTimeUtc', render: formatVn },
            { title: 'Giá ra', dataIndex: 'exitPrice', align: 'right', render: fmtPrice2 },
            {
              title: 'Lý do thoát',
              dataIndex: 'exitReason',
              filters: (Object.keys(EXIT_LABELS) as BacktestExitReason[]).map((reason) => ({ text: EXIT_LABELS[reason], value: reason })),
              onFilter: (value, trade) => trade.exitReason === value,
              render: (reason: BacktestExitReason) => <Tag color={EXIT_COLORS[reason]}>{EXIT_LABELS[reason]}</Tag>,
            },
            { title: 'Số coin', dataIndex: 'quantity', align: 'right', render: (value: number) => quantity.format(value) },
            { title: 'Phí', dataIndex: 'fees', align: 'right', render: (value: number) => fmtPrice2(value) },
            {
              title: 'Lãi/lỗ sau phí',
              dataIndex: 'netPnl',
              align: 'right',
              sorter: (first, second) => first.netPnl - second.netPnl,
              render: (value: number, trade) => (
                <span style={{ color: pnlColor(value) }}>
                  {fmtSigned(value)} ({fmtSignedPercent(trade.returnPercent)})
                </span>
              ),
            },
          ]}
        />
      </Card>
    </>
  );
}
