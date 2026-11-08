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
| Nến, dữ liệu thị trường | `MarketData/` | `MarketData/` | `Bot/`, `History/` | `features/market/`, `features/history/` |
| Chiến lược, tín hiệu | `Strategies/` | `Signals/` | `Bot/` | `pages/SignalsPage.tsx` |
| Chỉ báo tự viết | `Indicators/` | `Indicators/` | `Indicators/` | (trong `features/market/CandleChart.tsx`) |
| Paper futures + lệnh tự động | `Futures/` | `Futures/` | `PaperFutures/`, `AutoOrders/` | `features/futures/`, `features/auto/` |
| Giá trực tiếp, biên độ, ngày tương tự | `MarketData/` (công thức) | `LivePrices/` | `LivePrices/` | `features/live/` |
| Dự đoán, độ chính xác | `Prediction/` | — | `Prediction/` | `features/prediction/` |
| Báo cáo biến động hằng ngày | `MarketData/DailyMove/` (công thức, entity) | `Reports/` | `Reports/` | `features/reports/`, `pages/ReportsPage.tsx` |
| Tin tức | `News/` | `News/` | `News/` | `features/news/`, `pages/NewsPage.tsx` |
| Dùng chung | `Common/` | `Abstractions/` | — | `components/`, `time.ts` |

Không có thư mục kiểu `Models/`, `Services/`, `Helpers/`, `Utils/` gom theo loại kỹ thuật. Service, record, enum và
interface của cùng một tính năng nằm cạnh nhau.

### Hai quy tắc bố cục

1. **Mỗi file một kiểu.** Mỗi class, record, interface, struct hoặc enum nằm trong file riêng, tên file trùng tên
   kiểu (`ICandleStore.cs`, `PredictionDto.cs`). Không gom nhiều kiểu vào một file "contracts".
2. **Mảng lớn chia thư mục con theo chức năng.** Khi một mảng có nhiều file, chia thành thư mục con, mỗi thư mục
   là **một chức năng** (ví dụ `MarketData/DailyHistory/` chứa service, options và kết quả của việc đồng bộ nến
   ngày). Thư mục con **không đổi namespace**: file trong `Application/MarketData/DailyHistory/` vẫn là
   `namespace CryptoBot.Application.MarketData;` (giống `Enums/`), nên chuyển file giữa các thư mục con không phải
   sửa `using`.

---

## `src/CryptoBot.Domain` — nghiệp vụ thuần túy

```text
CryptoBot.Domain/
├── Common/
│   ├── ListSlice.cs              # View read-only trên một đoạn danh sách, không sao chép (cửa sổ nến trượt)
│   └── PercentChange.cs          # internal: % thay đổi Between(from, to), UpShare
├── MarketData/
│   ├── Candles/                  # Nến và kiểm tra chuỗi nến
│   │   ├── Candle.cs             # Entity nến OHLCV + ApplyUpdate (không ghi đè nến đã đóng) + Validate
│   │   ├── CandleInterval.cs     # 15m / 1h / 1d; TryParse/Parse; OpenTimeAt (nến chứa một thời điểm)
│   │   ├── CandleIssue.cs        # Một lỗi tìm thấy trong chuỗi nến
│   │   ├── CandleSeriesValidator.cs # Validate: trùng / sai thứ tự / thiếu / OHLCV không hợp lệ; WithoutRejected
│   │   ├── DailyCloses.cs        # internal: kiểm tra chung cho chuỗi giá đóng ngày
│   │   └── VolumePressure.cs     # Mua/bán chủ động, cảnh báo áp đảo
│   ├── DailyMove/                # Biến động theo ngày
│   │   ├── DailyMoveRange.cs     # Biên độ ±1σ/±2σ sau N ngày
│   │   ├── DailyMoveReport.cs    # Báo cáo một khoảng: cao/thấp, giảm từ đỉnh, khối lượng bất thường
│   │   ├── ReportCandle.cs       # Nến rút gọn trong báo cáo
│   │   └── DailyReport.cs        # Entity: báo cáo đã lưu (nội dung JSON)
│   ├── MonthlyChange/            # % theo tháng/năm
│   │   └── MonthlyChangeReport.cs, MonthChange.cs, YearChange.cs, MonthOfYearStats.cs
│   ├── SimilarDays/              # Dự báo "ngày tương tự"
│   │   └── SimilarDaysForecast.cs, SimilarDaysBacktest.cs
│   └── Enums/                    # CandleIssueKind, ForecastLean, VolumePressureSide
├── Strategies/
│   ├── Contracts/                # IStrategy, StrategyContext, StrategyDecision,
│   │                             # StrategyInstance (entity), TradingSignal (entity)
│   ├── EmaCross/                 # EmaCrossStrategy, EmaCrossParameters, IndicatorValues
│   ├── Calculations/
│   │   ├── Ema.cs                # EMA: Calculate (cả chuỗi), LastTwo (2 giá trị cuối, không cấp phát)
│   │   └── Crossover.cs          # Up/Down (2 đường), UpThrough/DownThrough (qua mức cố định)
│   └── Enums/                    # SignalAction
├── Indicators/
│   ├── Contracts/                # ICustomIndicator, IndicatorAttribute, IndicatorResult, IndicatorLine, IndicatorWindow
│   ├── Rsi/
│   │   ├── Rsi.cs                # RSI Wilder: Calculate, LastTwo
│   │   └── MyRsiIndicator.cs     # Chỉ báo mẫu đầy đủ (đọc file này trước khi viết chỉ báo mới)
│   ├── Template/
│   │   └── TemplateIndicator.cs  # Khung trống để copy (không có [Indicator] nên không được đăng ký)
│   └── Enums/                    # IndicatorSignal
├── Futures/
│   ├── Calculations/             # FuturesMath (làm tròn, slippage, giá thanh lý, PnL, SL/TP), FuturesSymbolRules
│   ├── Positions/                # FuturesPosition (entity vị thế), PaperLedgerEntry
│   ├── AutoOrders/               # AutoOrderRule (entity quy tắc tự mở lệnh)
│   └── Enums/                    # PositionSide, PositionStatus, CloseReason, LedgerEntryType,
│                                 # TriggerCondition, RuleTriggerType, AutoRuleStatus
├── News/
│   ├── NewsArticle.cs            # Entity: một bài tin (tiêu đề, tóm tắt, link, coin, giờ đăng)
│   └── NewsText.cs               # Bỏ HTML khỏi tóm tắt, nhận ra coin (BTC/ETH/ETC) theo từ khóa
└── Prediction/
    ├── DirectionPredictor/       # PriceDirectionPredictor (Predict, PredictAt, Backtest), PredictorSettings,
    │                             # PricePrediction, PredictionAccuracy
    ├── SignalAccuracy/           # SignalAccuracy (chấm tín hiệu sau 1/3/6/12 nến), DirectionSignal, HorizonAccuracy
    └── Enums/                    # PriceDirection (Up / Down)
```

Đặt gì ở đây:

- Entity (class có `private set`, constructor kiểm tra bất biến, constructor rỗng `private` cho EF Core).
- Công thức và quy tắc nghiệp vụ dưới dạng `static class` hoặc `record` có `static` factory (`Compute`, `Build`, `From`).
- Chiến lược (`IStrategy`) và chỉ báo (`ICustomIndicator`).
- Enum: **mỗi enum một file** trong thư mục `Enums/` của mảng (`Futures/Enums/CloseReason.cs`), có `/// <summary>`
  cho enum và từng giá trị. Namespace vẫn là namespace của mảng (`CryptoBot.Domain.Futures`), không thêm `.Enums`,
  để code dùng enum không cần thêm `using`.

Không đặt ở đây: bất cứ thứ gì cần `await`, `ILogger`, `HttpClient`, `DbContext`, `DateTime.UtcNow`, cấu hình.

Kiểu đi kèm (ví dụ `SimilarDaysBacktest` của `SimilarDaysForecast`) vẫn có file riêng, đặt cùng thư mục con với
kiểu chính.

## `src/CryptoBot.Application` — điều phối

```text
CryptoBot.Application/
├── Abstractions/                 # Port dùng chung nhiều mảng
│   ├── Clock/                    # IClock
│   ├── MarketData/               # IMarketDataClient, IDailyHistoryClient, ICandleStore, CandleKey, KlineDto,
│   │                             # UpsertResult, MarketDataUnavailableException
│   ├── Strategies/               # IStrategyInstanceStore
│   └── Signals/                  # ISignalStore, SignalFilter
├── MarketData/
│   ├── CandleSync/               # CandleSyncService, MarketDataOptions, SyncResult
│   ├── DailyHistory/             # DailyHistorySyncService, DailyHistoryOptions
│   ├── Status/                   # MarketDataStatusRegistry (trạng thái feed cho /api/status), MarketDataStatus
│   └── Events/                   # MarketEventCatalog, MarketEvent, MonthEvent
├── Signals/
│   ├── Evaluation/               # SignalEvaluationService, SignalOptions, EvaluationResult
│   ├── Charts/                   # ChartQueryService (EMA theo đúng cửa sổ), ChartData, ChartPoint
│   └── Configuration/            # StrategyConfigurator, StrategyDefinition, StrategyFactory, StrategyTypes
├── Indicators/
│   ├── Registry/                 # IndicatorRegistry (tìm [Indicator] bằng reflection, gán tham số),
│   │                             # IndicatorDefinition, ConfiguredIndicator, IndicatorConfigError
│   └── Computation/              # IndicatorService (chart, giá trị mới nhất, GapIndex), IndicatorChart,
│                                 # IndicatorPoint, IndicatorReading
├── Futures/
│   ├── PaperTrading/             # PaperFuturesService (Quote / Open / Close / EvaluateTriggers / Account),
│   │                             # PaperFuturesOptions, FuturesSymbolOptions, PaperFuturesLock, IPaperFuturesStore,
│   │                             # ClosedPositionFilter, OpenPositionRequest, OpenQuote, OpenResult, CloseResult,
│   │                             # PositionView, AccountSnapshot
│   ├── MarkPrices/               # IMarkPriceClient, MarkPriceCache, MarkPrice
│   └── AutoOrders/               # AutoOrderService, AutoOrderOptions, IAutoOrderStore, CreateRuleRequest,
│                                 # CreateRuleResult, DailyRiskState, AutoOrderSummary
├── News/
│   ├── Feed/                     # INewsFeedClient, NewsFeedOptions, NewsItemDto, NewsFeedUnavailableException
│   ├── Storage/                  # INewsStore, NewsFilter
│   └── Sync/                     # NewsSyncService (lấy bài mới từ các feed RSS), NewsOptions, NewsSyncResult
├── LivePrices/
│   ├── Sampling/                 # LivePriceSampler, LivePriceOptions, LivePriceSample, LivePriceHistory
│   ├── DailyRange/               # DailyRangeService: nến ngày Binance (cache) → DailyMoveRange
│   └── SimilarDays/              # SimilarDaysService, SimilarDaysCache, SimilarDaysResult
└── Reports/                      # Một chức năng nên không chia thư mục con
    ├── DailyReportService.cs     # Lịch 06:00, lập và đọc báo cáo
    └── DailyReportOptions.cs, DailyReportRun.cs, IDailyReportStore.cs, SavedDailyReport.cs
```

Quy ước:

- **Options class nằm cùng thư mục con với service dùng nó** (ví dụ `MarketDataOptions.cs` cạnh
  `CandleSyncService.cs` trong `MarketData/CandleSync/`).
- Interface (port) nằm ở `Abstractions/` nếu dùng chung nhiều mảng; port chỉ một mảng dùng thì nằm trong thư mục
  con của chức năng đó (ví dụ `IPaperFuturesStore` trong `Futures/PaperTrading/`, `IAutoOrderStore` trong
  `Futures/AutoOrders/`).
- Service là `sealed class` với primary constructor nhận dependency.

## `src/CryptoBot.Infrastructure` — kỹ thuật

```text
CryptoBot.Infrastructure/
├── DependencyInjection.cs        # AddInfrastructure(): đăng ký MỌI service/store/client/options; InitializeDatabaseAsync
├── SystemClock.cs                # IClock dùng đồng hồ thật
├── Exchanges/
│   ├── Binance/
│   │   ├── BinanceMarketDataClient.cs      # IMarketDataClient
│   │   ├── BinanceFuturesMarkPriceClient.cs# IMarkPriceClient
│   │   └── BinanceOptions.cs
│   └── Bitfinex/
│       ├── BitfinexDailyHistoryClient.cs   # IDailyHistoryClient
│       └── BitfinexOptions.cs
├── News/
│   └── RssNewsFeedClient.cs      # INewsFeedClient: đọc RSS 2.0 / Atom
└── Persistence/
    ├── Database/                 # BotDbContext (DbSet, mapping), DecimalInvariantStringConverter (decimal→TEXT),
    │                             # UtcUnixMillisecondsConverter (DateTime→Unix ms)
    ├── MarketData/               # CandleStore
    ├── Strategies/               # StrategyInstanceStore, SignalStore
    ├── Futures/                  # PaperFuturesStore, AutoOrderStore
    ├── Reports/                  # DailyReportStore
    ├── News/                     # NewsStore
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
├── Endpoints/                    # Mỗi thư mục con: XxxEndpoints.cs + các XxxDto.cs của nó
│   ├── Bot/                      # /api/status, /strategies, /signals, /market/candles, /health/*
│   ├── PaperFutures/             # /api/paper/futures/*
│   ├── AutoOrders/               # /api/paper/futures/auto-rules/*
│   ├── Indicators/               # /api/indicators/*
│   ├── LivePrices/               # /api/live/*
│   ├── Prediction/               # /api/prediction, /api/prediction/indicators
│   ├── History/                  # /api/history/monthly
│   ├── Reports/                  # /api/reports/daily, /api/reports/daily/run
│   ├── News/                     # /api/news, /api/news/sync
│   └── QueryEnum.cs              # Helper dùng chung: đọc enum từ query string
├── Workers/                      # 6 BackgroundService, mỗi worker một chức năng (architecture.md §3)
├── data/                         # cryptobot.db khi chạy từ thư mục này (bị .gitignore)
└── wwwroot/                      # Bản build dashboard (bị .gitignore)
```

Mỗi file endpoint là một `static class XxxEndpoints` có extension `MapXxxEndpoints(this IEndpointRouteBuilder app)`.
**Mỗi DTO một file** (`public sealed record XxxDto(...)`), nằm cùng thư mục con với endpoint dùng nó. Mapping
`ToDto` là `private static` trong class endpoint.

## `src/cryptobot-dashboard` — frontend

```text
cryptobot-dashboard/
├── index.html
├── package.json                  # Phiên bản cố định (không dùng ^ hay ~)
├── tsconfig.json
├── vite.config.ts                # Cổng 8090, proxy /api + /health → 5080, build ra ../CryptoBot.Api/wwwroot
└── src/
    ├── main.tsx                  # QueryClient, ConfigProvider vi_VN
    ├── App.tsx                   # Layout, menu 7 trang (#hash), chọn chiến lược
    ├── time.ts                   # Định dạng giờ Việt Nam (formatVn, formatVnUnix, ago…), ngày giờ cho Excel
    ├── api/
    │   └── client.ts             # MỌI kiểu dữ liệu REST + các object gọi API (api, futuresApi, autoApi…)
    ├── components/
    │   ├── StatusBar.tsx         # Component dùng ở nhiều trang
    │   ├── tableFilters.ts       # Bộ lọc trên tiêu đề cột Table, lọc lại khi xuất Excel
    │   ├── excel.ts              # downloadExcel: ghi .xlsx từ danh sách cột (write-excel-file)
    │   └── ExportExcelButton.tsx # Nút "Xuất Excel" dùng chung
    ├── pages/                    # Mỗi trang một file, chỉ ghép các card
    │   ├── MarketPage.tsx  TradingPage.tsx  PredictionPage.tsx  SignalsPage.tsx  HistoryPage.tsx  ReportsPage.tsx
    │   │   NewsPage.tsx
    └── features/                 # Card/biểu đồ theo tính năng
        ├── market/               # CandleChart, SessionStatsCard
        ├── live/                 # LivePricePanel, DailyRangeBar, Sparkline
        ├── futures/              # TradePanel, PositionsCard, format.ts (định dạng tiền/giá dùng chung)
        ├── auto/                 # AutoOrdersCard
        ├── prediction/           # PredictionCard, IndicatorAccuracyCard
        ├── history/              # MonthlyHeatmap, MoveEventsCard
        ├── reports/              # DailyReportCard
        └── news/                 # NewsList
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
| Công thức tính từ dữ liệu có sẵn (không I/O) | `Domain/<Mảng>/<Chức năng>/` | `DailyMoveRange`, `FuturesMath` |
| Chỉ báo mới | `Domain/Indicators/<Tên>/<Tên>Indicator.cs` | `Rsi/MyRsiIndicator` |
| Loại chiến lược mới | `Domain/Strategies/<Tên>/` + nhánh mới trong `StrategyFactory` | `EmaCross/EmaCrossStrategy` |
| Entity mới lưu DB | `Domain/<Mảng>/<Chức năng>/` + `DbSet` và mapping trong `BotDbContext` + migration | `AutoOrderRule` |
| Helper thuần túy dùng ở ≥ 2 mảng Domain | `Domain/Common/` | `ListSlice`, `PercentChange` |
| Việc cần đọc DB / gọi sàn / log / đồng hồ | `Application/<Mảng>/<Chức năng>/<Tên>Service.cs` | `SimilarDaysService` |
| Interface tới thế giới bên ngoài | `Application/Abstractions/<Mảng>/I<Tên>.cs` (hoặc thư mục con của chức năng) | `ICandleStore` |
| Cài đặt interface bằng EF Core | `Infrastructure/Persistence/<Mảng>/<Tên>Store.cs` | `CandleStore` |
| Client cho sàn mới | `Infrastructure/Exchanges/<Sàn>/` | `BitfinexDailyHistoryClient` |
| Đăng ký DI | `Infrastructure/DependencyInjection.cs` (đúng nhóm) | — |
| Endpoint REST | `Api/Endpoints/<Mảng>/<Mảng>Endpoints.cs` + mỗi DTO một file cùng thư mục, map trong `Program.cs` | `HistoryEndpoints` |
| Việc chạy định kỳ | `Api/Workers/<Tên>Worker.cs` + `AddHostedService` | `DailyHistoryWorker` |
| Cấu hình mới | Options class cạnh service + section trong `appsettings.json` + [configuration.md](configuration.md) | `LivePriceOptions` |
| Kiểu dữ liệu API cho frontend | `dashboard/src/api/client.ts` | `DailyRange` |
| Card / biểu đồ mới | `dashboard/src/features/<mảng>/` | `DailyRangeBar.tsx` |
| Trang mới | `dashboard/src/pages/` + thêm vào `PAGES` trong `App.tsx` | `HistoryPage.tsx` |
| Test | Cùng tầng với code được test: `tests/CryptoBot.<Tầng>.Tests/<Kiểu>Tests.cs` | `VolumePressureTests` |
| Tài liệu | `docs/` + link trong `docs/README.md` | — |

Các bước chi tiết cho từng trường hợp: [extending.md](extending.md).
