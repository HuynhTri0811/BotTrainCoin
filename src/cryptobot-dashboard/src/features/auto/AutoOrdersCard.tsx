import { useState } from 'react';
import { Alert, Button, Card, Checkbox, InputNumber, Modal, Popconfirm, Segmented, Select, Space, Table, Tag, Typography, message } from 'antd';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  ApiError,
  autoApi,
  indicatorApi,
  type AutoRule,
  type CreateAutoRule,
  type FuturesAccount,
  type PositionSide,
  type RuleTriggerType,
  type TriggerCondition,
} from '../../api/client';
import { columnFilter } from '../../components/tableFilters';
import { formatVn } from '../../time';
import { fmtPrice, fmtUsdt } from '../futures/format';

const statusTag: Record<AutoRule['status'], { text: string; color: string }> = {
  Active: { text: 'Đang chờ', color: 'processing' },
  Triggered: { text: 'Đã kích hoạt', color: 'success' },
  Rejected: { text: 'Bị từ chối', color: 'error' },
  Cancelled: { text: 'Đã hủy', color: 'default' },
};

const STATUSES = Object.entries(statusTag).map(([value, { text }]) => ({ text, value }));

const condText = (coin: TriggerCondition | null) => (coin === 'AtOrBelow' ? '≤' : '≥');

function describe(result: CreateAutoRule) {
  const tp = result.takeProfitPercent ? `, TP +${result.takeProfitPercent}%` : '';
  const when =
    result.triggerType === 'Indicator'
      ? `Khi chỉ báo ${result.indicatorName} báo ${result.side} trên một nến đóng sau lúc này`
      : `Khi mark price ${condText(result.condition)} ${result.triggerPrice}`;
  const close = result.closeOnReverse
    ? ` Tự đóng khi ${result.indicatorName} báo ${result.side === 'Long' ? 'Short' : 'Long'} trên nến đóng sau lúc mở lệnh.`
    : '';
  return `${when}, tự mở ${result.side} ${result.symbol} ${result.leverage}x, ` +
    `margin ${result.margin} USDT, SL ${result.stopLossPercent}%${tp}.${close} Chỉ chạy một lần, chỉ tiền giả lập (Paper).`;
}

export function AutoOrdersCard({ symbol, account }: { symbol: string; account: FuturesAccount | undefined }) {
  const queryClient = useQueryClient();
  const summary = useQuery({ queryKey: ['auto-rules'], queryFn: autoApi.summary, refetchInterval: 3_000 });
  const data = summary.data?.defaults;

  const indicators = useQuery({ queryKey: ['indicators'], queryFn: indicatorApi.list, staleTime: 60_000 });
  const symbolIndicators = (indicators.data?.indicators ?? []).filter((indicator) => indicator.symbol === symbol);

  const [triggerType, setTriggerType] = useState<RuleTriggerType>('Price');
  const [pickedIndicator, setPickedIndicator] = useState<string | null>(null);
  const indicatorName =
    pickedIndicator && symbolIndicators.some((indicator) => indicator.name === pickedIndicator)
      ? pickedIndicator
      : (symbolIndicators[0]?.name ?? null);
  const [closeOnReverse, setCloseOnReverse] = useState(false);
  const [side, setSide] = useState<PositionSide>('Long');
  const [condition, setCondition] = useState<TriggerCondition>('AtOrBelow');
  const [trigger, setTrigger] = useState<string | null>(null);
  const [margin, setMargin] = useState<string | null>(null);
  const [leverage, setLeverage] = useState<number | null>(null);
  const [sl, setSl] = useState<string | null>(null);
  const [tp, setTp] = useState<string | null>(null);

  const mark = account?.markPrices.find((markPrice) => markPrice.symbol === symbol)?.price;
  const supported = account?.rules.symbols.includes(symbol) ?? false;

  const create = useMutation({
    mutationFn: autoApi.create,
    onSuccess: () => {
      message.success(
        triggerType === 'Indicator'
          ? 'Đã tạo quy tắc. Bot kiểm tra chỉ báo mỗi khi có nến mới đóng.'
          : 'Đã tạo quy tắc. Bot sẽ theo dõi mark price mỗi 2 giây.',
      );
      setTrigger(null);
      queryClient.invalidateQueries({ queryKey: ['auto-rules'] });
    },
    onError: (error) => {
      const details = error instanceof ApiError ? error.details : [error.message];
      Modal.error({ title: 'Không thể tạo quy tắc', content: details.join('\n') });
    },
  });

  const cancel = useMutation({
    mutationFn: autoApi.cancel,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['auto-rules'] }),
    onError: (error) => message.error(error.message),
  });

  const submit = () => {
    const byIndicator = triggerType === 'Indicator';
    if (byIndicator && !indicatorName) {
      message.warning(`Chưa có chỉ báo nào cho ${symbol}. Khai báo trong appsettings.json → Indicators.`);
      return;
    }
    if (!byIndicator && !trigger) {
      message.warning('Nhập giá kích hoạt.');
      return;
    }
    const req: CreateAutoRule = {
      symbol,
      side,
      triggerType,
      condition: byIndicator ? null : condition,
      triggerPrice: byIndicator ? null : trigger,
      indicatorName: byIndicator ? indicatorName : null,
      closeOnReverse: byIndicator && closeOnReverse,
      margin: margin ?? String(data?.margin ?? 50),
      leverage: leverage ?? data?.leverage ?? 2,
      stopLossPercent: sl ?? String(data?.stopLossPercent ?? 1),
      takeProfitPercent: tp === '' ? null : (tp ?? String(data?.takeProfitPercent ?? 2)),
    };
    Modal.confirm({
      title: 'Bật quy tắc tự mở lệnh?',
      content: describe(req),
      okText: 'Bật quy tắc',
      cancelText: 'Hủy',
      onOk: () => create.mutateAsync(req).catch(() => undefined),
    });
  };

  const rules = summary.data?.rules ?? [];
  const risk = summary.data?.risk;

  return (
    <Card
      size="small"
      title={
        <Space size={4}>
          <span>Tự mở / đóng lệnh theo giá / chỉ báo</span>
          <Tag color="blue">PAPER</Tag>
        </Space>
      }
    >
      {risk?.blocked && (
        <Alert
          type="error"
          showIcon
          style={{ marginBottom: 8 }}
          title="Đã chạm giới hạn lỗ trong ngày"
          description={`Lỗ hôm nay ${fmtUsdt(risk.todayRealizedPnl)}, giới hạn −${fmtUsdt(risk.lossLimit)} (${
            data?.dailyLossLimitPercent
          }% vốn đầu ngày). Quy tắc mới sẽ không kích hoạt cho đến 00:00 UTC.`}
        />
      )}

      <Typography.Text type="secondary" style={{ fontSize: 12 }}>
        {symbol} mark hiện tại <Typography.Text strong>{mark ? fmtPrice(mark) : '—'}</Typography.Text>
      </Typography.Text>

      <Space orientation="vertical" size="small" style={{ width: '100%', marginTop: 4 }}>
        <Segmented<RuleTriggerType>
          block
          value={triggerType}
          onChange={setTriggerType}
          options={[
            { label: 'Theo giá', value: 'Price' },
            { label: 'Theo chỉ báo', value: 'Indicator' },
          ]}
        />

        {triggerType === 'Indicator' ? (
          <>
            <Select<string>
              value={indicatorName ?? undefined}
              onChange={setPickedIndicator}
              placeholder={`Chưa có chỉ báo cho ${symbol}`}
              disabled={symbolIndicators.length === 0}
              style={{ width: '100%' }}
              options={symbolIndicators.map((indicator) => ({
                value: indicator.name,
                label: `${indicator.name} · ${indicator.type} · ${indicator.interval}`,
              }))}
            />
            <Checkbox checked={closeOnReverse} onChange={(error) => setCloseOnReverse(error.target.checked)}>
              Tự đóng khi chỉ báo đảo chiều
            </Checkbox>
          </>
        ) : (
          <Space.Compact block>
            <Select<TriggerCondition>
              value={condition}
              onChange={setCondition}
              style={{ width: 150 }}
              options={[
                { value: 'AtOrBelow', label: 'Giá ≤ (mua khi giảm)' },
                { value: 'AtOrAbove', label: 'Giá ≥ (khi tăng)' },
              ]}
            />
            <InputNumber<string>
              stringMode
              min="0"
              value={trigger}
              onChange={setTrigger}
              placeholder="giá kích hoạt"
              style={{ width: '100%' }}
            />
          </Space.Compact>
        )}

        <Segmented
          block
          value={side}
          onChange={(value) => setSide(value as PositionSide)}
          options={[
            { label: <span style={{ color: '#16a34a', fontWeight: 600 }}>Long</span>, value: 'Long' },
            { label: <span style={{ color: '#dc2626', fontWeight: 600 }}>Short</span>, value: 'Short' },
          ]}
        />

        <Space wrap size="small">
          <InputNumber<string>
            stringMode
            min="0"
            value={margin ?? String(data?.margin ?? '')}
            onChange={setMargin}
            prefix="Margin"
            suffix="USDT"
            style={{ width: 160 }}
          />
          <InputNumber
            min={1}
            max={account?.rules.maxLeverage ?? 10}
            value={leverage ?? data?.leverage}
            onChange={setLeverage}
            suffix="x"
            style={{ width: 90 }}
          />
          <InputNumber<string>
            stringMode
            min="0"
            value={sl ?? String(data?.stopLossPercent ?? '')}
            onChange={setSl}
            prefix="SL"
            suffix="%"
            style={{ width: 110 }}
          />
          <InputNumber<string>
            stringMode
            min="0"
            value={tp ?? String(data?.takeProfitPercent ?? '')}
            onChange={setTp}
            prefix="TP"
            suffix="%"
            style={{ width: 110 }}
          />
        </Space>

        <Button type="primary" block loading={create.isPending} disabled={!supported} onClick={submit}>
          Tạo quy tắc
        </Button>
      </Space>

      <Typography.Paragraph type="secondary" style={{ fontSize: 12, margin: '8px 0' }}>
        Chạy một lần khi mark price chạm mức, hoặc khi chỉ báo phát tín hiệu cùng hướng (Long/Short) trên nến
        đóng sau lúc tạo quy tắc. Quy tắc theo chỉ báo có thể tự đóng vị thế khi chỉ báo báo ngược hướng.
        SL/TP tính theo % so với mark lúc kích hoạt. Mặc định thận trọng: tối đa{' '}
        {data?.maxActiveRules ?? 10} quy tắc, một vị thế mỗi symbol, dừng khi lỗ trong ngày quá {data?.dailyLossLimitPercent ?? 5}%.
      </Typography.Paragraph>

      <Table<AutoRule>
        rowKey="id"
        size="small"
        loading={summary.isPending}
        dataSource={rules}
        pagination={{ pageSize: 5, hideOnSinglePage: true, size: 'small' }}
        scroll={{ x: 'max-content' }}
        locale={{ emptyText: 'Chưa có quy tắc' }}
        columns={[
          {
            title: 'Quy tắc',
            key: 'rule',
            ...columnFilter<AutoRule>((rule) => rule.side, [
              { text: 'Long', value: 'Long' },
              { text: 'Short', value: 'Short' },
            ]),
            render: (_, rule) => (
              <span>
                {rule.triggerType === 'Indicator' ? (
                  <>
                    {rule.symbol} <Tag color="purple">{rule.indicatorName}</Tag>
                    {rule.closeOnReverse && <Tag color="geekblue">Tự đóng khi đảo chiều</Tag>}
                  </>
                ) : (
                  <>
                    {rule.symbol} {condText(rule.condition)} {fmtPrice(rule.triggerPrice)}
                  </>
                )}{' '}
                →{' '}
                <Tag color={rule.side === 'Long' ? 'green' : 'red'}>
                  {rule.side} {rule.leverage}x
                </Tag>
              </span>
            ),
          },
          {
            title: 'Trạng thái',
            key: 'status',
            ...columnFilter<AutoRule>((rule) => rule.status, STATUSES),
            render: (_, rule) => (
              <Space orientation="vertical" size={0}>
                <Tag color={statusTag[rule.status].color}>{statusTag[rule.status].text}</Tag>
                {rule.note && (
                  <Typography.Text type={rule.status === 'Rejected' ? 'danger' : 'warning'} style={{ fontSize: 12 }}>
                    {rule.note}
                  </Typography.Text>
                )}
                {rule.triggerDetail && (
                  <Typography.Text type="secondary" style={{ fontSize: 12, maxWidth: 260 }}>
                    {rule.triggerDetail}
                  </Typography.Text>
                )}
                {rule.triggeredAtUtc && (
                  <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                    {formatVn(rule.triggeredAtUtc)} @ {fmtPrice(rule.triggeredMarkPrice)}
                  </Typography.Text>
                )}
                {rule.status === 'Triggered' && rule.closeOnReverse && (
                  <Typography.Text type="secondary" style={{ fontSize: 12, maxWidth: 260 }}>
                    {rule.closeWatchEndedAtUtc
                      ? `Đóng: ${rule.closeDetail} (${formatVn(rule.closeWatchEndedAtUtc)})`
                      : 'Đang chờ chỉ báo đảo chiều để tự đóng…'}
                  </Typography.Text>
                )}
              </Space>
            ),
          },
          {
            title: '',
            key: 'cancel',
            render: (_, rule) =>
              rule.status === 'Active' ? (
                <Popconfirm title="Hủy quy tắc này?" okText="Hủy quy tắc" cancelText="Giữ" onConfirm={() => cancel.mutate(rule.id)}>
                  <Button size="small" danger>
                    Hủy
                  </Button>
                </Popconfirm>
              ) : null,
          },
        ]}
      />
    </Card>
  );
}
