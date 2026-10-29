# Tổ chức thư mục

Tài liệu này trả lời hai câu hỏi: **file X nằm ở đâu** và **code mới nên đặt ở đâu**. Phần cuối là bảng quyết định nhanh.

## Toàn cảnh

```text
BotTrainCoin/
├── CryptoTradingBot.sln          # Solution: 4 project src + 3 project test
├── dotnet-tools.json             # Tool cục bộ: dotnet-ef 10.0.13 (chạy `dotnet tool restore`)
├── README.md                     # Trang vào: chạy nhanh + link tới docs
├── docs/                         # Tài liệu (file này)
├── data/                         # Dữ liệu cục bộ (CSV, backup .db) — bị .gitignore, không commit
├── src/
│   ├── CryptoBot.Domain/         # Nghiệp vụ thuần túy, không phụ thuộc gì
│   ├── CryptoBot.Application/    # Service điều phối + interface (port)
│   ├── CryptoBot.Infrastructure/ # EF Core/SQLite, client sàn, đăng ký DI
│   ├── CryptoBot.Api/            # ASP.NET Core: endpoint, worker, cấu hình
│   └── cryptobot-dashboard/      # React + Vite
└── tests/
    ├── CryptoBot.Domain.Tests/
    ├── CryptoBot.Application.Tests/
    └── CryptoBot.IntegrationTests/
```

Không commit: `bin/`, `obj/`, `data/`, `*.db*`, `src/CryptoBot.Api/wwwroot/` (bản build dashboard), `node_modules/`,
`*.tsbuildinfo` (xem `.gitignore`).

## Thư mục con chia theo **tính năng**, giống nhau ở mọi tầng

Đây là quy tắc quan trọng nhất. Mỗi tầng có cùng tên thư mục cho cùng một mảng nghiệp vụ, nên đi từ endpoint xuống
domain rất dễ đoán:

| Mảng | Domain | Application | Api / Endpoints | Dashboard |
| --- | --- | --- | --- | --- |
| Nến, dữ liệu thị trường | `MarketData/` | `MarketData/` | `BotEndpoints.cs`, `HistoryEndpoints.cs` | `features/market/`, `features/history/` |
| Chiến lược, tín hiệu | `Strategies/` | `Signals/` | `BotEndpoints.cs` | `pages/SignalsPage.tsx` |
| Chỉ báo tự viết | `Indicators/` | `Indicators/` | `IndicatorEndpoints.cs` | (trong `features/market/CandleChart.tsx`) |
| Paper futures + lệnh tự động | `Futures/` | `Futures/` | `PaperFuturesEndpoints.cs`, `AutoOrderEndpoints.cs` | `features/futures/`, `features/auto/` |
| Giá trực tiếp, biên độ, ngày tương tự | `MarketData/` (công thức) | `LivePrices/` | `LivePriceEndpoints.cs` | `features/live/` |
| Dự đoán, độ chính xác | `Prediction/` | — | `PredictionEndpoints.cs` | `features/prediction/` |
| Dùng chung | `Common/` | `Abstractions/` | — | `components/`, `time.ts` |

Không có thư mục kiểu `Models/`, `Services/`, `Helpers/`, `Utils/` gom theo loại kỹ thuật. Service, record, enum và
interface của cùng một tính năng nằm cạnh nhau.

---

## `src/CryptoBot.Domain` — nghiệp vụ thuần túy

```text
CryptoBot.Domain/
├── Common/
│   ├── ListSlice.cs              # View read-only trên một đoạn danh sách, không sao chép (cửa sổ nến trượt)
│   └── PercentChange.cs          # internal: % thay đổi Between(from, to), UpShare
├── MarketData/
│   ├── Candle.cs                 # Entity nến OHLCV + ApplyUpdate (không ghi đè nến đã đóng) + Validate
│   ├── CandleInterval.cs         # 15m / 1h / 1d; TryParse/Parse; OpenTimeAt (nến chứa một thời điểm)
│   ├── CandleSeriesValidator.cs  # Trùng / sai thứ tự / thiếu / OHLCV không hợp lệ; WithoutRejected (bỏ nến hỏng)
│   ├── DailyCloses.cs            # internal: kiểm tra chung cho chuỗi giá đóng ngày
│   ├── DailyMoveRange.cs         # Biên độ ±1σ/±2σ sau N ngày
│   ├── MonthlyChangeReport.cs    # % theo tháng/năm, thống kê theo tháng trong năm
│   ├── SimilarDaysForecast.cs    # Dự báo "ngày tương tự" + backtest walk-forward
│   └── VolumePressure.cs         # Mua/bán chủ động, cảnh báo áp đảo
├── Strategies/
│   ├── StrategyContracts.cs      # IStrategy, StrategyContext, StrategyDecision, SignalAction
│   ├── EmaCrossStrategy.cs       # Chiến lược EMA cross (+ EmaCrossParameters)
│   ├── Ema.cs                    # EMA: Calculate (cả chuỗi), LastTwo (2 giá trị cuối, không cấp phát)
│   ├── Crossover.cs              # Up/Down (2 đường), UpThrough/DownThrough (qua mức cố định)
│   ├── StrategyInstance.cs       # Entity: cấu hình + Version + checkpoint
│   └── TradingSignal.cs          # Entity: tín hiệu BUY/SELL đã phát
├── Indicators/
│   ├── IndicatorContracts.cs     # ICustomIndicator, [Indicator], IndicatorResult, IndicatorLine, IndicatorWindow
│   ├── Rsi.cs                    # RSI Wilder: Calculate, LastTwo
│   ├── MyRsiIndicator.cs         # Chỉ báo mẫu đầy đủ (đọc file này trước khi viết chỉ báo mới)
│   └── TemplateIndicator.cs      # Khung trống để copy (không có [Indicator] nên không được đăng ký)
├── Futures/
│   ├── FuturesMath.cs            # Làm tròn, slippage, giá thanh lý, PnL, SL/TP hit; PositionSide; FuturesSymbolRules
│   ├── FuturesPosition.cs        # Entity vị thế + PaperLedgerEntry + các enum trạng thái
│   └── AutoOrderRule.cs          # Entity quy tắc tự mở lệnh + trạng thái
└── Prediction/
    ├── PriceDirection.cs         # enum Up / Down
    ├── PriceDirectionPredictor.cs# Predict, PredictAt, Backtest + PredictorSettings
    └── SignalAccuracy.cs         # Chấm tín hiệu sau 1/3/6/12 nến
```

Đặt gì ở đây:

- Entity (class có `private set`, constructor kiểm tra bất biến, constructor rỗng `private` cho EF Core).
- Công thức và quy tắc nghiệp vụ dưới dạng `static class` hoặc `record` có `static` factory (`Compute`, `Build`, `From`).
- Chiến lược (`IStrategy`) và chỉ báo (`ICustomIndicator`).

Không đặt ở đây: bất cứ thứ gì cần `await`, `ILogger`, `HttpClient`, `DbContext`, `DateTime.UtcNow`, cấu hình.

Một file có thể chứa vài kiểu nhỏ đi cùng nhau (ví dụ `SimilarDaysForecast` + `SimilarDaysBacktest`,
`FuturesPosition` + `PaperLedgerEntry`). Tách kiểu ra file riêng khi **nhiều file khác** cùng dùng nó (ví dụ
`PriceDirection` được tách ở PR #57 vì cả `PriceDirectionPredictor` và `SignalAccuracy` dùng).

## `src/CryptoBot.Application` — điều phối

```text
CryptoBot.Application/
├── Abstractions/
│   └── Ports.cs                  # IClock, CandleKey, KlineDto, IMarketDataClient, IDailyHistoryClient,
│                                 # ICandleStore, IStrategyInstanceStore, ISignalStore, MarketDataUnavailableException
├── MarketData/
│   ├── CandleSyncService.cs      # + MarketDataOptions, SyncResult
│   ├── DailyHistorySyncService.cs# + DailyHistoryOptions
│   ├── MarketDataStatusRegistry.cs # Trạng thái feed trong bộ nhớ cho /api/status
│   └── MarketEvents.cs           # MarketEvent, MarketEventCatalog
├── Signals/
│   ├── SignalEvaluationService.cs# + SignalOptions
│   ├── ChartQueryService.cs      # Dữ liệu chart chiến lược (EMA theo đúng cửa sổ)
│   ├── StrategyConfigurator.cs   # + StrategyDefinition (một mục trong "Strategies")
│   └── StrategyFactory.cs        # StrategyTypes, tạo IStrategy từ StrategyInstance
├── Indicators/
│   ├── IndicatorRegistry.cs      # + IndicatorDefinition; tìm [Indicator] bằng reflection, gán tham số
│   └── IndicatorService.cs       # Chart chỉ báo, đọc giá trị mới nhất, GapIndex
├── Futures/
│   ├── PaperFuturesContracts.cs  # Options, IMarkPriceClient, MarkPriceCache, PaperFuturesLock, IPaperFuturesStore, DTO nội bộ
│   ├── PaperFuturesService.cs    # Quote / Open / Close / EvaluateTriggers / Account
│   └── AutoOrderService.cs       # + AutoOrderOptions, IAutoOrderStore
└── LivePrices/
    ├── LivePrices.cs             # LivePriceOptions, LivePriceSample, LivePriceHistory, LivePriceSampler
    ├── DailyRangeService.cs      # Nến ngày Binance (cache) → DailyMoveRange
    └── SimilarDaysService.cs     # SimilarDaysCache, SimilarDaysResult, SimilarDaysService
```

Quy ước:

- **Options class nằm cùng file với service dùng nó** (ví dụ `MarketDataOptions` ở đầu `CandleSyncService.cs`).
- Interface (port) nằm ở `Abstractions/Ports.cs` nếu dùng chung nhiều mảng; port chỉ một mảng dùng thì nằm trong
  file contracts của mảng đó (ví dụ `IPaperFuturesStore` trong `PaperFuturesContracts.cs`, `IAutoOrderStore` trong
  `AutoOrderService.cs`).
- Service là `sealed class` với primary constructor nhận dependency.

## `src/CryptoBot.Infrastructure` — kỹ thuật

```text
CryptoBot.Infrastructure/
├── DependencyInjection.cs        # AddInfrastructure(): đăng ký MỌI service/store/client/options; InitializeDatabaseAsync; SystemClock
├── Exchanges/
│   ├── Binance/
│   │   ├── BinanceMarketDataClient.cs      # IMarketDataClient (+ BinanceOptions)
│   │   └── BinanceFuturesMarkPriceClient.cs# IMarkPriceClient
│   └── Bitfinex/
│       └── BitfinexDailyHistoryClient.cs   # IDailyHistoryClient (+ BitfinexOptions)
└── Persistence/
    ├── BotDbContext.cs           # DbSet, mapping, converter decimal→TEXT, DateTime→Unix ms
    ├── Stores.cs                 # CandleStore, StrategyInstanceStore, SignalStore, PaperFuturesStore, AutoOrderStore
    └── Migrations/               # Sinh bởi dotnet-ef, KHÔNG sửa tay (trừ khi thật cần và có ghi chú)
```

Mỗi sàn mới là một thư mục trong `Exchanges/`. Mọi đăng ký DI nằm trong một chỗ là `DependencyInjection.cs`,
có chú thích theo nhóm tính năng.

## `src/CryptoBot.Api` — điểm vào

```text
CryptoBot.Api/
├── Program.cs                    # Khởi động, middleware, map endpoint (xem architecture.md §2)
├── appsettings.json              # Cấu hình chính (configuration.md)
├── appsettings.Development.json  # Trống
├── marketevents.json             # Sự kiện thị trường ghi tay
├── Properties/launchSettings.json# Profile http: http://localhost:5080
├── Endpoints/
│   ├── BotEndpoints.cs           # /api/status, /strategies, /signals, /market/candles, /health/*
│   ├── PaperFuturesEndpoints.cs  # /api/paper/futures/*
│   ├── AutoOrderEndpoints.cs     # /api/paper/futures/auto-rules/*
│   ├── IndicatorEndpoints.cs     # /api/indicators/*
│   ├── LivePriceEndpoints.cs     # /api/live/*
│   ├── PredictionEndpoints.cs    # /api/prediction, /api/prediction/indicators
│   └── HistoryEndpoints.cs       # /api/history/monthly
├── Workers/                      # 4 BackgroundService (architecture.md §3)
├── data/                         # cryptobot.db khi chạy từ thư mục này (bị .gitignore)
└── wwwroot/                      # Bản build dashboard (bị .gitignore)
```

Mỗi file endpoint là một `static class XxxEndpoints` có extension `MapXxxEndpoints(this IEndpointRouteBuilder app)`.
**DTO của endpoint nằm ở cuối chính file đó** (`public sealed record XxxDto(...)`), mapping `ToDto` là `private static`
trong class.

## `src/cryptobot-dashboard` — frontend

```text
cryptobot-dashboard/
├── index.html
├── package.json                  # Phiên bản cố định (không dùng ^ hay ~)
├── tsconfig.json
├── vite.config.ts                # Cổng 8090, proxy /api + /health → 5080, build ra ../CryptoBot.Api/wwwroot
└── src/
    ├── main.tsx                  # QueryClient, ConfigProvider vi_VN
    ├── App.tsx                   # Layout, menu 5 trang (#hash), chọn chiến lược
    ├── time.ts                   # Định dạng giờ Việt Nam (formatVn, formatVnUnix, ago…)
    ├── api/
    │   └── client.ts             # MỌI kiểu dữ liệu REST + các object gọi API (api, futuresApi, autoApi…)
    ├── components/
    │   └── StatusBar.tsx         # Component dùng ở nhiều trang
    ├── pages/                    # Mỗi trang một file, chỉ ghép các card
    │   ├── MarketPage.tsx  TradingPage.tsx  PredictionPage.tsx  SignalsPage.tsx  HistoryPage.tsx
    └── features/                 # Card/biểu đồ theo tính năng
        ├── market/               # CandleChart, SessionStatsCard
        ├── live/                 # LivePricePanel, DailyRangeBar, Sparkline
        ├── futures/              # TradePanel, PositionsCard, format.ts (định dạng tiền/giá dùng chung)
        ├── auto/                 # AutoOrdersCard
        ├── prediction/           # PredictionCard, IndicatorAccuracyCard
        └── history/              # MonthlyHeatmap, MoveEventsCard
```

- `pages/` chỉ bố cục (Row/Col/Space) và truyền `symbol`/`interval`/`strategy` xuống. Gọi API nằm trong card ở
  `features/`.
- Hàm định dạng dùng chung đặt cạnh tính năng đầu tiên dùng nó (`features/futures/format.ts`) và được import từ nơi
  khác. Nếu sau này dùng rộng hơn thì chuyển sang `src/` gốc như `time.ts`.

## `tests/`

```text
tests/
├── CryptoBot.Domain.Tests/       # Tham chiếu chỉ Domain. Một file test cho mỗi kiểu: <Kiểu>Tests.cs
│   └── TestCandles.cs            # Helper tạo nến (At, FromCloses)
├── CryptoBot.Application.Tests/  # Tham chiếu Application. Fake trong bộ nhớ
│   ├── Fakes.cs                  # FakeClock, InMemoryCandleStore/StrategyInstanceStore/SignalStore, FakeMarketDataClient
│   ├── SignalGoldenTests.cs      # Khóa đầu ra hàm sinh tín hiệu
│   └── Golden/signal_outputs.txt # Snapshot 3,026 dòng
└── CryptoBot.IntegrationTests/   # Tham chiếu Infrastructure. SQLite file thật + migration thật
```

Chi tiết: [testing.md](testing.md).

---

## Bảng quyết định: code mới đặt ở đâu?

| Bạn đang viết… | Đặt ở | Ví dụ có sẵn |
| --- | --- | --- |
| Công thức tính từ dữ liệu có sẵn (không I/O) | `Domain/<Mảng>/` | `DailyMoveRange`, `FuturesMath` |
| Chỉ báo mới | `Domain/Indicators/<Tên>Indicator.cs` | `MyRsiIndicator` |
| Loại chiến lược mới | `Domain/Strategies/` + nhánh mới trong `StrategyFactory` | `EmaCrossStrategy` |
| Entity mới lưu DB | `Domain/<Mảng>/` + `DbSet` và mapping trong `BotDbContext` + migration | `AutoOrderRule` |
| Helper thuần túy dùng ở ≥ 2 mảng Domain | `Domain/Common/` | `ListSlice`, `PercentChange` |
| Việc cần đọc DB / gọi sàn / log / đồng hồ | `Application/<Mảng>/<Tên>Service.cs` | `SimilarDaysService` |
| Interface tới thế giới bên ngoài | `Application/Abstractions/Ports.cs` (hoặc contracts của mảng) | `ICandleStore` |
| Cài đặt interface bằng EF Core | `Infrastructure/Persistence/Stores.cs` | `CandleStore` |
| Client cho sàn mới | `Infrastructure/Exchanges/<Sàn>/` | `BitfinexDailyHistoryClient` |
| Đăng ký DI | `Infrastructure/DependencyInjection.cs` (đúng nhóm) | — |
| Endpoint REST | `Api/Endpoints/<Mảng>Endpoints.cs`, DTO cuối file, map trong `Program.cs` | `HistoryEndpoints` |
| Việc chạy định kỳ | `Api/Workers/<Tên>Worker.cs` + `AddHostedService` | `DailyHistoryWorker` |
| Cấu hình mới | Options class cạnh service + section trong `appsettings.json` + [configuration.md](configuration.md) | `LivePriceOptions` |
| Kiểu dữ liệu API cho frontend | `dashboard/src/api/client.ts` | `DailyRange` |
| Card / biểu đồ mới | `dashboard/src/features/<mảng>/` | `DailyRangeBar.tsx` |
| Trang mới | `dashboard/src/pages/` + thêm vào `PAGES` trong `App.tsx` | `HistoryPage.tsx` |
| Test | Cùng tầng với code được test: `tests/CryptoBot.<Tầng>.Tests/<Kiểu>Tests.cs` | `VolumePressureTests` |
| Tài liệu | `docs/` + link trong `docs/README.md` | — |

Các bước chi tiết cho từng trường hợp: [extending.md](extending.md).
