import { useState } from 'react';
import {
  Alert,
  Button,
  Card,
  Descriptions,
  Divider,
  Flex,
  InputNumber,
  Modal,
  Segmented,
  Slider,
  Space,
  Statistic,
  Tag,
  Typography,
  message,
} from 'antd';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { ApiError, futuresApi, type FuturesAccount, type FuturesQuote, type OpenRequest, type PositionSide } from '../../api/client';
import { fmtPrice, fmtUsdt, pnlColor } from './format';

const LONG = '#16a34a';
const SHORT = '#dc2626';

interface Props {
  symbol: string;
  account: FuturesAccount | undefined;
}

export function TradePanel({ symbol, account }: Props) {
  const queryClient = useQueryClient();
  const [side, setSide] = useState<PositionSide>('Long');
  const [leverage, setLeverage] = useState(3);
  const [margin, setMargin] = useState<string | null>('100');
  const [stopLoss, setStopLoss] = useState<string | null>(null);
  const [takeProfit, setTakeProfit] = useState<string | null>(null);
  const [pending, setPending] = useState<{ request: OpenRequest; quote: FuturesQuote } | null>(null);

  const mark = account?.markPrices.find((m) => m.symbol === symbol)?.price;
  const maxLeverage = account?.rules.maxLeverage ?? 10;
  const supported = account?.rules.symbols.includes(symbol) ?? false;
  const hasOpen = account?.openPositions.some((p) => p.symbol === symbol) ?? false;
  const color = side === 'Long' ? LONG : SHORT;

  const quote = useMutation({
    mutationFn: futuresApi.quote,
    onSuccess: (q, request) => setPending({ request, quote: q }),
    onError: (e) => message.error(e.message),
  });

  const open = useMutation({
    mutationFn: futuresApi.open,
    onSuccess: (r) => {
      message.success(
        r.duplicate
          ? 'Lệnh này đã được xử lý trước đó.'
          : `Đã mở ${r.position.side} ${r.position.quantity} ${symbol} @ ${fmtPrice(r.position.entryPrice)}`,
      );
      setPending(null);
      queryClient.invalidateQueries({ queryKey: ['futures-account'] });
    },
    onError: (e) => {
      const details = e instanceof ApiError ? e.details : [e.message];
      Modal.error({ title: 'Lệnh bị từ chối', content: details.join('\n') });
    },
  });

  // Gợi ý nhanh SL/TP theo % so với mark price, đúng phía cho Long/Short.
  const suggest = (slPct: number, tpPct: number) => {
    if (!mark) return;
    const dir = side === 'Long' ? 1 : -1;
    setStopLoss((mark * (1 - dir * slPct)).toFixed(2));
    setTakeProfit((mark * (1 + dir * tpPct)).toFixed(2));
  };

  const fillMarginPct = (pct: number) => {
    if (!account) return;
    setMargin(Math.max(0, account.availableBalance * pct * 0.995).toFixed(2));
  };

  const submit = () => {
    if (!margin || !stopLoss) {
      message.warning('Nhập margin và stop loss (bắt buộc).');
      return;
    }
    quote.mutate({
      commandId: crypto.randomUUID(),
      symbol,
      side,
      leverage,
      margin,
      stopLoss,
      takeProfit: takeProfit || null,
    });
  };

  return (
    <Card
      size="small"
      title={
        <Space size={4}>
          <span>Futures</span>
          <Tag color="blue">PAPER</Tag>
        </Space>
      }
      extra={
        <Typography.Text type="secondary" style={{ fontSize: 12 }}>
          {symbol} <Typography.Text strong>{mark ? fmtPrice(mark) : '—'}</Typography.Text>
        </Typography.Text>
      }
    >
      {account && (
        <Flex wrap gap={16} style={{ marginBottom: 12 }}>
          <Statistic title="Khả dụng" value={account.availableBalance} precision={2} valueStyle={{ fontSize: 16 }} />
          <Statistic title="Equity" value={account.equity} precision={2} valueStyle={{ fontSize: 16 }} />
          <Statistic
            title="uPnL"
            value={account.unrealizedPnl}
            precision={2}
            valueStyle={{ fontSize: 16, color: pnlColor(account.unrealizedPnl) }}
          />
        </Flex>
      )}

      {!supported && account && (
        <Alert type="info" showIcon title={`${symbol} chưa được cấu hình cho paper futures.`} style={{ marginBottom: 12 }} />
      )}
      {hasOpen && (
        <Alert type="info" showIcon title="Đang có vị thế trên symbol này (one-way mode)." style={{ marginBottom: 12 }} />
      )}

      <Segmented
        block
        value={side}
        onChange={(v) => setSide(v as PositionSide)}
        options={[
          { label: <span style={{ color: LONG, fontWeight: 600 }}>Long</span>, value: 'Long' },
          { label: <span style={{ color: SHORT, fontWeight: 600 }}>Short</span>, value: 'Short' },
        ]}
      />

      <Divider style={{ margin: '12px 0' }} />

      <Typography.Text>Đòn bẩy: </Typography.Text>
      <Typography.Text strong style={{ color: leverage >= 5 ? SHORT : undefined }}>
        {leverage}x
      </Typography.Text>
      <Slider
        min={1}
        max={maxLeverage}
        value={leverage}
        onChange={setLeverage}
        marks={{ 1: '1x', [Math.ceil(maxLeverage / 2)]: `${Math.ceil(maxLeverage / 2)}x`, [maxLeverage]: `${maxLeverage}x` }}
      />

      <Space orientation="vertical" style={{ width: '100%' }} size="small">
        <InputNumber<string>
          stringMode
          min="0"
          value={margin}
          onChange={setMargin}
          prefix={<Typography.Text type="secondary">Margin</Typography.Text>}
          suffix="USDT"
          style={{ width: '100%' }}
        />
        <Space size={4}>
          {[0.1, 0.25, 0.5].map((p) => (
            <Button key={p} size="small" onClick={() => fillMarginPct(p)}>
              {p * 100}%
            </Button>
          ))}
        </Space>
        <InputNumber<string>
          stringMode
          min="0"
          value={stopLoss}
          onChange={setStopLoss}
          prefix={<Typography.Text type="secondary">Stop loss</Typography.Text>}
          placeholder="bắt buộc"
          style={{ width: '100%' }}
        />
        <InputNumber<string>
          stringMode
          min="0"
          value={takeProfit}
          onChange={setTakeProfit}
          prefix={<Typography.Text type="secondary">Take profit</Typography.Text>}
          placeholder="tùy chọn"
          style={{ width: '100%' }}
        />
        <Space size={4} wrap>
          <Typography.Text type="secondary" style={{ fontSize: 12 }}>
            Điền nhanh:
          </Typography.Text>
          <Button size="small" disabled={!mark} onClick={() => suggest(0.01, 0.02)}>
            SL 1% / TP 2%
          </Button>
          <Button size="small" disabled={!mark} onClick={() => suggest(0.02, 0.04)}>
            SL 2% / TP 4%
          </Button>
        </Space>
      </Space>

      <Button
        block
        type="primary"
        size="large"
        style={{ marginTop: 16, background: color, borderColor: color }}
        loading={quote.isPending}
        disabled={!supported || hasOpen}
        onClick={submit}
      >
        {side === 'Long' ? 'Mở Long' : 'Mở Short'} {leverage}x
      </Button>
      <Typography.Paragraph type="secondary" style={{ fontSize: 12, margin: '8px 0 0' }}>
        Tiền giả lập, isolated margin. Khớp tại mark price Binance Futures ± trượt giá {((account?.rules.slippageRate ?? 0) * 100).toFixed(2)}%,
        phí {((account?.rules.takerFeeRate ?? 0) * 100).toFixed(3)}%/chiều. Chưa mô phỏng funding.
      </Typography.Paragraph>

      <ConfirmModal
        pending={pending}
        loading={open.isPending}
        onCancel={() => setPending(null)}
        onConfirm={() => pending && open.mutate(pending.request)}
      />
    </Card>
  );
}

function ConfirmModal({
  pending,
  loading,
  onCancel,
  onConfirm,
}: {
  pending: { request: OpenRequest; quote: FuturesQuote } | null;
  loading: boolean;
  onCancel: () => void;
  onConfirm: () => void;
}) {
  const q = pending?.quote;
  const r = pending?.request;
  const color = q?.side === 'Long' ? LONG : SHORT;
  const liqDistance =
    q?.fillPrice && q.liquidationPrice ? Math.abs(q.liquidationPrice - q.fillPrice) / q.fillPrice : null;

  return (
    <Modal
      open={!!pending}
      title={
        <Space>
          <Tag color="blue">PAPER</Tag>
          <span style={{ color }}>
            {q?.side} {q?.symbol} {q?.leverage}x
          </span>
        </Space>
      }
      okText={`Xác nhận mở ${q?.side ?? ''}`}
      okButtonProps={{ disabled: !q?.accepted, style: q?.accepted ? { background: color } : undefined }}
      cancelText="Hủy"
      confirmLoading={loading}
      onCancel={onCancel}
      onOk={onConfirm}
      destroyOnHidden
    >
      {q && !q.accepted && (
        <Alert
          type="error"
          showIcon
          title="Không thể mở vị thế"
          description={
            <ul style={{ margin: 0, paddingLeft: 18 }}>
              {q.errors.map((e) => (
                <li key={e}>{e}</li>
              ))}
            </ul>
          }
          style={{ marginBottom: 12 }}
        />
      )}
      {q && (
        <Descriptions size="small" column={1} bordered>
          <Descriptions.Item label="Mark price">{fmtPrice(q.markPrice)}</Descriptions.Item>
          <Descriptions.Item label="Giá vào ước tính">{fmtPrice(q.fillPrice)}</Descriptions.Item>
          <Descriptions.Item label="Khối lượng">{q.quantity ?? '—'}</Descriptions.Item>
          <Descriptions.Item label="Giá trị vị thế">{fmtUsdt(q.notional)}</Descriptions.Item>
          <Descriptions.Item label="Margin">{fmtUsdt(q.margin)}</Descriptions.Item>
          <Descriptions.Item label="Phí mở">{fmtUsdt(q.openFee)}</Descriptions.Item>
          <Descriptions.Item label="Giá thanh lý">
            <Typography.Text strong style={{ color: SHORT }}>
              {fmtPrice(q.liquidationPrice)}
            </Typography.Text>
            {liqDistance != null && (
              <Typography.Text type="secondary"> (cách {(liqDistance * 100).toFixed(2)}%)</Typography.Text>
            )}
          </Descriptions.Item>
          <Descriptions.Item label="Stop loss">{fmtPrice(r ? Number(r.stopLoss) : null)}</Descriptions.Item>
          <Descriptions.Item label="Take profit">{r?.takeProfit ? fmtPrice(Number(r.takeProfit)) : '—'}</Descriptions.Item>
          <Descriptions.Item label="Lỗ ước tính nếu chạm SL">
            <span style={{ color: SHORT }}>{q.lossAtStop != null ? `−${fmtUsdt(q.lossAtStop)}` : '—'}</span>
          </Descriptions.Item>
          <Descriptions.Item label="Lãi ước tính nếu chạm TP">
            <span style={{ color: LONG }}>{fmtUsdt(q.profitAtTakeProfit)}</span>
          </Descriptions.Item>
        </Descriptions>
      )}
      {q?.accepted && (
        <Typography.Paragraph type="secondary" style={{ fontSize: 12, margin: '12px 0 0' }}>
          Ước tính theo mark price hiện tại; giá khớp thực tế lấy tại thời điểm xác nhận. Nếu giá gap qua stop, lỗ có thể
          lớn hơn ước tính (tối đa bằng margin).
        </Typography.Paragraph>
      )}
    </Modal>
  );
}
