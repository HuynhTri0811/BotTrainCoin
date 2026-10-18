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

Dashboard dev: http://localhost:5173 (proxy `/api` sang http://localhost:5080).
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
