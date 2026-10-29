# API REST

Base URL khi chạy dev: `http://localhost:5080`. Dashboard dev (`:8090`) proxy `/api` và `/health` sang đây.
Kiểu TypeScript tương ứng nằm trong [`api/client.ts`](../src/cryptobot-dashboard/src/api/client.ts).
OpenAPI (môi trường Development): `GET /openapi/v1.json`.

## Quy ước chung

- JSON camelCase. Enum là chuỗi (`"Long"`, `"Buy"`, `"Active"`…).
- Thời gian: ISO 8601 UTC (`...Utc`), riêng dữ liệu chart dùng **Unix seconds** (`time`), open time của nến.
- Số tiền/giá là JSON number khi trả về; khi gửi lên có thể là number hoặc chuỗi (`"50.5"`).
- `symbol` chỉ chữ/số ASCII, không phân biệt hoa thường. `interval`: `15m`, `1h`, `1d`.
- `limit` luôn bị kẹp vào khoảng cho phép (ghi trong bảng), không báo lỗi khi vượt.
- Lỗi:

| Mã | Khi nào | Body |
| --- | --- | --- |
| 400 | Sai định dạng (symbol, interval, side, thiếu `commandId`) | `{ "error": "..." }` hoặc `{ "errors": [...] }` |
| 404 | Không có dữ liệu / tài nguyên, hoặc `/api/...` không tồn tại | `{ "error": "..." }` |
| 422 | Đúng định dạng nhưng nghiệp vụ từ chối | `{ "errors": [...] }`, `{ "error": "..." }` hoặc DTO có `errors` |
| 503 | `/health/ready` khi không kết nối được DB | `{ "status": "db-unavailable" }` |

## Trạng thái và chiến lược — [`BotEndpoints.cs`](../src/CryptoBot.Api/Endpoints/BotEndpoints.cs)

| Method | Đường dẫn | Tham số | Trả về |
| --- | --- | --- | --- |
| GET | `/api/status` | — | `decisionMode`, `environment`, `liveLocked`, `serverTimeUtc`, `feeds[]` (trạng thái đồng bộ từng cặp của chiến lược), `strategies[]` |
| GET | `/api/strategies` | — | Danh sách strategy instance (kèm `version`, `parameters`, checkpoint) |
| GET | `/api/strategies/{id}/chart` | `limit` (300, 1–1000) | `strategy`, `candles[]` (OHLCV, `fastEma`, `slowEma`, `pressure`), `signals[]` |
| GET | `/api/signals` | `strategyId?`, `limit` (100, 1–1000) | Tín hiệu BUY/SELL, cũ → mới, có `isBackfill` |
| GET | `/api/market/candles` | `symbol`, `interval`, `limit` (300, 1–1000) | Nến Binance (gồm nến đang chạy) |
| GET | `/health/live` | — | `{ "status": "live" }` |
| GET | `/health/ready` | — | `{ "status": "ready" }` hoặc 503 |

`pressure` của nến: `buyVolume`, `sellVolume`, `buyPercent`, `excessPercent` (null khi một phía = 0), `side`
(`Balanced`/`BuyHeavy`/`SellHeavy`), `alertExcessPercent`. `null` với nến chưa có khối lượng mua chủ động.

## Paper Futures — [`PaperFuturesEndpoints.cs`](../src/CryptoBot.Api/Endpoints/PaperFuturesEndpoints.cs)

Tiền tố `/api/paper/futures`.

| Method | Đường dẫn | Body / tham số | Trả về |
| --- | --- | --- | --- |
| GET | `/account` | — | Số dư ví, margin đang dùng, PnL chưa thực hiện, equity, khả dụng, `openPositions[]`, `markPrices[]`, `rules` (đòn bẩy tối đa, phí, slippage, symbols) |
| POST | `/quote` | `OpenRequest` | Báo giá: `accepted`, `errors[]`, giá khớp, khối lượng, notional, margin, phí mở, giá thanh lý, lỗ tại SL, lãi tại TP |
| POST | `/orders` | `OpenRequest` (bắt buộc `commandId`) | 200 `{ accepted, duplicate, errors, position }` hoặc 422 với `errors` |
| POST | `/positions/{id}/close` | `{ "commandId": "guid" }` | 200 vị thế đã đóng, hoặc 422 `{ error }` |
| GET | `/positions/history` | `limit` (50, 1–500) | Vị thế đã đóng/thanh lý, kèm `netPnl` |

`OpenRequest`:

```json
{
  "commandId": "3f1c…",      // GUID do client tạo, gửi lại cùng GUID không mở lần hai
  "symbol": "BTCUSDT",
  "side": "Long",            // Long | Short
  "leverage": 5,
  "margin": "50",
  "stopLoss": "60000",       // bắt buộc, phải nằm trước giá thanh lý
  "takeProfit": "70000"      // tùy chọn
}
```

## Quy tắc tự mở lệnh — [`AutoOrderEndpoints.cs`](../src/CryptoBot.Api/Endpoints/AutoOrderEndpoints.cs)

Tiền tố `/api/paper/futures/auto-rules`.

| Method | Đường dẫn | Body | Trả về |
| --- | --- | --- | --- |
| GET | `` (gốc) | — | `rules[]`, `defaults` (margin, đòn bẩy, SL%, TP%, giới hạn lỗ ngày, số quy tắc tối đa), `risk` (vốn đầu ngày, PnL hôm nay, mức lỗ tối đa, `blocked`) |
| POST | `` (gốc) | `CreateRule` | 200 quy tắc, 400 khi sai enum, 422 `{ errors }` khi bị từ chối |
| POST | `/{id}/cancel` | — | `{ "cancelled": true }` hoặc 422 |

`CreateRule`:

```json
// Theo giá
{ "symbol": "BTCUSDT", "side": "Long", "triggerType": "Price", "condition": "AtOrBelow",
  "triggerPrice": "62000", "margin": "50", "leverage": 2, "stopLossPercent": 1, "takeProfitPercent": 2 }

// Theo chỉ báo, tự đóng khi đảo chiều
{ "symbol": "BTCUSDT", "side": "Long", "triggerType": "Indicator", "indicatorName": "btc-1h-myrsi",
  "margin": "50", "leverage": 2, "stopLossPercent": 1, "takeProfitPercent": 2, "closeOnReverse": true }
```

Quy tắc trả về có `status` (`Active`/`Triggered`/`Rejected`/`Cancelled`), `note` (lý do bị chặn/từ chối),
`triggerDetail` (giá trị chỉ báo lúc kích hoạt), `positionId`, `closeWatchEndedAtUtc`, `closeDetail`.

## Chỉ báo tự viết — [`IndicatorEndpoints.cs`](../src/CryptoBot.Api/Endpoints/IndicatorEndpoints.cs)

| Method | Đường dẫn | Tham số | Trả về |
| --- | --- | --- | --- |
| GET | `/api/indicators` | — | `indicators[]` (tên, loại, mô tả, symbol, interval, `requiredCandles`, `lines[]`), `errors[]` (cấu hình bị bỏ qua), `availableTypes[]` |
| GET | `/api/indicators/{name}/chart` | `limit` (300, 1–1000) | `indicator`, `points[]` (`time`, `values` theo `lines.key`, `signal`), `markers[]` (nến có tín hiệu + lý do), `latestReason` |

## Giá trực tiếp và biên độ — [`LivePriceEndpoints.cs`](../src/CryptoBot.Api/Endpoints/LivePriceEndpoints.cs)

| Method | Đường dẫn | Tham số | Trả về |
| --- | --- | --- | --- |
| GET | `/api/live/prices` | `limit` (60, 1–`HistorySize`) | `intervalSeconds`, `serverTimeUtc`, `symbols[]` mỗi symbol có `samples[]` (Spot, Mark, độ trễ, lỗi) |
| GET | `/api/live/daily-range` | `symbol` (phải có trong `LivePrices:Symbols`), `days` (1, 1–90) | Biên độ ±1σ/±2σ, % ngày tăng, tăng/giảm mạnh nhất; `similarDays` (chỉ khi `days = 1`, có thể `null`) |

## Dự đoán — [`PredictionEndpoints.cs`](../src/CryptoBot.Api/Endpoints/PredictionEndpoints.cs)

| Method | Đường dẫn | Tham số | Trả về |
| --- | --- | --- | --- |
| GET | `/api/prediction` | `symbol`, `interval` (`1d` = nến ngày Binance), `history` (300, 1–1000) | Dự đoán nến kế tiếp (`direction`, `score`, `reason`), kết quả chạy thử (`backtestPredictions`, `backtestCorrect`, `hitRatePercent`), 20 lần đoán gần nhất (`recent[]`), `settings` |
| GET | `/api/prediction/indicators` | `symbol`, `interval`, `history` (1000) | Mỗi chỉ báo cùng symbol/khung: tỷ lệ đúng sau 1/3/6/12 nến (`horizons[]`), các tín hiệu gần nhất kèm % đi sau 1 và 3 nến |

## Lịch sử theo tháng — [`HistoryEndpoints.cs`](../src/CryptoBot.Api/Endpoints/HistoryEndpoints.cs)

| Method | Đường dẫn | Tham số | Trả về |
| --- | --- | --- | --- |
| GET | `/api/history/monthly` | `symbol?` (mặc định symbol đầu tiên của `DailyHistory:Symbols`) | `months[]`, `years[]`, `monthStats[]`, `events[]`, `firstDayUtc`, `lastDayUtc`, `symbols[]` |

## Thêm endpoint mới

Xem [extending.md → Thêm endpoint](extending.md#3-thêm-một-endpoint) và cập nhật bảng trong file này.
