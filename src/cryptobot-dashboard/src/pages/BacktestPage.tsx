import { useRef, useState } from 'react';
import {
  Alert,
  Button,
  Card,
  Checkbox,
  Col,
  Flex,
  Form,
  Input,
  InputNumber,
  Row,
  Segmented,
  Space,
  Spin,
  Typography,
  message,
} from 'antd';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  ApiError,
  backtestApi,
  type BacktestRequest,
  type BacktestResult,
  type PositionSizing,
  type Strategy,
} from '../api/client';
import { BacktestHistoryCard } from '../features/backtest/BacktestHistoryCard';
import { BacktestResultView } from '../features/backtest/BacktestResultView';
import { formatVn } from '../time';

/** Ngày `YYYY-MM-DD` trên ô nhập hiểu theo giờ Việt Nam (00:00 hoặc 23:59:59 của ngày đó). */
const vnStartOfDay = (date: string) => (date ? `${date}T00:00:00+07:00` : null);
const vnEndOfDay = (date: string) => (date ? `${date}T23:59:59+07:00` : null);

const HISTORY_PRESETS = ['2018-01-01', '2020-01-01', '2022-01-01', '2024-01-01'];

interface Settings {
  from: string;
  to: string;
  fastPeriod: number | null;
  slowPeriod: number | null;
  lookbackCandles: number | null;
  initialCapital: number | null;
  sizing: PositionSizing;
  sizePercent: number | null;
  sizeAmount: number | null;
  useTakeProfit: boolean;
  takeProfitPercent: number | null;
  useStopLoss: boolean;
  stopLossPercent: number | null;
  feePercent: number | null;
}

const errorText = (error: Error) => (error instanceof ApiError ? error.details.join(' ') || error.message : error.message);

/**
 * Backtest chiến lược EMA đang chọn trên nến lịch sử: gọi đúng hàm ra quyết định của bot cho từng nến, rồi giả lập
 * lệnh với vốn, khối lượng, TP/SL và phí. Chỉ mô phỏng, không đặt lệnh nào.
 */
export function BacktestPage({ strategy }: { strategy: Strategy }) {
  const queryClient = useQueryClient();
  const [historyFrom, setHistoryFrom] = useState('2020-01-01');
  const [settings, setSettings] = useState<Settings>({
    from: '',
    to: '',
    fastPeriod: strategy.parameters.fastPeriod,
    slowPeriod: strategy.parameters.slowPeriod,
    lookbackCandles: strategy.parameters.lookbackCandles,
    initialCapital: 1000,
    sizing: 'PercentOfEquity',
    sizePercent: 10,
    sizeAmount: 100,
    useTakeProfit: true,
    takeProfitPercent: 10,
    useStopLoss: true,
    stopLossPercent: 3,
    feePercent: 0.1,
  });
  const update = (patch: Partial<Settings>) => setSettings((current) => ({ ...current, ...patch }));

  const coverage = useQuery({
    queryKey: ['backtest-coverage', strategy.id],
    queryFn: () => backtestApi.coverage(strategy.id),
  });

  const history = useMutation({
    mutationFn: (from: string) => backtestApi.history(strategy.id, vnStartOfDay(from)!),
    onSuccess: (result) => {
      queryClient.invalidateQueries({ queryKey: ['backtest-coverage', strategy.id] });
      if (result.reachedRequestLimit)
        message.warning(`Đã tải ${result.inserted.toLocaleString('vi-VN')} nến, còn tiếp: bấm tải lại để tải phần còn lại.`, 8);
      else message.success(`Đã tải thêm ${result.inserted.toLocaleString('vi-VN')} nến (${result.requests} request tới Binance).`);
    },
    onError: (error) => message.error(errorText(error), 8),
  });

  // Kết quả đang hiển thị: lần vừa chạy hoặc một lần mở lại từ lịch sử.
  const [shown, setShown] = useState<BacktestResult | null>(null);
  const resultRef = useRef<HTMLDivElement>(null);
  const show = (result: BacktestResult) => {
    setShown(result);
    requestAnimationFrame(() => resultRef.current?.scrollIntoView({ behavior: 'smooth', block: 'start' }));
  };

  const run = useMutation<BacktestResult, Error, BacktestRequest>({
    mutationFn: (request) => backtestApi.run(strategy.id, request),
    onSuccess: (result) => {
      show(result);
      queryClient.invalidateQueries({ queryKey: ['backtests'] });
    },
  });

  const open = useMutation({
    mutationFn: backtestApi.get,
    onSuccess: show,
    onError: (error) => message.error(errorText(error)),
  });

  const submit = () => {
    const size = settings.sizing === 'PercentOfEquity' ? settings.sizePercent : settings.sizeAmount;
    run.mutate({
      fromUtc: vnStartOfDay(settings.from),
      toUtc: vnEndOfDay(settings.to),
      fastPeriod: settings.fastPeriod,
      slowPeriod: settings.slowPeriod,
      lookbackCandles: settings.lookbackCandles,
      initialCapital: String(settings.initialCapital ?? 0),
      sizing: settings.sizing,
      sizeValue: String(size ?? 0),
      takeProfitPercent: settings.useTakeProfit && settings.takeProfitPercent != null ? String(settings.takeProfitPercent) : null,
      stopLossPercent: settings.useStopLoss && settings.stopLossPercent != null ? String(settings.stopLossPercent) : null,
      feePercent: String(settings.feePercent ?? 0),
    });
  };

  return (
    <Space orientation="vertical" size="middle" style={{ width: '100%' }}>
      <Card size="small" title={`Dữ liệu lịch sử · ${strategy.symbol} ${strategy.interval}`}>
        <Space orientation="vertical" style={{ width: '100%' }}>
          {coverage.isPending ? (
            <Spin size="small" />
          ) : coverage.data?.firstOpenTimeUtc ? (
            <Typography.Text>
              Đang có nến từ <b>{formatVn(coverage.data.firstOpenTimeUtc)}</b> đến <b>{formatVn(coverage.data.lastOpenTimeUtc)}</b>.
            </Typography.Text>
          ) : (
            <Typography.Text type="warning">Chưa có nến nào cho cặp/khung nến này.</Typography.Text>
          )}
          <Flex wrap gap={8} align="center">
            <Typography.Text>Tải thêm nến cũ từ ngày</Typography.Text>
            <Input type="date" value={historyFrom} onChange={(event) => setHistoryFrom(event.target.value)} style={{ width: 160 }} />
            {HISTORY_PRESETS.map((date) => (
              <Button key={date} size="small" onClick={() => setHistoryFrom(date)}>
                {date.slice(0, 4)}
              </Button>
            ))}
            <Button type="primary" ghost loading={history.isPending} disabled={!historyFrom} onClick={() => history.mutate(historyFrom)}>
              Tải lịch sử từ Binance
            </Button>
          </Flex>
          <Typography.Text type="secondary">
            Chỉ cần tải một lần; nến được lưu vào database của bot. Nến 1h từ 2020 mất khoảng 15–30 giây, nến 15m lâu hơn khoảng 4 lần.
          </Typography.Text>
        </Space>
      </Card>

      <Card size="small" title="Thiết lập backtest">
        <Form layout="vertical" onFinish={submit}>
          <Row gutter={16}>
            <Col xs={24} md={12}>
              <Typography.Title level={5}>Khoảng thời gian và tín hiệu</Typography.Title>
              <Row gutter={8}>
                <Col span={12}>
                  <Form.Item label="Từ ngày" tooltip="Để trống = từ nến sớm nhất đang có">
                    <Input type="date" value={settings.from} onChange={(event) => update({ from: event.target.value })} />
                  </Form.Item>
                </Col>
                <Col span={12}>
                  <Form.Item label="Đến ngày" tooltip="Để trống = đến nến đóng mới nhất">
                    <Input type="date" value={settings.to} onChange={(event) => update({ to: event.target.value })} />
                  </Form.Item>
                </Col>
                <Col span={8}>
                  <Form.Item label="EMA nhanh">
                    <InputNumber min={1} value={settings.fastPeriod} onChange={(value) => update({ fastPeriod: value })} style={{ width: '100%' }} />
                  </Form.Item>
                </Col>
                <Col span={8}>
                  <Form.Item label="EMA chậm">
                    <InputNumber min={2} value={settings.slowPeriod} onChange={(value) => update({ slowPeriod: value })} style={{ width: '100%' }} />
                  </Form.Item>
                </Col>
                <Col span={8}>
                  <Form.Item label="Cửa sổ nến" tooltip="LookbackCandles: số nến dùng để tính EMA mỗi lần, giống bot">
                    <InputNumber min={3} value={settings.lookbackCandles} onChange={(value) => update({ lookbackCandles: value })} style={{ width: '100%' }} />
                  </Form.Item>
                </Col>
              </Row>
              <Typography.Text type="secondary">
                Mặc định là tham số bot đang chạy (EMA{strategy.parameters.fastPeriod}/{strategy.parameters.slowPeriod}, cửa sổ{' '}
                {strategy.parameters.lookbackCandles} nến).
              </Typography.Text>
            </Col>
            <Col xs={24} md={12}>
              <Typography.Title level={5}>Quản lý vốn và rủi ro</Typography.Title>
              <Row gutter={8}>
                <Col span={12}>
                  <Form.Item label="Vốn ban đầu (USDT)">
                    <InputNumber min={1} value={settings.initialCapital} onChange={(value) => update({ initialCapital: value })} style={{ width: '100%' }} />
                  </Form.Item>
                </Col>
                <Col span={12}>
                  <Form.Item label="Phí mỗi lần khớp (%)">
                    <InputNumber min={0} max={10} step={0.01} value={settings.feePercent} onChange={(value) => update({ feePercent: value })} style={{ width: '100%' }} />
                  </Form.Item>
                </Col>
                <Col span={24}>
                  <Form.Item label="Khối lượng mỗi lệnh">
                    <Space.Compact style={{ width: '100%' }}>
                      <Segmented<PositionSizing>
                        value={settings.sizing}
                        onChange={(value) => update({ sizing: value })}
                        options={[
                          { label: '% tổng vốn', value: 'PercentOfEquity' },
                          { label: 'USDT cố định', value: 'FixedAmount' },
                        ]}
                      />
                      {settings.sizing === 'PercentOfEquity' ? (
                        <InputNumber min={0.1} max={100} value={settings.sizePercent} onChange={(value) => update({ sizePercent: value })} suffix="%" style={{ width: 140 }} />
                      ) : (
                        <InputNumber min={1} value={settings.sizeAmount} onChange={(value) => update({ sizeAmount: value })} suffix="USDT" style={{ width: 140 }} />
                      )}
                    </Space.Compact>
                  </Form.Item>
                </Col>
                <Col span={12}>
                  <Form.Item label={<Checkbox checked={settings.useTakeProfit} onChange={(event) => update({ useTakeProfit: event.target.checked })}>Chốt lời (%)</Checkbox>}>
                    <InputNumber min={0.1} step={0.5} disabled={!settings.useTakeProfit} value={settings.takeProfitPercent} onChange={(value) => update({ takeProfitPercent: value })} style={{ width: '100%' }} />
                  </Form.Item>
                </Col>
                <Col span={12}>
                  <Form.Item label={<Checkbox checked={settings.useStopLoss} onChange={(event) => update({ useStopLoss: event.target.checked })}>Cắt lỗ (%)</Checkbox>}>
                    <InputNumber min={0.1} max={99} step={0.5} disabled={!settings.useStopLoss} value={settings.stopLossPercent} onChange={(value) => update({ stopLossPercent: value })} style={{ width: '100%' }} />
                  </Form.Item>
                </Col>
              </Row>
            </Col>
          </Row>
          <Flex gap={12} align="center" wrap>
            <Button type="primary" htmlType="submit" loading={run.isPending}>
              Chạy backtest
            </Button>
            <Typography.Text type="secondary">
              Lệnh Long không đòn bẩy. Tín hiệu lúc nến đóng → khớp ở giá mở nến kế tiếp. TP/SL xét theo giá cao/thấp của
              từng nến; nến chạm cả hai thì tính cắt lỗ. TP/SL không đổi chuỗi tín hiệu của bot.
            </Typography.Text>
          </Flex>
        </Form>
      </Card>

      {run.isError && <Alert type="error" showIcon title={errorText(run.error)} />}

      <BacktestHistoryCard strategyId={strategy.id} openedId={shown?.id ?? null} onOpen={(id) => open.mutate(id)} />

      <div ref={resultRef} style={{ scrollMarginTop: 16 }}>
        {open.isPending && <Spin />}
        {shown && (
          <Space orientation="vertical" size="middle" style={{ width: '100%' }}>
            <BacktestResultView result={shown} />
          </Space>
        )}
      </div>
      {shown && (
        <Alert
          type="info"
          showIcon
          title="Kết quả trong quá khứ không bảo đảm cho tương lai. Mô phỏng không tính trượt giá, độ sâu sổ lệnh và funding."
        />
      )}
    </Space>
  );
}
