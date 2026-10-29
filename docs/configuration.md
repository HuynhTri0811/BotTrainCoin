# Cấu hình

Cấu hình đọc từ [`src/CryptoBot.Api/appsettings.json`](../src/CryptoBot.Api/appsettings.json) và
[`marketevents.json`](../src/CryptoBot.Api/marketevents.json). **Đổi cấu hình cần restart API.**
Có thể ghi đè bằng biến môi trường theo quy ước .NET, ví dụ `MarketData__PollInterval=00:00:30`.

Định dạng: `TimeSpan` là `"hh:mm:ss"`, số thập phân dùng dấu chấm, ngày `"yyyy-MM-dd"`.
Cột "Mặc định" là giá trị trong code khi không khai báo; giá trị trong `appsettings.json` hiện tại có thể khác.

## ConnectionStrings

| Khóa | Hiện tại | Ghi chú |
| --- | --- | --- |
| `Bot` | `Data Source=data/cryptobot.db;Default Timeout=30` | Đường dẫn tương đối với thư mục chạy API. Thiếu thì dùng `Data Source=data/cryptobot.db` |

## Serilog

Mức log mặc định `Information`; `Microsoft.AspNetCore`, `Microsoft.EntityFrameworkCore`, `System.Net.Http.HttpClient`
hạ xuống `Warning` để log không bị ngập.

## Binance — `BinanceOptions`

| Khóa | Mặc định | Ý nghĩa |
| --- | --- | --- |
| `MarketDataBaseUrl` | `https://api.binance.com` | REST Spot công khai |
| `FuturesMarketDataBaseUrl` | `https://fapi.binance.com` | REST USDⓈ-M công khai (mark price) |
| `RequestTimeout` | `00:00:10` | Timeout mỗi request |

## MarketData — `MarketDataOptions` ([`CandleSyncService.cs`](../src/CryptoBot.Application/MarketData/CandleSyncService.cs))

| Khóa | Mặc định | Ý nghĩa |
| --- | --- | --- |
| `BootstrapCandles` | 1000 | Số nến tải lần đầu khi DB chưa có dữ liệu; cũng là cửa sổ kiểm tra nến thiếu thống kê |
| `BatchSize` | 1000 | Số nến mỗi request (Binance tối đa 1000) |
| `MaxBatchesPerSync` | 20 | Số batch tối đa mỗi lần sync |
| `PollInterval` | `00:00:15` | Chu kỳ worker đồng bộ nến + đánh giá chiến lược |
| `StaleAfter` | `00:02:00` | Quá thời gian này không sync được → feed `isStale` |
| `VolumeAlertExcessPercent` | 80 | Một phía mua/bán chủ động vượt phía kia bao nhiêu % thì cảnh báo |

## Signals — `SignalOptions` ([`SignalEvaluationService.cs`](../src/CryptoBot.Application/Signals/SignalEvaluationService.cs))

| Khóa | Mặc định | Ý nghĩa |
| --- | --- | --- |
| `MaxFreshAge` | `00:02:00` | Tín hiệu tạo quá lâu sau khi nến đóng → `IsBackfill = true` |
| `MaxCandlesPerRun` | 2000 | Số nến đóng tối đa đánh giá mỗi lần chạy |

## Strategies — danh sách `StrategyDefinition` ([`StrategyConfigurator.cs`](../src/CryptoBot.Application/Signals/StrategyConfigurator.cs))

```json
{ "Name": "btc-1h-ema", "Type": "EmaCross", "Symbol": "BTCUSDT", "Interval": "1h", "Enabled": true,
  "Parameters": { "FastPeriod": 9, "SlowPeriod": 21, "LookbackCandles": 300 } }
```

| Khóa | Mặc định | Ý nghĩa |
| --- | --- | --- |
| `Name` | (bắt buộc) | Tên duy nhất, khóa để tìm instance trong DB. **Đổi tên = tạo instance mới** |
| `Type` | `EmaCross` | Hiện chỉ có `EmaCross` |
| `Exchange` | `Binance` | |
| `Symbol` | (bắt buộc) | Chỉ chữ/số ASCII |
| `Interval` | `1h` | `15m`, `1h`, `1d` |
| `Enabled` | `true` | `false` → worker không đánh giá, dashboard không hiện |
| `Parameters.FastPeriod` | 9 | ≥ 1 |
| `Parameters.SlowPeriod` | 21 | > `FastPeriod` |
| `Parameters.LookbackCandles` | 300 | ≥ `SlowPeriod + 1`; cửa sổ cố định |

Đổi `Type`/`Parameters`/`Exchange`/`Symbol`/`Interval` → `Version + 1`, đánh giá lại lịch sử. Chỉ đổi `Enabled` →
không tăng version. Cấu hình sai bị bỏ qua và ghi log lỗi.

## Indicators — danh sách `IndicatorDefinition` ([`IndicatorRegistry.cs`](../src/CryptoBot.Application/Indicators/IndicatorRegistry.cs))

```json
{ "Name": "eth-15m-myrsi-trend", "Type": "MyRsi", "Symbol": "ETHUSDT", "Interval": "15m",
  "Parameters": { "Period": 14, "Oversold": 30, "Overbought": 70, "TrendEmaPeriod": 50, "LookbackCandles": 200 } }
```

| Khóa | Mặc định | Ý nghĩa |
| --- | --- | --- |
| `Name` | (bắt buộc) | Tên duy nhất (không phân biệt hoa thường), dùng trong URL và quy tắc tự động |
| `Type` | (bắt buộc) | Tên trong `[Indicator("...")]`, ví dụ `MyRsi` |
| `Exchange` | `Binance` | |
| `Symbol`, `Interval` | — / `15m` | Cặp nến được đồng bộ kể cả khi không chiến lược nào dùng |
| `Enabled` | `true` | |
| `Parameters` | `{}` | Tên property public của class chỉ báo → giá trị. Không phân biệt hoa thường |

Tham số của `MyRsi`: `Period` (14), `Oversold` (30), `Overbought` (70), `TrendEmaPeriod` (0 = tắt),
`PullbackLong` (40), `PullbackShort` (60), `LookbackCandles` (200).
Lỗi (loại không tồn tại, tham số sai tên/kiểu, `Validate()` trả lỗi, interval sai) hiện trong
`GET /api/indicators` → `errors` và log.

## PaperFutures — `PaperFuturesOptions` ([`PaperFuturesContracts.cs`](../src/CryptoBot.Application/Futures/PaperFuturesContracts.cs))

| Khóa | Mặc định | Ý nghĩa |
| --- | --- | --- |
| `StartingBalance` | 1000 | Vốn giả lập (USDT), nạp một lần |
| `TakerFeeRate` | 0.0005 | Phí taker mỗi chiều (0.05%) |
| `SlippageRate` | 0.0005 | Trượt giá bất lợi khi khớp |
| `MaxLeverage` | 10 | Đòn bẩy tối đa |
| `MarkPriceMaxAge` | `00:00:10` | Mark cũ hơn → chặn mở/đóng/trigger |
| `MarkPollInterval` | `00:00:02` | Chu kỳ `PaperFuturesWorker` |
| `Symbols[]` | `[]` | Quy tắc từng symbol: `Symbol`, `QuantityStep`, `TickSize`, `MinNotional`, `MaintenanceMarginRate`. Symbol không có ở đây thì không giao dịch được |

Giá trị hiện tại: BTCUSDT (step 0.001, tick 0.1, min 50, mmr 0.004), ETHUSDT (step 0.001, tick 0.01, min 20,
mmr 0.005). Cần kiểm tra lại với `exchangeInfo` của Binance khi tích hợp testnet.

## AutoOrders — `AutoOrderOptions` ([`AutoOrderService.cs`](../src/CryptoBot.Application/Futures/AutoOrderService.cs))

| Khóa | Mặc định | Ý nghĩa |
| --- | --- | --- |
| `DefaultMargin` | 50 | Giá trị điền sẵn trên form |
| `DefaultLeverage` | 2 | |
| `DefaultStopLossPercent` | 1 | |
| `DefaultTakeProfitPercent` | 2 | |
| `DailyLossLimitPercent` | 5 | Lỗ thực hiện trong ngày UTC ≥ mức này × vốn đầu ngày → chặn kích hoạt |
| `MaxActiveRules` | 10 | Số quy tắc `Active` tối đa |

## LivePrices — `LivePriceOptions` ([`LivePrices.cs`](../src/CryptoBot.Application/LivePrices/LivePrices.cs))

| Khóa | Mặc định | Ý nghĩa |
| --- | --- | --- |
| `PollInterval` | `00:00:15` | Chu kỳ lấy Spot + Mark |
| `HistorySize` | 240 | Số mẫu giữ trong bộ nhớ mỗi symbol (240 × 15s = 1 giờ) |
| `Symbols` | `[]` | Hiện tại `BTCUSDT`, `ETHUSDT`. Cũng là danh sách symbol hợp lệ cho `/api/live/daily-range` |

## Bitfinex — `BitfinexOptions`

| Khóa | Mặc định | Ý nghĩa |
| --- | --- | --- |
| `BaseUrl` | `https://api-pub.bitfinex.com` | REST công khai |
| `RequestTimeout` | `00:00:20` | |

## DailyHistory — `DailyHistoryOptions` ([`DailyHistorySyncService.cs`](../src/CryptoBot.Application/MarketData/DailyHistorySyncService.cs))

| Khóa | Mặc định | Ý nghĩa |
| --- | --- | --- |
| `StartDate` | `2016-01-01` | Ngày bắt đầu tải khi DB chưa có symbol |
| `PollInterval` | `01:00:00` | Chu kỳ đồng bộ |
| `BatchSize` | 10000 | Bitfinex tối đa 10000 nến/request |
| `Symbols` | `[]` | Ký hiệu Bitfinex: hiện tại `BTCUSD`, `ETCUSD`, `ETHUSD`. `BTCUSDT` (bảng giá) được nối với `BTCUSD` cho dự báo ngày tương tự |

## MarketEvents — `marketevents.json`

```json
{ "MarketEvents": [
  { "Month": "2017-12", "Symbols": [], "Title": "Ra mắt hợp đồng tương lai Bitcoin, đỉnh 2017",
    "Detail": "10/12 CBOE và 17/12 CME mở hợp đồng tương lai; ..." }
] }
```

| Khóa | Ý nghĩa |
| --- | --- |
| `Month` | `yyyy-MM`. Sai định dạng → app không khởi động (lỗi chỉ rõ sự kiện) |
| `Symbols` | Ký hiệu `DailyHistory` (`ETHUSD`…). Rỗng = sự kiện chung cả thị trường |
| `Title` | Bắt buộc |
| `Detail` | Tùy chọn, hiện khi mở rộng |

Một tháng có thể có nhiều sự kiện. Viết ngắn, có ngày cụ thể, chỉ nêu sự kiện cùng tháng, không khẳng định nguyên nhân.

## Thêm một khóa cấu hình mới

1. Thêm property (có giá trị mặc định và `/// <summary>`) vào options class cạnh service dùng nó.
2. Nếu là section mới: đăng ký trong `DependencyInjection.cs`
   (`services.AddSingleton(config.GetSection("X").Get<XOptions>() ?? new XOptions());`).
3. Thêm vào `appsettings.json` nếu giá trị khác mặc định hoặc người dùng hay cần chỉnh.
4. Thêm dòng vào bảng trong file này.
