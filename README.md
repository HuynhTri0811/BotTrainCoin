# CryptoTradingBot — vertical slice

Một cặp/một khung nến: tải nến Binance (REST công khai) → EMA 9/21 trên nến đã đóng → lưu signal vào SQLite → hiển thị trên dashboard.
Chế độ cố định: `SignalOnly + Paper`, live bị khóa. Chưa có order, risk, ledger hay API key.

## Chạy

```bash
dotnet run --project src/CryptoBot.Api --launch-profile http
```

```bash
npm --prefix src/cryptobot-dashboard install && npm --prefix src/cryptobot-dashboard run dev
```

Dashboard dev: http://localhost:8090 (proxy `/api` sang http://localhost:5080).
Production: `npm run build` xuất vào `src/CryptoBot.Api/wwwroot`, API phục vụ luôn file tĩnh.

DB: `src/CryptoBot.Api/data/cryptobot.db` (tạo tự động, migration chạy khi khởi động).
Chiến lược khai báo trong `appsettings.json` → `Strategies`; đổi tham số sẽ tăng `Version` và đánh giá lại lịch sử dưới version mới.

## Test

```bash
dotnet test
```

## Quy ước đã chốt trong slice này

- Tiền/giá/lượng: `decimal`, lưu SQLite dạng chuỗi invariant; không SUM/ORDER BY trong SQL.
- Thời gian: UTC, lưu Unix milliseconds; UI đổi sang giờ Việt Nam.
- Nến đóng xác định theo giờ server Binance, không theo giờ máy/trình duyệt.
- EMA: seed SMA, luôn tính trên cửa sổ cố định `LookbackCandles` nến đã đóng gần nhất → live/backtest cho cùng giá trị.
- Mỗi nến đóng được đánh giá đúng một lần cho mỗi instance/version; signal và checkpoint ghi cùng transaction.
- Signal từ nến cũ (tải bù, khởi động) gắn `IsBackfill = true`, không được dùng để đặt lệnh.
- Cửa sổ có nến thiếu → không phát signal. Nến OHLC không hợp lệ → không lưu, báo trong `/api/status`.
- Trong slice chưa có Portfolio: "đang giữ" = BUY gần nhất chưa có SELL. Thay bằng vị thế đã khớp khi có OMS.

## Endpoints

`GET /api/status`, `/api/strategies`, `/api/strategies/{id}/chart?limit=`, `/api/signals?strategyId=&limit=`,
`/api/market/candles?symbol=&interval=&limit=`, `/health/live`, `/health/ready`.

## Paper Futures (Long/Short có đòn bẩy, tiền giả lập)

Panel bên phải chart: chọn Long/Short, đòn bẩy (tối đa `PaperFutures:MaxLeverage`), margin, Stop loss (bắt buộc), Take profit.
Bấm mở → backend tính báo giá → hộp xác nhận hiện giá vào, khối lượng, margin, phí, **giá thanh lý**, lỗ ước tính tại SL → xác nhận mới mở.

- Mark price lấy từ Binance USDⓈ-M công khai (`/fapi/v1/premiumIndex`, không cần key), mỗi 2 giây. Mark cũ hơn 10 giây → chặn mở/đóng/trigger.
- Khớp market tại mark ± trượt giá bất lợi, phí taker mỗi chiều. Isolated margin, one-way (1 vị thế/symbol).
- Giá thanh lý: Long `entry×(1−1/L)/(1−mmr)`, Short `entry×(1+1/L)/(1+mmr)`, mmr cố định theo symbol trong config.
- Worker kiểm tra theo thứ tự: thanh lý → SL → TP. Thanh lý mất đúng toàn bộ margin. SL bị gap khớp tại mark, lỗ không vượt margin.
- Stop loss phải nằm trước giá thanh lý, nếu không lệnh bị từ chối.
- Mở/đóng có `commandId`: gửi lặp không mở hai lần. Mọi ghi số dư/vị thế đi qua một khóa.
- Sổ vốn: `PaperLedger` (Deposit/Fee/RealizedPnl). Vốn đầu `PaperFutures:StartingBalance`.
- **Chưa mô phỏng:** funding fee, bậc maintenance margin, order book/partial fill, cross margin, hedge mode.
- Endpoint: `/api/paper/futures/{account, quote, orders, positions/{id}/close, positions/history}`.

## Tự mở lệnh theo giá (Paper)

Card "Tự mở lệnh theo giá" dưới panel Futures: đặt điều kiện `mark price ≤ / ≥ mức giá`, hướng Long/Short, margin, đòn bẩy, SL %, TP %.
Khi mark price chạm mức, bot tự mở **một** lệnh Paper qua đúng `PaperFuturesService` (cùng kiểm tra SL/giá thanh lý/số dư/min notional) rồi quy tắc kết thúc.
Chỉ chạy Paper; không có đường nào tới tiền thật.

- Mặc định thận trọng (`AutoOrders` trong `appsettings.json`): margin 50 USDT, 2x, SL 1%, TP 2%, tối đa 10 quy tắc đang chạy.
- SL/TP tính theo % so với **mark price lúc kích hoạt**, làm tròn theo tick của symbol.
- Giới hạn lỗ ngày: nếu lỗ thực hiện (PnL đã chốt + phí) trong ngày UTC ≥ 5% vốn đầu ngày thì quy tắc không kích hoạt (giữ nguyên trạng thái chờ, hiện ghi chú "Bị chặn") cho đến 00:00 UTC.
- Khi tạo, quy tắc bị từ chối nếu: điều kiện đã đúng ở giá hiện tại (tránh kích hoạt ngay), stop loss nằm sau giá thanh lý, margin × đòn bẩy không đủ một bước khối lượng ở mức giá đặt, hoặc quá số quy tắc tối đa.
- Lúc kích hoạt nếu engine từ chối (thiếu số dư, đã có vị thế cùng symbol…) quy tắc chuyển "Bị từ chối" kèm lý do và không tự thử lại.
- Không dùng mark price cũ hơn 10 giây. `commandId` của quy tắc cũng là commandId của lệnh mở nên xử lý lặp (crash/restart) không mở hai vị thế.
- Endpoint: `GET/POST /api/paper/futures/auto-rules`, `POST /api/paper/futures/auto-rules/{id}/cancel`.

## Chỉ báo tự viết (C#)

Viết công thức của riêng bạn, xem nó trên chart và dùng nó để tự mở lệnh Paper.

**Cách viết một chỉ báo mới**
1. Copy `src/CryptoBot.Domain/Indicators/TemplateIndicator.cs` thành file mới (ví dụ `MyBreakoutIndicator.cs`), đổi tên class.
   Ví dụ đầy đủ có chú thích từng bước: `MyRsiIndicator.cs`.
2. Thêm `[Indicator("MyBreakout", Description = "...")]` trên class.
3. Tham số = property public có setter. `Lines` = các đường vẽ (`Overlay: true` vẽ trên nến, `false` vẽ ở khung bên dưới, `Levels` là mức ngang như 30/70).
   `Compute(nến đã đóng)` trả `Values` (khóa trùng `Lines.Key`) + `Signal` (`None/Long/Short`) + `Reason`.
4. Khai báo trong `appsettings.json` → `Indicators` (tên tham số không phân biệt hoa thường, số dùng dấu chấm):
   ```json
   { "Name": "btc-1h-myrsi", "Type": "MyRsi", "Symbol": "BTCUSDT", "Interval": "1h",
     "Parameters": { "Period": 14, "Oversold": 30, "Overbought": 70 } }
   ```
5. Restart API. Cấu hình sai (loại/tham số/interval) bị bỏ qua và hiện cảnh báo trên dashboard + log.

**Quy ước**: chỉ dùng nến đã đóng, tính trên cửa sổ cố định `RequiredCandles` nến → chart và lệnh tự động ra cùng giá trị.
Cửa sổ thiếu nến → không tín hiệu. Lỗi trong `Compute` được bắt lại và hiện thành lý do, không làm dừng bot.
Nến của symbol/khung trong `Indicators` được đồng bộ kể cả khi không có chiến lược nào dùng.

**Trên chart**: chọn chỉ báo cùng symbol và khung nến ở góc phải card chart. Chấm tím `L`/`S` là tín hiệu của chỉ báo.

**Tự mở lệnh theo chỉ báo**: trong card "Tự mở lệnh", chọn *Theo chỉ báo* → chọn chỉ báo và hướng Long/Short.
Quy tắc kích hoạt khi chỉ báo báo cùng hướng trên **nến đóng sau lúc tạo quy tắc**, và nến đó là nến mới nhất
(trễ quá 1 khung + 2 phút coi như dữ liệu cũ, bỏ qua). Sau đó đi đúng đường của quy tắc theo giá: giới hạn lỗ ngày,
SL/TP theo % mark price lúc kích hoạt, một lần duy nhất. Giá trị chỉ báo lúc kích hoạt lưu ở `TriggerDetail`.
Chỉ xét nến đóng mới nhất ở mỗi vòng 2 giây, nên nếu bot tắt đúng lúc có tín hiệu thì tín hiệu đó bị bỏ lỡ.

**Tự đóng khi chỉ báo đảo chiều**: tick *Tự đóng khi chỉ báo đảo chiều* khi tạo quy tắc theo chỉ báo. Sau khi mở lệnh,
nếu chính chỉ báo đó báo ngược hướng (Long đang mở mà chỉ báo báo Short, và ngược lại) trên **nến đóng sau lúc mở lệnh**,
bot tự đóng vị thế theo mark price (lý do đóng `Signal`, hiện "Chỉ báo đảo chiều" trong lịch sử vị thế).
SL/TP vẫn chạy song song; nếu SL/TP/thanh lý/đóng tay đến trước thì quy tắc ngừng theo dõi. Tự đóng không bị chặn
bởi giới hạn lỗ ngày. Nếu chưa có mark price mới thì giữ theo dõi và thử lại ở vòng sau.

Endpoint: `GET /api/indicators`, `GET /api/indicators/{name}/chart?limit=`; tạo quy tắc: `POST /api/paper/futures/auto-rules`
với `"triggerType": "Indicator", "indicatorName": "..."` (thêm `"closeOnReverse": true` để tự đóng khi đảo chiều).

## Cảnh báo mua/bán áp đảo

Card "Giao dịch theo phiên" có cột *Mua / Bán chủ động*: Binance trả sẵn khối lượng **mua chủ động** (taker buy, cột k[9] của kline),
phần còn lại của Volume là **bán chủ động**. Thuật toán ở `CryptoBot.Domain/MarketData/VolumePressure.cs`:
phía lớn hơn ≥ phía nhỏ hơn × (1 + ngưỡng%) thì cảnh báo `SellHeavy` (bán áp đảo) hoặc `BuyHeavy` (mua áp đảo).
Ngưỡng mặc định 80% (`MarketData:VolumeAlertExcessPercent`), tức một phía chiếm ≥ ~64% tổng khối lượng.
Dashboard hiện hộp cảnh báo cho phiên vừa đóng và phiên đang chạy. Nến cũ được tải lại để bổ sung số liệu này.

## Bảng giá trực tiếp (mỗi 15 giây)

Panel trên cùng dashboard: mỗi `LivePrices:PollInterval` (mặc định 15s) backend lấy **Spot** (`/api/v3/ticker/price`) và **Mark Futures** (`/fapi/v1/premiumIndex`) song song cho từng symbol trong `LivePrices:Symbols`.
Hiển thị giá, % đổi so với lần trước, chênh lệch Mark − Spot, độ trễ API từng nguồn, trạng thái OK/Lỗi, đếm ngược lần lấy tiếp theo và cảnh báo "Chậm" nếu quá 2 chu kỳ.
Một nguồn lỗi thì chỉ nguồn đó để trống kèm lý do, không điền giá cũ. Lịch sử (240 mẫu ≈ 1 giờ) giữ trong bộ nhớ, mất khi restart. Endpoint: `GET /api/live/prices?limit=`.

**Biên độ 24 giờ** (dưới giá Spot): lấy 90 nến ngày đã đóng (`/api/v3/klines`, cache 15 phút), tính độ lệch chuẩn σ của log return
một ngày (`CryptoBot.Domain/MarketData/DailyMoveRange.cs`). Khoảng `giá × e^(±σ)` ≈ 68% số ngày, `giá × e^(±2σ)` ≈ 95%.
Kèm % ngày tăng, ngày tăng/giảm mạnh nhất. Chỉ là biên độ, không đoán hướng. Endpoint: `GET /api/live/daily-range?symbol=&days=`.
Nút **24 giờ / 7 ngày / 30 ngày**: mốc N ngày dùng σ của 365 ngày × √N (giả định các ngày độc lập), số liệu lịch sử tính trên
các đoạn N ngày trượt từng ngày trong một năm.
