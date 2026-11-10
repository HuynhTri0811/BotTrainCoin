import { useMemo, useState } from 'react';
import { Alert, Card, Col, Row, Segmented, Space, Statistic, Table, Tag, Typography } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { useQuery } from '@tanstack/react-query';
import {
  predictionApi,
  type MarketOpening,
  type PredictionHistory,
  type PriceDirection,
  type StockMarket,
} from '../../api/client';
import { EXCEL_FORMAT, downloadExcel, type ExcelColumn } from '../../components/excel';
import { ExportExcelButton } from '../../components/ExportExcelButton';
import { applyColumnFilters, columnFilter, useScopedFilters, withFilteredValues } from '../../components/tableFilters';
import { fmtPrice2 as fmtPrice } from '../futures/format';
import { excelVnDateUnix, formatVnTimeDayUnix, formatVnTimeUnix, formatVnUnix } from '../../time';

const HISTORY = 300;

const DIRECTIONS = [
  { text: '▲ Lên', value: 'Up' },
  { text: '▼ Xuống', value: 'Down' },
];

function DirectionTag({ direction, big }: { direction: PriceDirection | null; big?: boolean }) {
  if (direction == null) return <Tag>Đứng giá</Tag>;
  const style = big ? { fontSize: 18, padding: '4px 12px' } : undefined;
  return direction === 'Up' ? (
    <Tag color="green" style={style}>
      ▲ Lên
    </Tag>
  ) : (
    <Tag color="red" style={style}>
      ▼ Xuống
    </Tag>
  );
}

/** Màu thẻ theo thứ tự sàn API trả về; tránh xanh lá và đỏ vì đã dùng cho Lên/Xuống. */
const MARKET_PALETTE = ['magenta', 'cyan', 'gold', 'purple', 'blue', 'orange', 'geekblue', 'lime', 'volcano'];

/** Phần của danh sách sàn dùng để dựng cột; không đổi giữa các lần tải lại nên cột không dựng lại mỗi 15 giây. */
type MarketColumnInfo = Pick<StockMarket, 'code' | 'name' | 'exchange'>;

const marketColor = (markets: MarketColumnInfo[], code: string) => {
  const index = markets.findIndex((market) => market.code === code);
  return index < 0 ? undefined : MARKET_PALETTE[index % MARKET_PALETTE.length];
};

const openingText = (opening: MarketOpening) => `${opening.name} ${formatVnTimeUnix(opening.time)}`;

function OpeningTags({ openings, markets }: { openings: MarketOpening[]; markets: MarketColumnInfo[] }) {
  if (openings.length === 0) return <Typography.Text type="secondary">—</Typography.Text>;
  return (
    <Space size={4} wrap>
      {openings.map((opening) => (
        <Tag
          key={`${opening.code}-${opening.time}`}
          color={marketColor(markets, opening.code)}
          title={`Sàn ${opening.exchange} mở cửa`}
        >
          {openingText(opening)}
        </Tag>
      ))}
    </Space>
  );
}

/** Ghi chú giờ mở cửa: lần mở cửa kế tiếp của từng sàn theo giờ Việt Nam, đã tính giờ mùa hè và ngày lễ đã biết. */
function MarketHoursNote({ markets }: { markets: StockMarket[] }) {
  const nextOpens = markets
    .map((market) =>
      market.nextOpenTime == null
        ? `${market.name} (${market.exchange}) ${market.localOpen} giờ địa phương`
        : `${market.name} (${market.exchange}) ${formatVnTimeDayUnix(market.nextOpenTime)}`,
    )
    .join(', ');
  const knownYears = [...new Set(markets.map((market) => market.holidaysKnownThroughYear))].sort();
  const holidayNote = markets
    .filter((market) => market.holidaysKnownThroughYear < knownYears[knownYears.length - 1])
    .map((market) => market.name);
  return (
    <Typography.Paragraph type="secondary" style={{ margin: '4px 0 0', fontSize: 12 }}>
      Lần mở cửa kế tiếp theo giờ Việt Nam: {nextOpens}. Đã tính giờ mùa hè và ngày nghỉ lễ đến hết năm{' '}
      {knownYears[knownYears.length - 1]}
      {holidayNote.length > 0 && ` (${holidayNote.join(', ')} mới có đến ${knownYears[0]})`}.
    </Typography.Paragraph>
  );
}

type Mode = 'candle' | 'day';

/**
 * Lần đoán đúng hay sai. `null` khi nến đứng giá (`actual` null): không tính vào đúng/sai. Mọi nến trong `recent` đã
 * đóng, nên `null` không có nghĩa là "chưa biết".
 */
type Outcome = 'Hit' | 'Miss' | null;
const outcomeOf = (entry: PredictionHistory): Outcome =>
  entry.actual == null ? null : entry.actual === entry.predicted ? 'Hit' : 'Miss';

const directionText = (direction: PriceDirection | null) =>
  direction == null ? 'Đứng giá' : direction === 'Up' ? 'Lên' : 'Xuống';
const OUTCOME_TEXT = { Hit: 'Đúng', Miss: 'Sai' } as const;

/** Cột file Excel; `day` đổi tên cột thời gian giống bảng, `withOpenings` thêm cột sàn mở cửa. */
const excelColumns = (day: boolean, withOpenings: boolean): ExcelColumn<PredictionHistory>[] => [
  {
    header: day ? 'Ngày (mở lúc)' : 'Nến (mở lúc)',
    value: (entry) => excelVnDateUnix(entry.time),
    format: EXCEL_FORMAT.dateTime,
    width: 17,
  },
  ...(withOpenings
    ? [{ header: 'Sàn mở cửa', value: (entry: PredictionHistory) => entry.openings.map(openingText).join(', '), width: 22 }]
    : []),
  { header: 'Đoán', value: (entry) => directionText(entry.predicted) },
  { header: 'Điểm', value: (entry) => entry.score, format: '0.0000' },
  { header: 'Thực tế', value: (entry) => directionText(entry.actual) },
  { header: 'Giá đóng trước', value: (entry) => entry.previousClose, format: EXCEL_FORMAT.price, width: 14 },
  { header: 'Giá đóng', value: (entry) => entry.close, format: EXCEL_FORMAT.price, width: 12 },
  { header: 'Tăng/giảm', value: (entry) => entry.change, format: EXCEL_FORMAT.signedAmount, width: 12 },
  { header: 'Tăng/giảm (%)', value: (entry) => entry.changePercent, format: EXCEL_FORMAT.signedPercent, width: 13 },
  {
    header: 'Kết quả',
    value: (entry) => {
      const outcome = outcomeOf(entry);
      return outcome == null ? null : OUTCOME_TEXT[outcome];
    },
  },
];

/**
 * Cột bảng lịch sử dự đoán; bộ lọc của bảng cũng dùng khi xuất Excel. Cột Mở cửa chỉ có khi API trả danh sách sàn,
 * bộ lọc dựng từ chính danh sách đó.
 */
const historyColumns = (day: boolean, markets: MarketColumnInfo[]): ColumnsType<PredictionHistory> => [
  { title: day ? 'Ngày (mở lúc)' : 'Nến (mở lúc)', dataIndex: 'time', render: formatVnUnix },
  ...(markets.length === 0
    ? []
    : [
        {
          title: 'Mở cửa',
          key: 'openings',
          filters: [
            ...markets.map((market) => ({ text: `${market.name} (${market.exchange})`, value: market.code })),
            { text: 'Không có sàn nào', value: 'None' },
          ],
          onFilter: (selected: unknown, entry: PredictionHistory) =>
            selected === 'None'
              ? entry.openings.length === 0
              : entry.openings.some((opening) => opening.code === selected),
          render: (_: unknown, entry: PredictionHistory) => (
            <OpeningTags openings={entry.openings} markets={markets} />
          ),
        },
      ]),
  {
    title: 'Đoán',
    dataIndex: 'predicted',
    ...columnFilter<PredictionHistory>((entry) => entry.predicted, DIRECTIONS),
    render: (direction: PriceDirection) => <DirectionTag direction={direction} />,
  },
  {
    title: 'Thực tế',
    dataIndex: 'actual',
    ...columnFilter<PredictionHistory>((entry) => entry.actual ?? 'Flat', [
      ...DIRECTIONS,
      { text: 'Đứng giá', value: 'Flat' },
    ]),
    render: (direction: PriceDirection | null) => <DirectionTag direction={direction} />,
  },
  {
    title: 'Giá đóng',
    key: 'close',
    align: 'right',
    render: (_, entry) => `${fmtPrice(entry.previousClose)} → ${fmtPrice(entry.close)}`,
  },
  {
    title: 'Tăng/giảm',
    key: 'change',
    align: 'right',
    render: (_, entry) => (
      <span style={{ color: entry.change > 0 ? '#16a34a' : entry.change < 0 ? '#dc2626' : undefined, fontVariantNumeric: 'tabular-nums' }}>
        {entry.change > 0 ? '+' : ''}
        {fmtPrice(entry.change)} ({entry.changePercent > 0 ? '+' : ''}
        {entry.changePercent.toFixed(2)}%)
      </span>
    ),
  },
  {
    title: 'Kết quả',
    key: 'hit',
    ...columnFilter<PredictionHistory>(
      (entry) => outcomeOf(entry) ?? 'None',
      [
        { text: 'Đúng', value: 'Hit' },
        { text: 'Sai', value: 'Miss' },
      ],
    ),
    render: (_, entry) => {
      const outcome = outcomeOf(entry);
      return outcome == null ? '—' : <Tag color={outcome === 'Hit' ? 'green' : 'red'}>{OUTCOME_TEXT[outcome]}</Tag>;
    },
  },
];

/**
 * Kết quả hàm core PriceDirectionPredictor: nến kế tiếp (hoặc ngày hiện tại) lên hay xuống, tỷ lệ đúng khi chạy thử
 * trên lịch sử, và các lần đoán gần nhất so với thực tế. Sửa hàm trong Domain rồi restart API để thấy kết quả mới.
 */
export function PredictionCard({ symbol, interval }: { symbol: string; interval: string }) {
  const [mode, setMode] = useState<Mode>('candle');
  const day = mode === 'day';
  // 1 ngày: cùng hàm dự đoán nhưng chạy trên nến ngày (1d), nến được đoán là ngày đang chạy.
  const queryInterval = day ? '1d' : interval;
  // Bộ lọc controlled: bảng và file xuất cùng đọc state này, về rỗng khi đổi symbol/khung/chế độ.
  const tableScope = `${symbol}-${queryInterval}-${mode}`;
  const [tableFilters, setTableFilters] = useScopedFilters(tableScope);
  const unit = day ? 'ngày' : 'nến';
  const query = useQuery({
    queryKey: ['prediction', symbol, queryInterval],
    queryFn: () => predictionApi.get(symbol, queryInterval, HISTORY),
    refetchInterval: day ? 60_000 : 15_000,
  });
  const prediction = query.data;
  const recentHits = prediction?.recent.filter((entry) => outcomeOf(entry) === 'Hit').length ?? 0;
  const recentCounted = prediction?.recent.filter((entry) => outcomeOf(entry) != null).length ?? 0;

  // So theo nội dung để cột chỉ dựng lại khi danh sách sàn thật sự đổi, không phải mỗi lần tải lại.
  const marketsKey = JSON.stringify(
    prediction?.markets.map(({ code, name, exchange }) => ({ code, name, exchange })) ?? [],
  );
  const columnMarkets = useMemo(() => JSON.parse(marketsKey) as MarketColumnInfo[], [marketsKey]);
  const withOpenings = columnMarkets.length > 0;
  const columns = useMemo(() => historyColumns(day, columnMarkets), [day, columnMarkets]);
  const tableColumns = useMemo(() => withFilteredValues(columns, tableFilters), [columns, tableFilters]);

  // Đúng các dòng bảng đang hiển thị (sau bộ lọc Đoán / Thực tế / Kết quả), mọi trang.
  const exportHistory = async () => {
    const rows = applyColumnFilters(prediction?.recent ?? [], columns, tableFilters);
    return { count: await downloadExcel(`du-doan-${symbol}-${queryInterval}`, 'Dự đoán', rows, excelColumns(day, withOpenings)) };
  };

  return (
    <Card
      size="small"
      title={`Dự đoán ${day ? '1 ngày' : 'nến kế tiếp'} · ${symbol} ${queryInterval}`}
      extra={
        <Space>
          <Segmented<Mode>
            size="small"
            value={mode}
            onChange={setMode}
            options={[
              { value: 'candle', label: `Nến kế tiếp (${interval})` },
              { value: 'day', label: '1 ngày' },
            ]}
          />
          <ExportExcelButton onExport={exportHistory} disabled={!prediction} />
        </Space>
      }
      loading={query.isPending}
    >
      {query.isError && <Alert type="error" showIcon title={`Không tải được dự đoán: ${query.error.message}`} />}
      {prediction && (
        <>
          <Row gutter={16} align="middle">
            <Col xs={24} md={8}>
              <Typography.Text type="secondary">
                {day ? 'Ngày' : 'Nến'} mở lúc {formatVnUnix(prediction.nextCandleTime)} sẽ đóng
              </Typography.Text>
              <div style={{ margin: '6px 0' }}>
                <DirectionTag direction={prediction.direction} big />
              </div>
              {prediction.nextCandleOpenings.length > 0 && (
                <div style={{ marginBottom: 6 }}>
                  <Typography.Text type="warning" style={{ fontSize: 12, marginRight: 4 }}>
                    Nến này có sàn mở cửa, giá hay biến động mạnh:
                  </Typography.Text>
                  <OpeningTags openings={prediction.nextCandleOpenings} markets={columnMarkets} />
                </div>
              )}
              <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                so với giá đóng {day ? 'ngày' : 'nến'} trước {fmtPrice(prediction.lastClose)} · điểm {prediction.score.toFixed(4)}
              </Typography.Text>
            </Col>
            <Col xs={12} md={8}>
              <Statistic
                title={`Tỷ lệ đúng (chạy thử ${prediction.backtestPredictions} ${unit})`}
                value={prediction.hitRatePercent.toFixed(1)}
                suffix="%"
                styles={{ content: { color: prediction.hitRatePercent > 50 ? '#16a34a' : '#dc2626' } }}
              />
              <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                {prediction.backtestCorrect}/{prediction.backtestPredictions} lần đúng · đoán bừa ≈ 50%
              </Typography.Text>
            </Col>
            <Col xs={12} md={8}>
              <Statistic title={`Đúng ${recentCounted} ${unit} gần nhất`} value={`${recentHits}/${recentCounted}`} />
            </Col>
          </Row>

          <Typography.Paragraph style={{ margin: '12px 0 0', fontSize: 12 }}>
            <Typography.Text strong>Lý do: </Typography.Text>
            {prediction.reason}
          </Typography.Paragraph>
          <Typography.Paragraph type="secondary" style={{ margin: '4px 0 0', fontSize: 12 }}>
            Tham số: xu hướng TB {prediction.settings.trendPeriod} {unit} × {prediction.settings.trendWeight}, đà {prediction.settings.momentumPeriod}{' '}
            {unit} × {prediction.settings.momentumWeight}, lực mua/bán × {prediction.settings.pressureWeight}. Sửa trong{' '}
            <code>CryptoBot.Domain/Prediction/PriceDirectionPredictor.cs</code> rồi restart API.
          </Typography.Paragraph>
          {withOpenings && <MarketHoursNote markets={prediction.markets} />}

          {/* Mount lại khi đổi symbol/khung/chế độ để phân trang về trang 1 (trang mới nhất). */}
          <Table<PredictionHistory>
            key={tableScope}
            rowKey="time"
            size="small"
            style={{ marginTop: 12 }}
            dataSource={prediction.recent}
            pagination={{ pageSize: 10, hideOnSinglePage: true, size: 'small' }}
            scroll={{ x: 'max-content' }}
            onChange={(_, filters) => setTableFilters(filters)}
            columns={tableColumns}
          />
        </>
      )}
    </Card>
  );
}
