import { useMemo } from 'react';
import { Alert, Card, Col, Descriptions, Row, Statistic, Table, Tag, Typography } from 'antd';
import type { BacktestExitReason, BacktestResult, BacktestTrade, TradeSide } from '../../api/client';
import { EXCEL_FORMAT, downloadExcel, type ExcelColumn } from '../../components/excel';
import { ExportExcelButton } from '../../components/ExportExcelButton';
import { excelVnDate, formatVn } from '../../time';
import { fmtPercent, fmtPrice2, fmtSigned, fmtSignedPercent, fmtUsdt, pnlColor } from '../futures/format';
import { BacktestChart } from './BacktestChart';
import { BacktestTradeDetail } from './BacktestTradeDetail';
import {
  EMA_FAST_COLOR,
  EMA_SLOW_COLOR,
  ENTRY_COLOR,
  EXIT_COLORS,
  LIQUIDATION_COLOR,
  SHORT_COLOR,
  SL_COLOR,
  TP_COLOR,
  sideColor,
  toOverlayTrades,
} from './tradeOverlay';
import { fmtSizing, fmtStops } from './BacktestHistoryCard';
import { fmtMode, fmtSignal, isEmaCross, signalName } from './describe';

const quantity = new Intl.NumberFormat('en-US', { maximumFractionDigits: 6 });

/** Nhãn lý do thoát; Spot đóng lệnh khi bot phát SELL, Futures khi tín hiệu đảo chiều. */
const exitLabels = (futures: boolean): Record<BacktestExitReason, string> => ({
  Signal: futures ? 'Tín hiệu đảo chiều' : 'SELL (EMA cắt xuống)',
  TakeProfit: 'Chốt lời',
  StopLoss: 'Cắt lỗ',
  Liquidation: 'Thanh lý',
});

const excelColumns = (labels: Record<BacktestExitReason, string>): ExcelColumn<BacktestTrade>[] => [
  { header: 'Chiều', value: (trade) => trade.side, width: 7 },
  { header: 'Vào lúc', value: (trade) => excelVnDate(trade.entryTimeUtc), format: EXCEL_FORMAT.dateTime, width: 17 },
  { header: 'Giá vào', value: (trade) => trade.entryPrice, format: EXCEL_FORMAT.price, width: 12 },
  { header: 'Ra lúc', value: (trade) => excelVnDate(trade.exitTimeUtc), format: EXCEL_FORMAT.dateTime, width: 17 },
  { header: 'Giá ra', value: (trade) => trade.exitPrice, format: EXCEL_FORMAT.price, width: 12 },
  { header: 'Lý do thoát', value: (trade) => labels[trade.exitReason], width: 20 },
  { header: 'Số coin', value: (trade) => trade.quantity, format: '0.000000', width: 12 },
  { header: 'Phí (USDT)', value: (trade) => trade.fees, format: EXCEL_FORMAT.amount, width: 11 },
  { header: 'Lãi/lỗ sau phí (USDT)', value: (trade) => trade.netPnl, format: EXCEL_FORMAT.signedAmount, width: 18 },
  { header: 'Lãi/lỗ %', value: (trade) => trade.returnPercent, format: EXCEL_FORMAT.signedPercent, width: 10 },
];

/** Bảng thống kê, chart điểm vào/ra và danh sách lệnh của một lần backtest. */
export function BacktestResultView({ result }: { result: BacktestResult }) {
  const stats = result.statistics;
  const futures = result.settings.futures;
  const labels = exitLabels(futures != null);
  const name = `${signalName(result.signal, result.parameters)} · ${fmtMode(result.settings)} · ${result.symbol} ${result.interval}`;
  const overlayTrades = useMemo(() => toOverlayTrades(result.trades, result.openTrade), [result.trades, result.openTrade]);
  const hasEma = result.chart.bars.some((bar) => bar.emaFast != null);

  if (result.candleCount === 0)
    return <Alert type="warning" showIcon title="Không có nến nào trong khoảng thời gian đã chọn." />;

  return (
    <>
      <Card
        size="small"
        title={`Kết quả #${result.id} · ${name}`}
        extra={<Typography.Text type="secondary">Chạy lúc {formatVn(result.createdAtUtc)} · đã lưu vào lịch sử</Typography.Text>}
      >
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
              styles={{ content: { color: 'var(--down)' } }}
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
          <Descriptions.Item label="Kiểu · tín hiệu">
            {fmtMode(result.settings)} · {fmtSignal(result.signal, result.parameters)}
          </Descriptions.Item>
          <Descriptions.Item label="Vốn · rủi ro">
            {fmtSizing(result.settings)} · {fmtStops(result.settings)} · phí {result.settings.feePercent}%
            {futures && ` · trượt giá ${futures.slippagePercent}% · ký quỹ duy trì ${futures.maintenanceMarginPercent}%`}
          </Descriptions.Item>
          {result.note && <Descriptions.Item label="Ghi chú">{result.note}</Descriptions.Item>}
          <Descriptions.Item label="Khoảng nến">
            {formatVn(result.firstCandleUtc)} → {formatVn(result.lastCandleUtc)}
          </Descriptions.Item>
          <Descriptions.Item label="Số nến">
            {result.candleCount.toLocaleString('vi-VN')} (thêm {result.warmupCandles} nến warm-up phía trước)
          </Descriptions.Item>
          <Descriptions.Item label={futures ? 'Tín hiệu' : 'Tín hiệu của bot'}>
            {futures ? `${result.buySignals} Long · ${result.sellSignals} Short` : `${result.buySignals} BUY · ${result.sellSignals} SELL`}
          </Descriptions.Item>
          <Descriptions.Item label="Nến bỏ qua vì cửa sổ thiếu nến">
            {result.skippedGapWindows.toLocaleString('vi-VN')}
          </Descriptions.Item>
          {result.openTrade && (
            <Descriptions.Item label="Lệnh còn mở ở nến cuối">
              {futures && <SideTag side={result.openTrade.side} />}
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
        <BacktestChart
          bars={result.chart.bars}
          trades={overlayTrades}
          settings={result.settings}
          parameters={result.parameters}
          showEquity
        />
        <Typography.Text type="secondary">
          {futures ? (
            <>
              <span style={{ color: ENTRY_COLOR }}>▲ LONG</span> · <span style={{ color: SHORT_COLOR }}>▼ SHORT</span>; mũi tên
              thoát ngược chiều: <span style={{ color: EXIT_COLORS.Signal }}>đóng khi đảo chiều</span> ·{' '}
              <span style={{ color: EXIT_COLORS.TakeProfit }}>chốt lời</span> · <span style={{ color: EXIT_COLORS.StopLoss }}>cắt lỗ</span> ·{' '}
              <span style={{ color: EXIT_COLORS.Liquidation }}>thanh lý</span> (kèm giá và lãi/lỗ trên margin). Trong lúc giữ lệnh:{' '}
              <span style={{ color: ENTRY_COLOR }}>— giá vào Long</span> · <span style={{ color: SHORT_COLOR }}>— giá vào Short</span>
            </>
          ) : (
            <>
              <span style={{ color: ENTRY_COLOR }}>▲ vào lệnh</span> · <span style={{ color: EXIT_COLORS.Signal }}>▼ SELL</span> ·{' '}
              <span style={{ color: EXIT_COLORS.TakeProfit }}>▼ chốt lời</span> · <span style={{ color: EXIT_COLORS.StopLoss }}>▼ cắt lỗ</span>{' '}
              (kèm giá và lãi/lỗ). Trong lúc giữ lệnh: <span style={{ color: ENTRY_COLOR }}>— giá vào</span>
            </>
          )}
          {result.settings.takeProfitPercent != null && <span style={{ color: TP_COLOR }}> · - - mức chốt lời</span>}
          {result.settings.stopLossPercent != null && <span style={{ color: SL_COLOR }}> · - - mức cắt lỗ</span>}
          {futures && <span style={{ color: LIQUIDATION_COLOR }}> · ··· giá thanh lý</span>}.
          {!isEmaCross(result.signal) ? (
            ` Tín hiệu từ chỉ báo ${result.signal.type} (không vẽ đường chỉ báo trên chart này).`
          ) : hasEma && result.parameters ? (
            <>
              {' '}<span style={{ color: EMA_FAST_COLOR }}>— EMA{result.parameters.fastPeriod}</span> ·{' '}
              <span style={{ color: EMA_SLOW_COLOR }}>— EMA{result.parameters.slowPeriod}</span>.
            </>
          ) : (
            ' Lần chạy này lưu trước khi chart có EMA; chạy lại để thấy đường EMA.'
          )}{' '}
          Khung dưới: tổng vốn. Kéo/cuộn chuột để phóng to; bấm một lệnh ở bảng dưới để xem nến thật của lệnh đó.
        </Typography.Text>
      </Card>

      <Card
        size="small"
        title={`Danh sách lệnh (${result.trades.length})`}
        extra={
          <ExportExcelButton
            onExport={async () => ({
              count: await downloadExcel(`backtest-${result.symbol}-${result.interval}`, 'Lệnh', result.trades, excelColumns(labels)),
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
          // Bấm một dòng để xem chart nến thật của lệnh đó ngay bên dưới dòng.
          expandable={{
            expandRowByClick: true,
            expandedRowRender: (trade) => (
              <BacktestTradeDetail result={result} trade={trade} />
            ),
          }}
          columns={[
            ...(futures
              ? [
                  {
                    title: 'Chiều',
                    dataIndex: 'side',
                    filters: (['Long', 'Short'] as const).map((side) => ({ text: side, value: side })),
                    onFilter: (value: unknown, trade: BacktestTrade) => trade.side === value,
                    render: (side: TradeSide) => <SideTag side={side} />,
                  },
                ]
              : []),
            { title: 'Vào lúc', dataIndex: 'entryTimeUtc', render: formatVn },
            { title: 'Giá vào', dataIndex: 'entryPrice', align: 'right', render: fmtPrice2 },
            { title: 'Ra lúc', dataIndex: 'exitTimeUtc', render: formatVn },
            { title: 'Giá ra', dataIndex: 'exitPrice', align: 'right', render: fmtPrice2 },
            {
              title: 'Lý do thoát',
              dataIndex: 'exitReason',
              filters: (Object.keys(labels) as BacktestExitReason[])
                .filter((reason) => futures != null || reason !== 'Liquidation')
                .map((reason) => ({ text: labels[reason], value: reason })),
              onFilter: (value, trade) => trade.exitReason === value,
              render: (reason: BacktestExitReason) => <Tag color={EXIT_COLORS[reason]}>{labels[reason]}</Tag>,
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

const SideTag = ({ side }: { side: TradeSide }) => <Tag color={sideColor(side)}>{side}</Tag>;
