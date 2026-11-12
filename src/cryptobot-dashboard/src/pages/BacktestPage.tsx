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
  Select,
  Space,
  Spin,
  Typography,
  message,
} from 'antd';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  ApiError,
  EMA_CROSS_SIGNAL,
  backtestApi,
  type BacktestDirection,
  type BacktestMode,
  type BacktestRequest,
  type BacktestResult,
  type PositionSizing,
  type Strategy,
} from '../api/client';
import { DIRECTION_LABELS } from '../features/backtest/describe';
import { BacktestResultView } from '../features/backtest/BacktestResultView';
import { backtestQueryKey } from '../features/backtest/useBacktestMutations';
import { backtestDetailHref, pageHref } from '../routes';
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
  mode: BacktestMode;
  /** EMA_CROSS_SIGNAL hoặc loại chỉ báo tự viết (chỉ Futures). */
  signalType: string;
  /** Tham số đã sửa của từng loại chỉ báo (chưa sửa thì dùng gợi ý), đổi qua lại không mất giá trị đã nhập. */
  signalParameters: Record<string, Record<string, string>>;
  direction: BacktestDirection;
  closeOnReverse: boolean;
  /** null cho tới lần đầu chọn Futures: khi đó lấy theo Paper Futures và quy tắc tự động. */
  leverage: number | null;
  slippagePercent: number | null;
  maintenanceMarginPercent: number | null;
}

/** Giá thanh lý cách giá vào bao nhiêu % (giống FuturesBacktestSettings), null nếu thiếu số. */
function liquidationDistance(leverage: number | null, maintenancePercent: number | null) {
  if (!leverage || leverage < 1 || maintenancePercent == null) return null;
  const rate = maintenancePercent / 100;
  return {
    long: (1 - (1 - 1 / leverage) / (1 - rate)) * 100,
    short: ((1 + 1 / leverage) / (1 + rate) - 1) * 100,
  };
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
    mode: 'Spot',
    signalType: EMA_CROSS_SIGNAL,
    signalParameters: {},
    direction: 'Both',
    closeOnReverse: true,
    leverage: null,
    slippagePercent: null,
    maintenanceMarginPercent: null,
  });
  const update = (patch: Partial<Settings>) => setSettings((current) => ({ ...current, ...patch }));
  const futures = settings.mode === 'Futures';

  const options = useQuery({
    queryKey: ['backtest-options', strategy.id],
    queryFn: () => backtestApi.options(strategy.id),
    staleTime: Infinity,
  });
  // Spot và Futures có mức phí mặc định khác nhau (Spot 0.1%, Futures theo phí taker của Paper Futures). Lần đầu chọn
  // Futures lấy đòn bẩy, trượt giá, ký quỹ duy trì theo Paper Futures và quy tắc tự động.
  const switchMode = (mode: BacktestMode) => {
    const defaults = options.data?.futures;
    setSettings((current) => ({
      ...current,
      mode,
      feePercent: mode === 'Futures' ? (defaults?.feePercent ?? 0.05) : 0.1,
      // Spot chỉ chạy chiến lược EMA của bot.
      signalType: mode === 'Spot' ? EMA_CROSS_SIGNAL : current.signalType,
      leverage: current.leverage ?? defaults?.leverage ?? 2,
      slippagePercent: current.slippagePercent ?? defaults?.slippagePercent ?? 0.05,
      maintenanceMarginPercent: current.maintenanceMarginPercent ?? defaults?.maintenanceMarginPercent ?? 0.4,
    }));
  };

  const applyAutoOrderDefaults = () => {
    const defaults = options.data?.futures;
    if (!defaults) return;
    update({
      sizing: 'FixedAmount',
      sizeAmount: defaults.margin,
      leverage: defaults.leverage,
      useStopLoss: true,
      stopLossPercent: defaults.stopLossPercent,
      useTakeProfit: true,
      takeProfitPercent: defaults.takeProfitPercent,
    });
  };

  const indicator = options.data?.indicators.find((choice) => choice.type === settings.signalType);
  const parameters = settings.signalParameters[settings.signalType] ?? indicator?.parameters ?? {};
  const setParameter = (name: string, value: string) =>
    update({ signalParameters: { ...settings.signalParameters, [settings.signalType]: { ...parameters, [name]: value } } });
  const resetParameters = () =>
    update({
      signalParameters: Object.fromEntries(
        Object.entries(settings.signalParameters).filter(([type]) => type !== settings.signalType),
      ),
    });
  const distance = futures ? liquidationDistance(settings.leverage, settings.maintenanceMarginPercent) : null;

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

  // Kết quả lần vừa chạy; các lần trước xem ở tab Lịch sử backtest.
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
      // Trang chi tiết của lần vừa chạy dùng luôn kết quả này, không tải lại.
      queryClient.setQueryData(backtestQueryKey(result.id), result);
      queryClient.invalidateQueries({ queryKey: ['backtests'] });
    },
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
      mode: settings.mode,
      ...(futures
        ? {
            leverage: settings.leverage ?? 1,
            direction: settings.direction,
            closeOnReverse: settings.closeOnReverse,
            slippagePercent: String(settings.slippagePercent ?? 0),
            maintenanceMarginPercent: String(settings.maintenanceMarginPercent ?? 0),
            signalType: settings.signalType,
            ...(settings.signalType === EMA_CROSS_SIGNAL ? {} : { signalParameters: parameters }),
          }
        : {}),
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
          <Form.Item label="Kiểu backtest" style={{ marginBottom: 8 }}>
            <Segmented<BacktestMode>
              value={settings.mode}
              onChange={switchMode}
              options={[
                { label: 'Spot · như bot EMA đang chạy', value: 'Spot' },
                { label: 'Futures · Long/Short như lệnh tự động', value: 'Futures' },
              ]}
            />
          </Form.Item>
          <Typography.Paragraph type="secondary">
            {futures
              ? 'Mở vị thế USDⓈ-M isolated có đòn bẩy giống lệnh tự động Paper Futures: tín hiệu Long mở Long, tín hiệu Short mở ' +
                'Short (theo hướng lệnh đã chọn); phí tính trên giá trị vị thế, có trượt giá, lỗ tối đa bằng margin, chạm giá ' +
                'thanh lý thì mất margin. Không tính funding.'
              : 'Mua bằng tiền mặt rồi bán lại, chỉ Long, không đòn bẩy: đúng chiến lược EMA bot đang chạy (EMA cắt xuống chỉ ' +
                'để bán lệnh đang giữ).'}
          </Typography.Paragraph>
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
                {futures && (
                  <Col span={24}>
                    <Form.Item label="Tín hiệu" tooltip="Chỉ báo tự viết chạy trên nến của chiến lược đang chọn">
                      <Select
                        value={settings.signalType}
                        onChange={(value) => update({ signalType: value })}
                        loading={options.isPending}
                        options={[
                          { value: EMA_CROSS_SIGNAL, label: 'EMA cross · cắt lên → Long, cắt xuống → Short' },
                          ...(options.data?.indicators ?? []).map((choice) => ({
                            value: choice.type,
                            label: `${choice.type}${choice.presetName ? ` · tham số của ${choice.presetName}` : ''}`,
                          })),
                        ]}
                      />
                    </Form.Item>
                  </Col>
                )}
                {settings.signalType === EMA_CROSS_SIGNAL ? (
                  <>
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
                  </>
                ) : (
                  Object.entries(parameters).map(([name, value]) => (
                    <Col key={name} span={12}>
                      <Form.Item label={name}>
                        <Input value={value} onChange={(event) => setParameter(name, event.target.value)} />
                      </Form.Item>
                    </Col>
                  ))
                )}
              </Row>
              <Typography.Text type="secondary">
                {settings.signalType === EMA_CROSS_SIGNAL ? (
                  <>
                    Mặc định là tham số bot đang chạy (EMA{strategy.parameters.fastPeriod}/{strategy.parameters.slowPeriod}, cửa
                    sổ {strategy.parameters.lookbackCandles} nến).
                  </>
                ) : (
                  <>
                    {indicator?.description}{' '}
                    {indicator?.presetName
                      ? `Tham số mặc định lấy từ chỉ báo ${indicator.presetName} đang chạy trên ${strategy.symbol} ${strategy.interval}.`
                      : 'Tham số mặc định của chỉ báo.'}{' '}
                    <Button type="link" size="small" style={{ padding: 0 }} onClick={resetParameters}>
                      Về mặc định
                    </Button>
                  </>
                )}
              </Typography.Text>
              {futures && (
                <Row gutter={8} style={{ marginTop: 16 }}>
                  <Col span={24}>
                    <Form.Item label="Hướng lệnh" style={{ marginBottom: 8 }}>
                      <Segmented<BacktestDirection>
                        value={settings.direction}
                        onChange={(value) => update({ direction: value })}
                        options={(Object.keys(DIRECTION_LABELS) as BacktestDirection[]).map((direction) => ({
                          label: DIRECTION_LABELS[direction],
                          value: direction,
                        }))}
                      />
                    </Form.Item>
                    <Checkbox checked={settings.closeOnReverse} onChange={(event) => update({ closeOnReverse: event.target.checked })}>
                      Đóng lệnh khi có tín hiệu ngược chiều
                    </Checkbox>
                    <Typography.Paragraph type="secondary" style={{ marginTop: 4 }}>
                      {settings.closeOnReverse
                        ? settings.direction === 'Both'
                          ? 'Tín hiệu ngược chiều đóng lệnh đang giữ rồi mở lệnh chiều kia ngay ở cùng giá mở.'
                          : 'Tín hiệu ngược chiều đóng lệnh đang giữ, rồi chờ tín hiệu đúng chiều tiếp theo.'
                        : 'Lệnh chỉ đóng ở chốt lời, cắt lỗ hoặc khi bị thanh lý; tín hiệu ngược chiều bị bỏ qua.'}
                    </Typography.Paragraph>
                  </Col>
                </Row>
              )}
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
                {futures && (
                  <>
                    <Col span={8}>
                      <Form.Item label="Đòn bẩy">
                        <InputNumber
                          min={1}
                          max={options.data?.futures.maxLeverage ?? 125}
                          precision={0}
                          value={settings.leverage}
                          onChange={(value) => update({ leverage: value })}
                          suffix="x"
                          style={{ width: '100%' }}
                        />
                      </Form.Item>
                    </Col>
                    <Col span={8}>
                      <Form.Item label="Trượt giá (%)" tooltip="Mỗi lần khớp lệnh, luôn bất lợi (Paper Futures dùng cùng mức)">
                        <InputNumber min={0} max={5} step={0.01} value={settings.slippagePercent} onChange={(value) => update({ slippagePercent: value })} style={{ width: '100%' }} />
                      </Form.Item>
                    </Col>
                    <Col span={8}>
                      <Form.Item label="Ký quỹ duy trì (%)" tooltip="Để tính giá thanh lý (maintenance margin rate)">
                        <InputNumber min={0} max={5} step={0.1} value={settings.maintenanceMarginPercent} onChange={(value) => update({ maintenanceMarginPercent: value })} style={{ width: '100%' }} />
                      </Form.Item>
                    </Col>
                  </>
                )}
                <Col span={24}>
                  <Form.Item label={futures ? 'Margin mỗi lệnh (giá trị vị thế = margin × đòn bẩy)' : 'Khối lượng mỗi lệnh'}>
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
              {futures && (
                <Typography.Text type="secondary">
                  {distance &&
                    `Giá thanh lý cách giá vào ≈ ${distance.long.toFixed(2)}% (Long) / ${distance.short.toFixed(2)}% (Short); cắt lỗ phải gần hơn mức này. `}
                  TP/SL tính theo chiều lệnh: Short chốt lời khi giá giảm, cắt lỗ khi giá tăng.{' '}
                  {options.data && (
                    <Button type="link" size="small" style={{ padding: 0 }} onClick={applyAutoOrderDefaults}>
                      Dùng mặc định của lệnh tự động ({options.data.futures.margin} USDT · {options.data.futures.leverage}x · SL{' '}
                      {options.data.futures.stopLossPercent}% · TP {options.data.futures.takeProfitPercent}%)
                    </Button>
                  )}
                </Typography.Text>
              )}
            </Col>
          </Row>
          <Flex gap={12} align="center" wrap>
            <Button type="primary" htmlType="submit" loading={run.isPending}>
              Chạy backtest
            </Button>
            <Typography.Text type="secondary">
              {futures
                ? 'Tín hiệu lúc nến đóng → khớp ở giá mở nến kế tiếp. TP/SL/thanh lý xét theo giá cao/thấp của từng nến; nến chạm ' +
                  'cả cắt lỗ và chốt lời thì tính cắt lỗ.'
                : 'Lệnh Long không đòn bẩy. Tín hiệu lúc nến đóng → khớp ở giá mở nến kế tiếp. TP/SL xét theo giá cao/thấp của ' +
                  'từng nến; nến chạm cả hai thì tính cắt lỗ. TP/SL không đổi chuỗi tín hiệu của bot.'}
            </Typography.Text>
          </Flex>
        </Form>
      </Card>

      {run.isError && <Alert type="error" showIcon title={errorText(run.error)} />}

      <Typography.Text type="secondary">
        Mọi lần chạy được lưu tự động: xem lại, so sánh, ghi chú ở tab{' '}
        <a href={pageHref('backtest-history')}>Lịch sử backtest</a>.
      </Typography.Text>

      <div ref={resultRef} style={{ scrollMarginTop: 16 }}>
        {shown && (
          <Space orientation="vertical" size="middle" style={{ width: '100%' }}>
            <Flex justify="end">
              <a href={backtestDetailHref(shown.id)}>Mở trang chi tiết lần chạy #{shown.id} →</a>
            </Flex>
            <BacktestResultView result={shown} />
          </Space>
        )}
      </div>
      {shown && (
        <Alert
          type="info"
          showIcon
          title={`Kết quả trong quá khứ không bảo đảm cho tương lai. Mô phỏng không tính ${
            shown.settings.futures ? 'độ sâu sổ lệnh và funding' : 'trượt giá, độ sâu sổ lệnh và funding'
          }.`}
        />
      )}
    </Space>
  );
}
