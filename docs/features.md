# Tính năng đã làm

Trạng thái tính đến 2026-10-29 (sau PR #60). Mỗi mục nêu: tính năng làm gì, code nằm ở đâu, chạy theo quy tắc nào,
và **chưa làm** gì. Lịch sử theo PR xem ở [changelog.md](changelog.md).

Chế độ cố định của bot: `DecisionMode = SignalOnly`, `Environment = Paper`, `LiveLocked = true`
([`BotEndpoints.cs`](../src/CryptoBot.Api/Endpoints/BotEndpoints.cs)). Không có API key, không có lệnh thật.

## Mục lục

1. [Đồng bộ nến từ Binance](#1-đồng-bộ-nến-từ-binance)
2. [Chiến lược EMA 9/21 và tín hiệu BUY/SELL](#2-chiến-lược-ema-921-và-tín-hiệu-buysell)
3. [Chỉ báo tự viết (C#)](#3-chỉ-báo-tự-viết-c)
4. [Paper Futures](#4-paper-futures-longshort-có-đòn-bẩy-tiền-giả-lập)
5. [Tự mở lệnh theo giá hoặc theo chỉ báo](#5-tự-mở-lệnh-theo-giá-hoặc-theo-chỉ-báo)
6. [Bảng giá trực tiếp](#6-bảng-giá-trực-tiếp-mỗi-15-giây)
7. [Biên độ 24 giờ / 7 ngày / 30 ngày](#7-biên-độ-24-giờ--7-ngày--30-ngày)
8. [Dự báo "ngày tương tự"](#8-dự-báo-ngày-tương-tự)
9. [Thống kê phiên và cảnh báo mua/bán áp đảo](#9-thống-kê-phiên-và-cảnh-báo-muabán-áp-đảo)
10. [Dự đoán nến kế tiếp lên/xuống](#10-dự-đoán-nến-kế-tiếp-lênxuống)
11. [Tỷ lệ đúng tín hiệu chỉ báo](#11-tỷ-lệ-đúng-tín-hiệu-chỉ-báo)
12. [Nến ngày lịch sử từ 2016 (Bitfinex)](#12-nến-ngày-lịch-sử-từ-2016-bitfinex)
13. [Lịch sử theo tháng và sự kiện thị trường](#13-lịch-sử-theo-tháng-và-sự-kiện-thị-trường)
14. [Dashboard](#14-dashboard)
15. [Trạng thái, health check, log](#15-trạng-thái-health-check-log)
16. [Báo cáo biến động hằng ngày](#16-báo-cáo-biến-động-hằng-ngày)
17. [Tin tức (RSS, mỗi 5 giờ)](#17-tin-tức-rss-mỗi-5-giờ)
18. [Chưa làm / giới hạn đã biết](#18-chưa-làm--giới-hạn-đã-biết)

---

## 1. Đồng bộ nến từ Binance

**Code:** [`MarketDataWorker`](../src/CryptoBot.Api/Workers/MarketDataWorker.cs) →
[`CandleSyncService`](../src/CryptoBot.Application/MarketData/CandleSyncService.cs) →
[`BinanceMarketDataClient`](../src/CryptoBot.Infrastructure/Exchanges/Binance/BinanceMarketDataClient.cs) →
[`CandleStore`](../src/CryptoBot.Infrastructure/Persistence/Stores.cs).

- Mỗi `MarketData:PollInterval` (15 giây), worker đồng bộ mọi cặp (symbol, khung nến) mà **chiến lược đang bật**
  hoặc **chỉ báo tự viết** cần. Khung hỗ trợ: `15m`, `1h`, `1d`
  ([`CandleInterval.cs`](../src/CryptoBot.Domain/MarketData/CandleInterval.cs)).
- Lần đầu tải `BootstrapCandles` (1000) nến. Các lần sau tải lại **từ nến mới nhất đã lưu (bao gồm)**, nên nến đang
  chạy được cập nhật và khoảng thiếu sau khi mất mạng/restart được tải bù tự động. Tối đa `MaxBatchesPerSync` (20)
  batch × 1000 nến mỗi lần.
- Nến "đã đóng" xác định theo **giờ server Binance** (`/api/v3/time`), không theo giờ máy.
- Mỗi batch qua [`CandleSeriesValidator`](../src/CryptoBot.Domain/MarketData/CandleSeriesValidator.cs): nến có giá
  trị không hợp lệ, trùng hoặc sai thứ tự **không được lưu**; nến thiếu (gap) được ghi nhận. Mọi lỗi hiện trong
  `/api/status` và log.
- Upsert theo khóa `(Exchange, Symbol, Interval, OpenTimeUtc)`. Nến đã đóng **không bao giờ bị ghi đè**; nếu sàn trả
  giá khác cho nến đã đóng thì giữ bản đã lưu và báo xung đột
  ([`Candle.ApplyUpdate`](../src/CryptoBot.Domain/MarketData/Candle.cs)).
- Nến lưu từ trước khi có cột `QuoteVolume`/`TradeCount`/`TakerBuyVolume` được tải lại để **điền thêm** các cột này
  (giá không đổi).
- Lỗi sàn (rate limit, 5xx, mạng) ném `MarketDataUnavailableException`; worker lùi thời gian thử lại theo cấp số nhân
  (tối đa 5 phút, có jitter) hoặc theo `Retry-After` của sàn.

## 2. Chiến lược EMA 9/21 và tín hiệu BUY/SELL

**Code:** [`EmaCrossStrategy`](../src/CryptoBot.Domain/Strategies/EmaCrossStrategy.cs),
[`SignalEvaluationService`](../src/CryptoBot.Application/Signals/SignalEvaluationService.cs),
[`StrategyConfigurator`](../src/CryptoBot.Application/Signals/StrategyConfigurator.cs).
Chi tiết thuật toán: [signal-functions.md](signal-functions.md).

- BUY khi EMA nhanh cắt lên EMA chậm; SELL khi cắt xuống **và đang giữ** (long-only).
- Chiến lược khai báo trong `appsettings.json` → `Strategies` (mặc định 3 instance: BTC 1h, BTC 15m, ETH 15m).
  Khi khởi động, `StrategyConfigurator` đồng bộ DB với cấu hình. **Đổi tham số/cặp/khung nến → tăng `Version`** và
  đánh giá lại toàn bộ lịch sử dưới version mới; tín hiệu cũ giữ nguyên với version cũ.
- EMA seed bằng SMA, luôn tính trên **cửa sổ cố định** `LookbackCandles` (300) nến đã đóng gần nhất → chart,
  backtest và live ra cùng giá trị.
- Mỗi nến đã đóng được đánh giá **đúng một lần** cho mỗi instance/version (checkpoint
  `LastEvaluatedCandleOpenTimeUtc`). Tín hiệu và checkpoint ghi trong **cùng một transaction**.
- Tín hiệu tạo quá `Signals:MaxFreshAge` (2 phút) sau khi nến đóng gắn `IsBackfill = true`: chỉ để xem lịch sử, không
  bao giờ được dùng để đặt lệnh.
- Cửa sổ có nến thiếu → Hold, không phát tín hiệu.
- Chưa có Portfolio: "đang giữ" = BUY gần nhất chưa có SELL. Thay bằng vị thế đã khớp khi có OMS.

## 3. Chỉ báo tự viết (C#)

**Code:** [`ICustomIndicator`](../src/CryptoBot.Domain/Indicators/IndicatorContracts.cs),
[`MyRsiIndicator`](../src/CryptoBot.Domain/Indicators/MyRsiIndicator.cs) (ví dụ đầy đủ),
[`TemplateIndicator`](../src/CryptoBot.Domain/Indicators/TemplateIndicator.cs) (khung trống),
[`IndicatorRegistry`](../src/CryptoBot.Application/Indicators/IndicatorRegistry.cs),
[`IndicatorService`](../src/CryptoBot.Application/Indicators/IndicatorService.cs).

- Viết một class C# có `[Indicator("TênLoại")]`, khai báo trong `appsettings.json` → `Indicators`, restart API.
  Chỉ báo hiện trên chart (dropdown góc phải) và dùng được trong quy tắc tự mở lệnh. Từng bước:
  [extending.md → Thêm chỉ báo](extending.md#1-thêm-một-chỉ-báo-tự-viết).
- `IndicatorRegistry` tìm mọi class có `[Indicator]` trong assembly Domain bằng reflection, gán tham số từ cấu hình
  (không phân biệt hoa thường, số dùng dấu chấm). Cấu hình sai (loại/tham số/interval) bị **bỏ qua kèm lỗi** trên
  dashboard và log, không làm dừng app.
- Quy ước giống chiến lược: chỉ nến đã đóng, cửa sổ cố định `RequiredCandles`, cửa sổ có nến thiếu → không tín hiệu.
  Lỗi ném ra trong `Compute` được bắt lại và hiện thành lý do.
- **MyRsi** có 2 chế độ:
  - Không bật `TrendEmaPeriod` (đảo chiều): RSI cắt lên `Oversold` (30) → Long, cắt xuống `Overbought` (70) → Short.
  - Bật `TrendEmaPeriod` (ví dụ 50, hồi trong xu hướng, sửa ở PR #48): giá trên EMA và RSI cắt lên `PullbackLong`
    (40) → Long; giá dưới EMA và RSI cắt xuống `PullbackShort` (60) → Short. Lý do bỏ 30/70 khi có EMA: RSI dưới
    30 thì giá gần như luôn dưới EMA, nên bản cũ cho 0 tín hiệu.
- Mặc định có 2 chỉ báo: `btc-1h-myrsi` (đảo chiều) và `eth-15m-myrsi-trend` (hồi trong xu hướng, EMA50).

## 4. Paper Futures (Long/Short có đòn bẩy, tiền giả lập)

**Code:** [`PaperFuturesService`](../src/CryptoBot.Application/Futures/PaperFuturesService.cs),
[`FuturesMath`](../src/CryptoBot.Domain/Futures/FuturesMath.cs),
[`FuturesPosition`](../src/CryptoBot.Domain/Futures/FuturesPosition.cs),
[`PaperFuturesWorker`](../src/CryptoBot.Api/Workers/PaperFuturesWorker.cs).
UI: [`TradePanel.tsx`](../src/cryptobot-dashboard/src/features/futures/TradePanel.tsx),
[`PositionsCard.tsx`](../src/cryptobot-dashboard/src/features/futures/PositionsCard.tsx).

- Chọn Long/Short, đòn bẩy (tối đa `PaperFutures:MaxLeverage` = 10), margin, Stop loss (**bắt buộc**), Take profit.
  Bấm mở → backend báo giá (`/quote`) → hộp xác nhận hiện giá vào, khối lượng, margin, phí, **giá thanh lý**, lỗ ước
  tính tại SL → xác nhận mới mở.
- Mark price lấy từ Binance USDⓈ-M (`/fapi/v1/premiumIndex`, không cần key) mỗi 2 giây. Mark cũ hơn 10 giây → chặn
  mở/đóng/trigger.
- Khớp market tại mark ± trượt giá **bất lợi** (`SlippageRate` 0.05%), phí taker mỗi chiều (`TakerFeeRate` 0.05%).
  Isolated margin, one-way mode (tối đa 1 vị thế mở mỗi symbol).
- Giá thanh lý: Long `entry × (1 − 1/L) / (1 − mmr)`, Short `entry × (1 + 1/L) / (1 + mmr)`; `mmr` cố định theo symbol.
- Worker kiểm tra theo thứ tự **thanh lý → SL → TP** (bảo thủ). Thanh lý mất đúng toàn bộ margin. SL bị gap thì khớp
  tại mark, lỗ không vượt margin. SL phải nằm trước giá thanh lý, nếu không lệnh bị từ chối.
- Mở/đóng mang `commandId` (GUID do client tạo): gửi lặp (double-click, retry) không mở/đóng hai lần. Mọi ghi số
  dư/vị thế đi qua một khóa (`PaperFuturesLock`), API và worker không ghi chồng.
- Sổ vốn `PaperLedger` (Deposit/Fee/RealizedPnl). Số dư = tổng bút toán, tính trong app. Vốn đầu
  `PaperFutures:StartingBalance` (1000 USDT), nạp một lần khi khởi động.
- **Chưa mô phỏng:** funding fee, bậc maintenance margin, order book/partial fill, cross margin, hedge mode,
  insurance fund.

## 5. Tự mở lệnh theo giá hoặc theo chỉ báo

**Code:** [`AutoOrderService`](../src/CryptoBot.Application/Futures/AutoOrderService.cs),
[`AutoOrderRule`](../src/CryptoBot.Domain/Futures/AutoOrderRule.cs).
UI: [`AutoOrdersCard.tsx`](../src/cryptobot-dashboard/src/features/auto/AutoOrdersCard.tsx).

**Theo giá:** điều kiện `mark ≤ mức` hoặc `mark ≥ mức`, hướng, margin, đòn bẩy, SL %, TP %. Khi mark chạm mức, bot
mở **một** lệnh Paper qua đúng `PaperFuturesService` (cùng kiểm tra SL/giá thanh lý/số dư/min notional) rồi quy tắc
kết thúc.

**Theo chỉ báo:** chọn chỉ báo và hướng. Quy tắc kích hoạt khi chỉ báo báo cùng hướng trên **nến đóng sau lúc tạo
quy tắc**, và nến đó là nến mới nhất (trễ quá 1 khung + 2 phút coi là dữ liệu cũ, bỏ qua). Giá trị chỉ báo lúc kích
hoạt lưu ở `TriggerDetail`.

**Tự đóng khi chỉ báo đảo chiều** (`closeOnReverse`, chỉ với quy tắc theo chỉ báo): sau khi mở, nếu chính chỉ báo đó
báo ngược hướng trên nến đóng sau lúc mở lệnh thì bot đóng vị thế theo mark (lý do `Signal`). SL/TP chạy song song;
SL/TP/thanh lý/đóng tay đến trước thì quy tắc ngừng theo dõi. Chưa có mark mới thì thử lại ở vòng sau.

Quy tắc chung:

- Mặc định thận trọng (`AutoOrders`): margin 50 USDT, 2x, SL 1%, TP 2%, tối đa 10 quy tắc đang chạy.
- SL/TP tính theo % so với **mark lúc kích hoạt**, làm tròn theo tick của symbol.
- **Giới hạn lỗ ngày:** lỗ thực hiện (PnL đã chốt + phí) trong ngày UTC ≥ 5% vốn đầu ngày → quy tắc không kích hoạt
  (giữ trạng thái chờ, ghi chú "Bị chặn") đến 00:00 UTC. Tự đóng khi đảo chiều **không** bị chặn (đóng lệnh chỉ giảm
  rủi ro).
- Từ chối khi tạo: điều kiện đã đúng ở giá hiện tại (tránh kích hoạt ngay), SL sau giá thanh lý, margin × đòn bẩy
  không đủ một bước khối lượng, quá số quy tắc tối đa.
- Lúc kích hoạt mà engine từ chối (thiếu số dư, đã có vị thế cùng symbol…) → quy tắc chuyển `Rejected` kèm lý do, không
  tự thử lại.
- `CommandId` của quy tắc cũng là `commandId` của lệnh mở, nên xử lý lặp (crash/restart) không mở hai vị thế.
- Worker xử lý SL/TP/thanh lý của vị thế đang mở **trước**, rồi mới đến quy tắc tự động.
- Chỉ xét nến đóng mới nhất ở mỗi vòng 2 giây: nếu bot tắt đúng lúc có tín hiệu thì tín hiệu đó bị bỏ lỡ.

## 6. Bảng giá trực tiếp (mỗi 15 giây)

**Code:** [`LivePriceWorker`](../src/CryptoBot.Api/Workers/LivePriceWorker.cs),
[`LivePrices.cs`](../src/CryptoBot.Application/LivePrices/LivePrices.cs).
UI: [`LivePricePanel.tsx`](../src/cryptobot-dashboard/src/features/live/LivePricePanel.tsx).

- Mỗi `LivePrices:PollInterval` lấy **Spot** (`/api/v3/ticker/price`) và **Mark Futures** song song cho từng symbol
  trong `LivePrices:Symbols` (BTCUSDT, ETHUSDT). Dùng `PeriodicTimer` nên mốc 15 giây đều, không cộng dồn thời gian xử lý.
- Hiển thị giá, % đổi so với lần trước, chênh Mark − Spot, độ trễ từng nguồn, trạng thái OK/Lỗi, đếm ngược lần lấy
  tiếp theo, cảnh báo "Chậm" khi quá 2 chu kỳ không có mẫu mới.
- Một nguồn lỗi thì chỉ nguồn đó để trống kèm lý do, **không điền giá cũ**.
- Lịch sử 240 mẫu (≈ 1 giờ) giữ trong bộ nhớ, mất khi restart.

## 7. Biên độ 24 giờ / 7 ngày / 30 ngày

**Code:** [`DailyMoveRange`](../src/CryptoBot.Domain/MarketData/DailyMoveRange.cs),
[`DailyRangeService`](../src/CryptoBot.Application/LivePrices/DailyRangeService.cs).
UI: [`DailyRangeBar.tsx`](../src/cryptobot-dashboard/src/features/live/DailyRangeBar.tsx).

- Lấy nến ngày đã đóng từ Binance (365 ngày, cache 15 phút, không lưu DB). Tính σ của log return một ngày.
  Khoảng `giá × e^(±σ)` ≈ 68% số ngày, `giá × e^(±2σ)` ≈ 95%. Kèm % ngày tăng, lần tăng/giảm mạnh nhất.
- 24 giờ dùng σ của 90 ngày gần nhất. 7/30 ngày dùng σ của 365 ngày × √N (giả định các ngày độc lập); số liệu lịch sử
  tính trên các đoạn N ngày trượt từng ngày trong một năm.
- Mốc giá là Spot mới nhất trong bảng giá trực tiếp, chưa có thì dùng giá đóng gần nhất.
- **Chỉ là biên độ, không đoán hướng.**

## 8. Dự báo "ngày tương tự"

**Code:** [`SimilarDaysForecast`](../src/CryptoBot.Domain/MarketData/SimilarDaysForecast.cs),
[`SimilarDaysService`](../src/CryptoBot.Application/LivePrices/SimilarDaysService.cs).
Hiện dưới thanh biên độ 24 giờ.

- Mô tả mỗi ngày bằng 5 con số: khoảng cách giá tới EMA200 và EMA50, RSI14, % thay đổi 7 ngày, % thay đổi 1 ngày.
  Tìm 60 ngày trong lịch sử giống hôm nay nhất, xem sau 1 ngày giá của chúng đi thế nào: tỷ lệ tăng, trung vị,
  khoảng 68% (phân vị 16–84). Khoảng có thể **lệch** về phía hay xảy ra hơn (khác biên độ σ luôn đối xứng).
- Nghiêng rõ khi tỷ lệ tăng ≥ 60% hoặc ≤ 40%; còn lại là `Neutral`.
- Dùng nến ngày Bitfinex từ 2016 trong DB (BTCUSDT → BTCUSD). Khoảng giá **neo vào giá đóng hôm qua (UTC)**, không
  phải giá hiện tại; chỉ trả kết quả khi lịch sử đã có đến hôm qua.
- Kèm chạy thử walk-forward (chỉ dùng ngày đã biết kết quả), bỏ qua ngày hòa và ngày giá đứng yên.
  Kết quả kiểm chứng trên BTC/ETH: 24 giờ đúng khoảng 54–55% khi nghiêng mạnh; 7 ngày không có lợi thế, nên chỉ bật
  cho 24 giờ.
- Kết quả cache theo (symbol, mốc, ngày cuối): mỗi ngày tính một lần. Lỗi phần này chỉ ghi log, thanh biên độ vẫn hiện.

## 9. Thống kê phiên và cảnh báo mua/bán áp đảo

**Code:** [`VolumePressure`](../src/CryptoBot.Domain/MarketData/VolumePressure.cs).
UI: [`SessionStatsCard.tsx`](../src/cryptobot-dashboard/src/features/market/SessionStatsCard.tsx).

- Mỗi nến là một phiên (15m → phiên 15 phút): số giao dịch khớp, khối lượng (coin và USDT), so với trung bình 20
  phiên gần nhất.
- Binance trả sẵn khối lượng **mua chủ động** (taker buy, cột `k[9]` của kline); phần còn lại của Volume là **bán chủ
  động**. Phía lớn ≥ phía nhỏ × (1 + ngưỡng%) → cảnh báo `BuyHeavy`/`SellHeavy`.
- Ngưỡng mặc định 80% (`MarketData:VolumeAlertExcessPercent`), tức một phía chiếm ≥ ~64% tổng khối lượng.
  Dashboard cảnh báo cho phiên vừa đóng và phiên đang chạy.

## 10. Dự đoán nến kế tiếp lên/xuống

**Code:** [`PriceDirectionPredictor`](../src/CryptoBot.Domain/Prediction/PriceDirectionPredictor.cs),
[`PredictionEndpoints`](../src/CryptoBot.Api/Endpoints/PredictionEndpoints.cs).
UI: [`PredictionCard.tsx`](../src/cryptobot-dashboard/src/features/prediction/PredictionCard.tsx), trang **Dự đoán**.

- "Hàm core" để tự tối ưu dần: điểm = tổng có trọng số của 3 thành phần, dương nghiêng Up, âm nghiêng Down:
  - **Xu hướng:** giá đóng cách trung bình `TrendPeriod` (20) nến bao nhiêu %.
  - **Đà:** giá đã đi bao nhiêu % trong `MomentumPeriod` (3) nến gần nhất.
  - **Lực mua/bán:** mua chủ động hơn/kém 50% bao nhiêu điểm % ở nến cuối (trọng số 0.05; bỏ qua nếu thiếu dữ liệu).
- Trọng số trong [`PredictorSettings`](../src/CryptoBot.Domain/Prediction/PriceDirectionPredictor.cs); đặt trọng số
  = 0 để tắt một thành phần. Sửa rồi restart API là thấy kết quả mới.
- Kèm `Backtest`: với mỗi nến, chỉ dùng các nến trước nó để đoán (`PredictAt`), so với thực tế, bỏ qua nến đóng bằng
  đúng giá nến trước. Dashboard hiện tỷ lệ đúng và các lần đoán gần nhất.
- Chọn **Nến kế tiếp** (theo khung của chiến lược) hoặc **Theo ngày** (`1d`, nến ngày Binance).

## 11. Tỷ lệ đúng tín hiệu chỉ báo

**Code:** [`SignalAccuracy`](../src/CryptoBot.Domain/Prediction/SignalAccuracy.cs).
UI: [`IndicatorAccuracyCard.tsx`](../src/cryptobot-dashboard/src/features/prediction/IndicatorAccuracyCard.tsx).

- Với mỗi chỉ báo tự viết cùng symbol/khung: sau 1, 3, 6, 12 nến, giá có đi đúng hướng tín hiệu Long/Short không, và
  trung bình đi được bao nhiêu % theo hướng tín hiệu (âm = đi ngược). Tín hiệu quá gần cuối chuỗi không được chấm ở
  mốc chưa đủ nến.

## 12. Nến ngày lịch sử từ 2016 (Bitfinex)

**Code:** [`DailyHistoryWorker`](../src/CryptoBot.Api/Workers/DailyHistoryWorker.cs),
[`DailyHistorySyncService`](../src/CryptoBot.Application/MarketData/DailyHistorySyncService.cs),
[`BitfinexDailyHistoryClient`](../src/CryptoBot.Infrastructure/Exchanges/Bitfinex/BitfinexDailyHistoryClient.cs).

- Lưu nến `1d` của BTCUSD, ETCUSD, ETHUSD từ 2016-01-01 vào bảng `Candles` với `Exchange = "Bitfinex"`.
  Mỗi giờ đồng bộ lại từ nến mới nhất (bao gồm), ngày thiếu sau downtime được tải bù.
- Bitfinex trả 403 nếu request không có `User-Agent`, nên client gửi `CryptoBot/1.0`.
- Dùng cho lịch sử theo tháng (mục 13) và dự báo ngày tương tự (mục 8).

## 13. Lịch sử theo tháng và sự kiện thị trường

**Code:** [`MonthlyChangeReport`](../src/CryptoBot.Domain/MarketData/MonthlyChangeReport.cs),
[`MarketEventCatalog`](../src/CryptoBot.Application/MarketData/MarketEvents.cs),
[`marketevents.json`](../src/CryptoBot.Api/marketevents.json).
UI: trang **Lịch sử theo tháng** ([`HistoryPage.tsx`](../src/cryptobot-dashboard/src/pages/HistoryPage.tsx),
[`MonthlyHeatmap.tsx`](../src/cryptobot-dashboard/src/features/history/MonthlyHeatmap.tsx),
[`MoveEventsCard.tsx`](../src/cryptobot-dashboard/src/features/history/MoveEventsCard.tsx)).

- Heatmap năm × tháng: % từ giá mở cửa ngày đầu tháng đến giá đóng cửa ngày cuối tháng. Tháng đang chạy đánh dấu
  chưa trọn vẹn. Kèm % cả năm.
- Thống kê cùng một tháng qua các năm (chỉ tháng trọn vẹn): trung bình, tốt nhất, tệ nhất, số năm tăng. Dùng để xem
  tính mùa vụ, **không dự đoán tháng tới**.
- Các tháng tăng/giảm từ 10/15/20/30% trở lên, kèm sự kiện thị trường cùng tháng. Sự kiện ghi tay trong
  `marketevents.json` (chung cả thị trường hoặc riêng symbol); chỉ là sự kiện cùng tháng, không khẳng định nguyên nhân.
  Sai định dạng tháng (`yyyy-MM`) hoặc thiếu `Title` → app không khởi động được (lỗi rõ ràng).

## 14. Dashboard

**Code:** [`src/cryptobot-dashboard`](../src/cryptobot-dashboard). Chi tiết thư mục ở
[folder-structure.md](folder-structure.md#srccryptobot-dashboard--frontend).

- Menu trái 7 trang: Biểu đồ, Giao dịch Paper, Dự đoán, Tín hiệu, Lịch sử theo tháng, Báo cáo ngày, Tin tức. Trang
  hiện tại nằm trong `#hash` (tải lại hoặc gửi link vẫn mở đúng trang).
- Nút chọn chiến lược (`BTCUSDT 1h`, `BTCUSDT 15m`, …) ở góc phải; các trang (trừ Lịch sử, Báo cáo ngày, Tin tức)
  dùng symbol/khung của chiến lược đang chọn.
- Lọc trên tiêu đề cột (`components/tableFilters.ts`) ở các bảng Tín hiệu, Lịch sử vị thế, Quy tắc tự động, Dự đoán,
  Tín hiệu chỉ báo và Các ngày trước. Trang Tin tức có thêm ô tìm kiếm và chọn nguồn. Các bộ lọc này chạy trên dữ
  liệu đã tải về (ví dụ 50 tín hiệu, 50 vị thế, 100 tin), không gọi lại API.
- Thanh trạng thái: chế độ, môi trường, feed nào stale, lỗi đồng bộ.
- Thời gian hiển thị theo giờ Việt Nam (`Asia/Ho_Chi_Minh`); backend luôn trả UTC.
- Giao diện tiếng Việt (`antd/locale/vi_VN`).

> **Lưu ý:** PR #53 (Dark mode và trang Cài đặt) được merge vào nhánh `fix/similar-days-review` **sau khi** nhánh đó
> đã merge vào `develop`, nên dark mode và trang Cài đặt **chưa có** trong `develop`/`main`. 5 commit này chỉ còn trên
> `origin/fix/similar-days-review`; cần mở PR mới để đưa vào `develop`.

## 15. Trạng thái, health check, log

- `GET /api/status`: chế độ, môi trường, giờ server, trạng thái từng feed (lần thành công/thử gần nhất, lỗi, số lần
  lỗi liên tiếp, nến đóng mới nhất, 20 lỗi dữ liệu gần nhất, `isStale`) và danh sách chiến lược.
  Feed stale khi quá `StaleAfter` (2 phút) không sync thành công, hoặc nến đóng mới nhất trễ hơn một khung + 2 phút.
  Trạng thái chỉ nằm trong bộ nhớ ([`MarketDataStatusRegistry`](../src/CryptoBot.Application/MarketData/MarketDataStatusRegistry.cs)).
- `GET /health/live` (process sống), `GET /health/ready` (kết nối được DB, không thì 503).
- Log Serilog ra console: `[HH:mm:ss LVL] SourceContext: message`. Log có cấu trúc (`{Symbol}`, `{Key}`…), không nối
  chuỗi.
- `/api/...` không tồn tại trả 404 JSON, không bao giờ rơi vào trang SPA.

## 16. Báo cáo biến động hằng ngày

**Code:** [`DailyMoveReport`](../src/CryptoBot.Domain/MarketData/DailyMoveReport.cs),
[`DailyReportService`](../src/CryptoBot.Application/Reports/DailyReportService.cs),
[`DailyReportWorker`](../src/CryptoBot.Api/Workers/DailyReportWorker.cs).
UI: trang **Báo cáo ngày** ([`ReportsPage.tsx`](../src/cryptobot-dashboard/src/pages/ReportsPage.tsx),
[`DailyReportCard.tsx`](../src/cryptobot-dashboard/src/features/reports/DailyReportCard.tsx)).

- Mỗi ngày lúc **06:00 giờ Việt Nam** (`DailyReports:RunAt`, `TimeZone`), bot lấy nến `1h` đã đóng của 24 giờ vừa qua
  từ Binance cho từng symbol (`BTCUSDT`, `ETHUSDT`) và lưu một báo cáo vào bảng `DailyReports`.
- Báo cáo gồm: giá mở → đóng và % thay đổi, cao nhất/thấp nhất kèm giờ, biên độ cao–thấp, **cú giảm sâu nhất tính từ
  đỉnh trước đó** và **cú hồi mạnh nhất tính từ đáy trước đó**, tỷ lệ mua chủ động, giờ tăng/giảm mạnh nhất, và các
  **giờ khối lượng bất thường** (≥ 3 lần trung bình 20 nến trước, kèm bên mua/bán chiếm ưu thế).
- Trong một nến không biết đỉnh hay đáy đến trước, nên cú giảm/hồi chỉ so với đỉnh/đáy của các nến trước (và giá mở
  của chính nến): không bao giờ phóng đại.
- Mỗi (symbol, giờ lập) chỉ có **một** báo cáo (index duy nhất `(Symbol, PeriodEndUtc)`), nên chạy lại không tạo trùng.
- Bot tắt lúc 06:00 → khi bật lại sẽ **tạo bù** báo cáo của giờ lập gần nhất. Sàn lỗi → thử lại sau 5 phút. Worker
  ngủ tối đa 1 giờ mỗi lần để máy ngủ/thức cũng không lỡ giờ lập.
- Nút **Tạo ngay** (`POST /api/reports/daily/run`) lập báo cáo còn thiếu mà không cần chờ.
- Dưới mỗi báo cáo có **tin nhắc tới coin đó, đăng trong đúng khoảng báo cáo** (mục 17), để gợi ý nguyên nhân.
- Chỉ mô tả chuyện đã xảy ra, không dự đoán; tin cùng khoảng thời gian không chắc là nguyên nhân.
- **Chưa làm:** chỉ tạo bù báo cáo của giờ lập gần nhất (không bù nhiều ngày bị lỡ), chưa gửi thông báo ra ngoài
  (Telegram, email).

## 17. Tin tức (RSS, mỗi 5 giờ)

**Code:** [`NewsText`](../src/CryptoBot.Domain/News/NewsText.cs),
[`NewsArticle`](../src/CryptoBot.Domain/News/NewsArticle.cs),
[`NewsSyncService`](../src/CryptoBot.Application/News/NewsSyncService.cs),
[`RssNewsFeedClient`](../src/CryptoBot.Infrastructure/News/RssNewsFeedClient.cs),
[`NewsWorker`](../src/CryptoBot.Api/Workers/NewsWorker.cs).
UI: trang **Tin tức** ([`NewsPage.tsx`](../src/cryptobot-dashboard/src/pages/NewsPage.tsx),
[`NewsList.tsx`](../src/cryptobot-dashboard/src/features/news/NewsList.tsx)) và trong card báo cáo ngày.

- Nguồn: RSS công khai của **CoinDesk, Cointelegraph, Decrypt** (không cần API key), khai báo trong `News:Feeds`.
  Đọc được cả RSS 2.0 và Atom.
- `NewsWorker` đồng bộ ngay khi khởi động, sau đó **mỗi 5 giờ** (`News:PollInterval`). Nút **Đồng bộ ngay**
  (`POST /api/news/sync`) chạy ngay một lượt.
- Mỗi bài lưu tiêu đề, tóm tắt (bỏ HTML, cắt còn 500 ký tự), link bài gốc, giờ đăng (UTC). Khóa duy nhất
  `(Source, ExternalId)` với `ExternalId` là guid của RSS (không có thì là link): đồng bộ lại không lưu trùng.
- Coin (BTC/ETH/ETC) gắn theo từ khóa nguyên từ trong tiêu đề + tóm tắt: `bitcoin`/`btc`, `ether`/`ethereum`/`eth`,
  `ethereum classic`/`ETC` viết hoa ("etc." trong câu tiếng Anh không tính). Có thể sót hoặc nhầm.
- Một nguồn lỗi (mạng, 4xx/5xx, XML hỏng) chỉ ghi log và trả trong `errors`, các nguồn khác vẫn chạy. Bài thiếu tiêu
  đề hoặc ngày đăng bị bỏ qua.
- **Giới hạn:** feed chỉ giữ 20–60 bài mới nhất; nguồn đăng dày có thể đẩy bài ra khỏi feed trước lượt đồng bộ sau
  5 giờ, nên có thể lỡ một ít bài. Không dịch, không tóm tắt bằng AI, không đánh giá tin tốt/xấu.

## 18. Chưa làm / giới hạn đã biết

| Hạng mục | Hiện trạng |
| --- | --- |
| Giao dịch thật / testnet | Chưa có. Không có API key, `LiveLocked = true` |
| Portfolio/OMS cho chiến lược EMA | Chưa có. "Đang giữ" suy ra từ tín hiệu BUY/SELL |
| Tín hiệu EMA → lệnh Paper | Chưa nối. Lệnh tự động chỉ chạy từ quy tắc theo giá/theo chỉ báo |
| Futures | Không có funding, bậc maintenance margin, order book, partial fill, cross margin, hedge mode |
| Loại chiến lược | Chỉ `EmaCross` (`StrategyFactory`) |
| Khung nến | Chỉ `15m`, `1h`, `1d` |
| Dữ liệu thời gian thực | REST polling (15s nến, 2s mark), chưa dùng WebSocket |
| Bộ nhớ tạm | Bảng giá trực tiếp, trạng thái feed, cache biên độ mất khi restart |
| Xác thực | Không có đăng nhập; chỉ nên chạy trên máy cá nhân |
| Dark mode / Cài đặt | Có ở PR #53 nhưng chưa vào `develop` (xem mục 14) |
| Frontend test | Chưa có; chỉ có `tsc -b` (typecheck) |
