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

const condText = (c: TriggerCondition | null) => (c === 'AtOrBelow' ? '≤' : '≥');

function describe(r: CreateAutoRule) {
  const tp = r.takeProfitPercent ? `, TP +${r.takeProfitPercent}%` : '';
  const when =
    r.triggerType === 'Indicator'
      ? `Khi chỉ báo ${r.indicatorName} báo ${r.side} trên một nến đóng sau lúc này`
      : `Khi mark price ${condText(r.condition)} ${r.triggerPrice}`;
  const close = r.closeOnReverse
    ? ` Tự đóng khi ${r.indicatorName} báo ${r.side === 'Long' ? 'Short' : 'Long'} trên nến đóng sau lúc mở lệnh.`
    : '';
  return `${when}, tự mở ${r.side} ${r.symbol} ${r.leverage}x, ` +
    `margin ${r.margin} USDT, SL ${r.stopLossPercent}%${tp}.${close} Chỉ chạy một lần, chỉ tiền giả lập (Paper).`;
}

export function AutoOrdersCard({ symbol, account }: { symbol: string; account: FuturesAccount | undefined }) {
  const queryClient = useQueryClient();
  const summary = useQuery({ queryKey: ['auto-rules'], queryFn: autoApi.summary, refetchInterval: 3_000 });
  const d = summary.data?.defaults;

  const indicators = useQuery({ queryKey: ['indicators'], queryFn: indicatorApi.list, staleTime: 60_000 });
  const symbolIndicators = (indicators.data?.indicators ?? []).filter((i) => i.symbol === symbol);

  const [triggerType, setTriggerType] = useState<RuleTriggerType>('Price');
  const [pickedIndicator, setPickedIndicator] = useState<string | null>(null);
  const indicatorName =
    pickedIndicator && symbolIndicators.some((i) => i.name === pickedIndicator)
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

  const mark = account?.markPrices.find((m) => m.symbol === symbol)?.price;
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
    onError: (e) => {
      const details = e instanceof ApiError ? e.details : [e.message];
      Modal.error({ title: 'Không thể tạo quy tắc', content: details.join('\n') });
    },
  });

  const cancel = useMutation({
    mutationFn: autoApi.cancel,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['auto-rules'] }),
    onError: (e) => message.error(e.message),
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
      margin: margin ?? String(d?.margin ?? 50),
      leverage: leverage ?? d?.leverage ?? 2,
      stopLossPercent: sl ?? String(d?.stopLossPercent ?? 1),
      takeProfitPercent: tp === '' ? null : (tp ?? String(d?.takeProfitPercent ?? 2)),
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
            d?.dailyLossLimitPercent
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
              options={symbolIndicators.map((i) => ({
                value: i.name,
                label: `${i.name} · ${i.type} · ${i.interval}`,
              }))}
            />
            <Checkbox checked={closeOnReverse} onChange={(e) => setCloseOnReverse(e.target.checked)}>
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
          onChange={(v) => setSide(v as PositionSide)}
          options={[
            { label: <span style={{ color: '#16a34a', fontWeight: 600 }}>Long</span>, value: 'Long' },
            { label: <span style={{ color: '#dc2626', fontWeight: 600 }}>Short</span>, value: 'Short' },
          ]}
        />

        <Space wrap size="small">
          <InputNumber<string>
            stringMode
            min="0"
            value={margin ?? String(d?.margin ?? '')}
            onChange={setMargin}
            prefix="Margin"
            suffix="USDT"
            style={{ width: 160 }}
          />
          <InputNumber
            min={1}
            max={account?.rules.maxLeverage ?? 10}
            value={leverage ?? d?.leverage}
            onChange={setLeverage}
            suffix="x"
            style={{ width: 90 }}
          />
          <InputNumber<string>
            stringMode
            min="0"
            value={sl ?? String(d?.stopLossPercent ?? '')}
            onChange={setSl}
            prefix="SL"
            suffix="%"
            style={{ width: 110 }}
          />
          <InputNumber<string>
            stringMode
            min="0"
            value={tp ?? String(d?.takeProfitPercent ?? '')}
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
        {d?.maxActiveRules ?? 10} quy tắc, một vị thế mỗi symbol, dừng khi lỗ trong ngày quá {d?.dailyLossLimitPercent ?? 5}%.
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
            ...columnFilter<AutoRule>((r) => r.side, [
              { text: 'Long', value: 'Long' },
              { text: 'Short', value: 'Short' },
            ]),
            render: (_, r) => (
              <span>
                {r.triggerType === 'Indicator' ? (
                  <>
                    {r.symbol} <Tag color="purple">{r.indicatorName}</Tag>
                    {r.closeOnReverse && <Tag color="geekblue">Tự đóng khi đảo chiều</Tag>}
                  </>
                ) : (
                  <>
                    {r.symbol} {condText(r.condition)} {fmtPrice(r.triggerPrice)}
                  </>
                )}{' '}
                →{' '}
                <Tag color={r.side === 'Long' ? 'green' : 'red'}>
                  {r.side} {r.leverage}x
                </Tag>
              </span>
            ),
          },
          {
            title: 'Trạng thái',
            key: 'status',
            ...columnFilter<AutoRule>((r) => r.status, STATUSES),
            render: (_, r) => (
              <Space orientation="vertical" size={0}>
                <Tag color={statusTag[r.status].color}>{statusTag[r.status].text}</Tag>
                {r.note && (
                  <Typography.Text type={r.status === 'Rejected' ? 'danger' : 'warning'} style={{ fontSize: 12 }}>
                    {r.note}
                  </Typography.Text>
                )}
                {r.triggerDetail && (
                  <Typography.Text type="secondary" style={{ fontSize: 12, maxWidth: 260 }}>
                    {r.triggerDetail}
                  </Typography.Text>
                )}
                {r.triggeredAtUtc && (
                  <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                    {formatVn(r.triggeredAtUtc)} @ {fmtPrice(r.triggeredMarkPrice)}
                  </Typography.Text>
                )}
                {r.status === 'Triggered' && r.closeOnReverse && (
                  <Typography.Text type="secondary" style={{ fontSize: 12, maxWidth: 260 }}>
                    {r.closeWatchEndedAtUtc
                      ? `Đóng: ${r.closeDetail} (${formatVn(r.closeWatchEndedAtUtc)})`
                      : 'Đang chờ chỉ báo đảo chiều để tự đóng…'}
                  </Typography.Text>
                )}
              </Space>
            ),
          },
          {
            title: '',
            key: 'cancel',
            render: (_, r) =>
              r.status === 'Active' ? (
                <Popconfirm title="Hủy quy tắc này?" okText="Hủy quy tắc" cancelText="Giữ" onConfirm={() => cancel.mutate(r.id)}>
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
