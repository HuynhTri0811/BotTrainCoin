using CryptoBot.Application.Abstractions;
using CryptoBot.Domain.Futures;
using Microsoft.Extensions.Logging;

namespace CryptoBot.Application.Futures;

/// <summary>
/// Sàn Futures giả lập (isolated, one-way, lệnh market). Khớp tại mark price ± slippage bất lợi.
/// Giới hạn mô hình: không có order book, partial fill, funding, bậc maintenance margin hay insurance fund.
/// </summary>
public sealed class PaperFuturesService(
    IPaperFuturesStore store,
    MarkPriceCache marks,
    PaperFuturesLock gate,
    PaperFuturesOptions options,
    IClock clock,
    ILogger<PaperFuturesService> logger)
{
    public async Task<OpenQuote> QuoteAsync(OpenPositionRequest request, CancellationToken ct)
    {
        var (_, available) = await BalancesAsync(ct);
        var open = await store.GetOpenPositionsAsync(ct);
        return BuildQuote(request, available, open);
    }

    public async Task<OpenResult> OpenAsync(OpenPositionRequest request, CancellationToken ct)
    {
        await gate.Semaphore.WaitAsync(ct);
        try
        {
            // Gửi lại cùng CommandId (double-click, retry mạng) trả về kết quả cũ, không mở vị thế thứ hai.
            if (await store.FindByOpenCommandAsync(request.CommandId, ct) is { } existing)
                return new OpenResult(QuoteFromPosition(existing), existing, Duplicate: true);

            await EnsureFundedAsync(ct);
            var (_, available) = await BalancesAsync(ct);
            var quote = BuildQuote(request, available, await store.GetOpenPositionsAsync(ct));
            if (!quote.Accepted)
                return new OpenResult(quote, null, false);

            var now = clock.UtcNow;
            var position = new FuturesPosition(request.CommandId, request.Symbol, request.Side, request.Leverage,
                quote.Quantity!.Value, quote.FillPrice!.Value, quote.Margin!.Value, quote.LiquidationPrice!.Value,
                request.StopLoss, request.TakeProfit, quote.OpenFee!.Value, now);
            store.AddPosition(position);
            store.AddLedger(new PaperLedgerEntry(-position.OpenFee, LedgerEntryType.Fee, position, "Phí mở vị thế", now));
            await store.SaveAsync(ct);

            logger.LogInformation(
                "Paper futures OPEN {Side} {Symbol} x{Leverage} qty {Qty} @ {Entry} margin {Margin} liq {Liq} SL {Stop} TP {Tp}",
                position.Side, position.Symbol, position.Leverage, position.Quantity, position.EntryPrice,
                position.Margin, position.LiquidationPrice, position.StopLoss, position.TakeProfit);
            return new OpenResult(quote, position, false);
        }
        finally
        {
            gate.Semaphore.Release();
        }
    }

    public Task<CloseResult> CloseAsync(long positionId, Guid commandId, CancellationToken ct) =>
        CloseAsync(positionId, commandId, CloseReason.Manual, ct);

    /// <summary>Đóng vị thế theo giá thị trường. Gửi lại cùng <paramref name="commandId"/> không đóng hai lần.</summary>
    public async Task<CloseResult> CloseAsync(long positionId, Guid commandId, CloseReason reason, CancellationToken ct)
    {
        await gate.Semaphore.WaitAsync(ct);
        try
        {
            var position = await store.GetAsync(positionId, ct);
            if (position is null)
                return new CloseResult(false, "Không tìm thấy vị thế.", null);
            if (position.Status != PositionStatus.Open)
            {
                return position.CloseCommandId == commandId
                    ? new CloseResult(true, null, position)
                    : new CloseResult(false, $"Vị thế đã {position.Status} ({position.CloseReason}).", position);
            }

            if (marks.GetFresh(position.Symbol) is not { } mark)
                return new CloseResult(false, "Chưa có mark price mới; không thể ước lượng giá đóng.", position);

            CloseAt(position, reason, mark.Price, commandId);
            await store.SaveAsync(ct);
            return new CloseResult(true, null, position);
        }
        finally
        {
            gate.Semaphore.Release();
        }
    }

    /// <summary>Kiểm tra thanh lý, stop loss, take profit theo mark price mới. Thanh lý được xét trước (bảo thủ).</summary>
    public async Task<int> EvaluateTriggersAsync(CancellationToken ct)
    {
        await gate.Semaphore.WaitAsync(ct);
        try
        {
            var triggered = 0;
            foreach (var p in await store.GetOpenPositionsAsync(ct))
            {
                if (marks.GetFresh(p.Symbol) is not { } mark)
                    continue;

                if (FuturesMath.IsLiquidated(p.Side, mark.Price, p.LiquidationPrice))
                {
                    p.Liquidate(mark.Price, clock.UtcNow);
                    store.AddLedger(new PaperLedgerEntry(-p.Margin, LedgerEntryType.RealizedPnl, p, "Bị thanh lý", clock.UtcNow));
                    logger.LogWarning("Paper futures LIQUIDATED {Side} {Symbol} mark {Mark} liq {Liq} mất {Margin}",
                        p.Side, p.Symbol, mark.Price, p.LiquidationPrice, p.Margin);
                }
                else if (FuturesMath.StopHit(p.Side, mark.Price, p.StopLoss))
                {
                    CloseAt(p, CloseReason.StopLoss, mark.Price, null);
                }
                else if (p.TakeProfit is { } tp && FuturesMath.TakeProfitHit(p.Side, mark.Price, tp))
                {
                    CloseAt(p, CloseReason.TakeProfit, mark.Price, null);
                }
                else
                {
                    continue;
                }

                triggered++;
            }

            if (triggered > 0)
                await store.SaveAsync(ct);
            return triggered;
        }
        finally
        {
            gate.Semaphore.Release();
        }
    }

    public async Task<AccountSnapshot> GetAccountAsync(CancellationToken ct)
    {
        var (wallet, available) = await BalancesAsync(ct);
        var open = await store.GetOpenPositionsAsync(ct);

        var views = open.Select(p =>
        {
            var mark = marks.Get(p.Symbol)?.Price;
            var upnl = mark is { } m ? FuturesMath.UnrealizedPnl(p.Side, p.Quantity, p.EntryPrice, m) : (decimal?)null;
            return new PositionView(p, mark, upnl, upnl is { } u && p.Margin > 0 ? u / p.Margin * 100 : null);
        }).ToList();

        var unrealized = views.Sum(v => v.UnrealizedPnl ?? 0);
        var used = open.Sum(p => p.Margin);
        var prices = options.Symbols.Select(s => marks.Get(s.Symbol)).OfType<MarkPrice>().ToList();
        return new AccountSnapshot(wallet, used, unrealized, wallet + unrealized, available, views, prices);
    }

    /// <summary>Nạp vốn giả lập lần đầu.</summary>
    public async Task EnsureFundedAsync(CancellationToken ct)
    {
        if ((await store.GetLedgerAsync(ct)).Count > 0)
            return;
        store.AddLedger(new PaperLedgerEntry(options.StartingBalance, LedgerEntryType.Deposit, null,
            "Vốn giả lập ban đầu", clock.UtcNow));
        await store.SaveAsync(ct);
    }

    private void CloseAt(FuturesPosition p, CloseReason reason, decimal markPrice, Guid? commandId)
    {
        var rules = options.FindRules(p.Symbol);
        var exit = FuturesMath.ApplySlippage(markPrice, p.Side, opening: false, options.SlippageRate);
        if (rules is not null)
            exit = FuturesMath.RoundToTick(exit, rules.TickSize);
        var fee = p.Quantity * exit * options.TakerFeeRate;
        var now = clock.UtcNow;

        p.Close(reason, exit, fee, commandId, now);
        store.AddLedger(new PaperLedgerEntry(p.RealizedPnl!.Value, LedgerEntryType.RealizedPnl, p, $"Đóng: {reason}", now));
        store.AddLedger(new PaperLedgerEntry(-fee, LedgerEntryType.Fee, p, "Phí đóng vị thế", now));

        logger.LogInformation("Paper futures CLOSE {Reason} {Side} {Symbol} @ {Exit} PnL {Pnl} phí {Fee}",
            reason, p.Side, p.Symbol, exit, p.RealizedPnl, p.OpenFee + fee);
    }

    private async Task<(decimal Wallet, decimal Available)> BalancesAsync(CancellationToken ct)
    {
        var wallet = (await store.GetLedgerAsync(ct)).Sum(e => e.Amount);
        var used = (await store.GetOpenPositionsAsync(ct)).Sum(p => p.Margin);
        return (wallet, wallet - used);
    }

    private OpenQuote BuildQuote(OpenPositionRequest r, decimal available, IReadOnlyList<FuturesPosition> open)
    {
        var errors = new List<string>();
        OpenQuote Reject() => new(false, errors, r.Symbol, r.Side, r.Leverage, marks.Get(r.Symbol)?.Price,
            null, null, null, null, null, null, null, null, available);

        var rules = options.FindRules(r.Symbol);
        if (rules is null)
            errors.Add($"Symbol {r.Symbol} chưa được cấu hình cho paper futures.");
        if (r.Leverage < 1 || r.Leverage > options.MaxLeverage)
            errors.Add($"Đòn bẩy phải từ 1 đến {options.MaxLeverage}x.");
        if (r.Margin <= 0)
            errors.Add("Margin phải lớn hơn 0.");
        if (open.Any(p => p.Symbol == r.Symbol))
            errors.Add($"Đã có vị thế mở trên {r.Symbol} (one-way mode). Đóng vị thế cũ trước.");
        var mark = marks.GetFresh(r.Symbol);
        if (mark is null)
            errors.Add("Chưa có mark price mới (dữ liệu stale); chặn mở vị thế.");
        if (errors.Count > 0)
            return Reject();

        var fill = FuturesMath.RoundToTick(
            FuturesMath.ApplySlippage(mark!.Price, r.Side, opening: true, options.SlippageRate), rules!.TickSize);
        var qty = FuturesMath.RoundDown(r.Margin * r.Leverage / fill, rules.QuantityStep);
        var notional = qty * fill;
        var margin = notional / r.Leverage;
        var fee = notional * options.TakerFeeRate;
        var liq = FuturesMath.RoundToTick(
            FuturesMath.LiquidationPrice(r.Side, fill, r.Leverage, rules.MaintenanceMarginRate), rules.TickSize);

        if (qty <= 0)
            errors.Add($"Margin quá nhỏ: khối lượng làm tròn theo bước {rules.QuantityStep} bằng 0.");
        else if (notional < rules.MinNotional)
            errors.Add($"Giá trị vị thế {notional:0.##} USDT nhỏ hơn tối thiểu {rules.MinNotional} USDT.");
        if (margin + fee > available)
            errors.Add($"Không đủ số dư khả dụng: cần {margin + fee:0.##} USDT, có {available:0.##} USDT.");

        var isLong = r.Side == PositionSide.Long;
        if (isLong ? r.StopLoss >= fill : r.StopLoss <= fill)
            errors.Add(isLong ? "Stop loss của Long phải thấp hơn giá vào." : "Stop loss của Short phải cao hơn giá vào.");
        else if (isLong ? r.StopLoss <= liq : r.StopLoss >= liq)
            errors.Add($"Stop loss phải nằm trước giá thanh lý {liq} (nếu không, vị thế bị thanh lý trước khi chạm stop).");
        if (r.TakeProfit is { } tp && (isLong ? tp <= fill : tp >= fill))
            errors.Add(isLong ? "Take profit của Long phải cao hơn giá vào." : "Take profit của Short phải thấp hơn giá vào.");

        var stopExit = FuturesMath.ApplySlippage(r.StopLoss, r.Side, opening: false, options.SlippageRate);
        var lossAtStop = -FuturesMath.UnrealizedPnl(r.Side, qty, fill, stopExit) + fee + qty * stopExit * options.TakerFeeRate;
        decimal? profitAtTp = r.TakeProfit is { } t
            ? FuturesMath.UnrealizedPnl(r.Side, qty, fill, t) - fee - qty * t * options.TakerFeeRate
            : null;

        return new OpenQuote(errors.Count == 0, errors, r.Symbol, r.Side, r.Leverage, mark.Price, fill, qty, notional,
            margin, fee, liq, Math.Min(lossAtStop, margin + fee * 2), profitAtTp, available);
    }

    private static OpenQuote QuoteFromPosition(FuturesPosition p) =>
        new(true, [], p.Symbol, p.Side, p.Leverage, null, p.EntryPrice, p.Quantity, p.Quantity * p.EntryPrice,
            p.Margin, p.OpenFee, p.LiquidationPrice, null, null, 0);
}
