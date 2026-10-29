# Hướng dẫn mở rộng

Các bước cụ thể cho những thay đổi hay gặp. Mỗi mục liệt kê đủ file cần sửa, theo thứ tự nên commit
(xem [git-workflow.md](git-workflow.md#commit)).

## 1. Thêm một chỉ báo tự viết

Không cần sửa Application, Api hay dashboard: registry tự tìm class, chart và quy tắc tự động tự dùng được.

1. Copy [`TemplateIndicator.cs`](../src/CryptoBot.Domain/Indicators/TemplateIndicator.cs) thành
   `src/CryptoBot.Domain/Indicators/MyBreakoutIndicator.cs`, đổi tên class. Đọc
   [`MyRsiIndicator.cs`](../src/CryptoBot.Domain/Indicators/MyRsiIndicator.cs) để có ví dụ đầy đủ.
2. Gắn `[Indicator("MyBreakout", Description = "Mô tả ngắn hiện trên dashboard")]`.
3. Viết theo 4 phần như MyRsi:
   - **Tham số:** property `public` có `set`, có giá trị mặc định. Tên property = khóa trong `Parameters`.
   - **`Validate()`:** trả `string?` lý do khi tham số sai.
   - **`RequiredCandles` + `Lines`:** số nến của cửa sổ cố định; các đường vẽ (`Overlay: true` vẽ trên nến, `false` vẽ
     khung riêng, `Levels` là mức ngang).
   - **`Compute(closedCandles)`:** trả `IndicatorResult(values, signal, reason)`. Khóa của `values` trùng `Lines.Key`.
4. Quy tắc bắt buộc: chỉ đọc `closedCandles` (nến cuối là nến vừa đóng); không gọi API, không đọc đồng hồ, không lưu
   gì vào field giữa các lần gọi; chưa đủ nến → `IndicatorResult.None("Warm-up: ...")`. Không dùng `var`.
5. Dùng khối có sẵn thay vì tự viết vòng lặp: `IndicatorWindow.LastCloses`, `Ema.LastTwo`, `Rsi.LastTwo`,
   `Crossover.UpThrough/DownThrough`, `PercentChange.Between`, `ListSlice`.
6. Test trong `tests/CryptoBot.Domain.Tests/MyBreakoutIndicatorTests.cs`: warm-up, phát Long, phát Short, không phát,
   tham số sai (xem `MyRsiIndicatorTests`).
7. Khai báo trong `appsettings.json` → `Indicators`:
   ```json
   { "Name": "btc-15m-breakout", "Type": "MyBreakout", "Symbol": "BTCUSDT", "Interval": "15m",
     "Parameters": { "Period": 20 } }
   ```
8. Restart API. Kiểm tra `GET /api/indicators` (không có trong `errors`), chọn chỉ báo trên chart, xem trang Dự đoán →
   tỷ lệ đúng.

## 2. Thêm một loại chiến lược (ngoài EmaCross)

Hiện code gắn khá chặt với EMA, nên đây là thay đổi lớn. Cần sửa:

| Chỗ | Việc |
| --- | --- |
| `Domain/Strategies/` | Class mới cài `IStrategy` + record tham số có `Validate()` |
| [`StrategyFactory`](../src/CryptoBot.Application/Signals/StrategyFactory.cs) | Thêm hằng trong `StrategyTypes`, nhánh trong `Create` (kiểu trả về đang là `EmaCrossStrategy`, cần đổi thành `IStrategy`) |
| [`StrategyConfigurator`](../src/CryptoBot.Application/Signals/StrategyConfigurator.cs) | `StrategyDefinition.Parameters` đang là `EmaCrossParameters`; `Validate` chỉ nhận `EmaCross` |
| `TradingSignal` | Có cột `FastEma`/`SlowEma`; chiến lược mới cần giá trị tương ứng hoặc cột tổng quát hơn (migration) |
| `StrategyDecision.Indicators` | Đang là `IndicatorValues(FastPrev, SlowPrev, Fast, Slow)` |
| [`ChartQueryService`](../src/CryptoBot.Application/Signals/ChartQueryService.cs) + `BotEndpoints` + dashboard | Đang vẽ đúng 2 đường EMA |

Nếu chỉ cần tín hiệu Long/Short để xem trên chart và kích hoạt lệnh Paper, **viết chỉ báo tự viết (mục 1)** đơn giản
hơn nhiều. Chỉ làm chiến lược mới khi cần lưu tín hiệu vào DB theo version.

## 3. Thêm một endpoint

1. Chọn file `src/CryptoBot.Api/Endpoints/<Mảng>Endpoints.cs` có sẵn, hoặc tạo file mới:
   ```csharp
   namespace CryptoBot.Api.Endpoints;

   public static class FooEndpoints
   {
       public static void MapFooEndpoints(this IEndpointRouteBuilder app)
       {
           app.MapGet("/api/foo", async (string symbol, int? limit, FooService svc, CancellationToken ct) =>
           {
               if (string.IsNullOrWhiteSpace(symbol) || !symbol.All(char.IsAsciiLetterOrDigit))
                   return Results.BadRequest(new { error = "Symbol không hợp lệ" });
               var result = await svc.GetAsync(symbol.ToUpperInvariant(), Math.Clamp(limit ?? 100, 1, 1000), ct);
               return result is null
                   ? Results.NotFound(new { error = "Chưa có dữ liệu." })
                   : Results.Ok(new FooDto(result.Symbol, result.Value));
           });
       }
   }

   /// <summary>Mô tả DTO, đơn vị, khi nào null.</summary>
   public sealed record FooDto(string Symbol, decimal Value);
   ```
2. File mới → thêm `app.MapFooEndpoints();` trong [`Program.cs`](../src/CryptoBot.Api/Program.cs), **trước**
   `MapFallback`.
3. Logic nằm trong service Application, endpoint chỉ kiểm tra đầu vào + gọi service + đổi DTO.
4. Thêm kiểu `Foo` và hàm gọi vào [`api/client.ts`](../src/cryptobot-dashboard/src/api/client.ts).
5. Thêm dòng vào [api.md](api.md).

## 4. Thêm một service Application

1. `src/CryptoBot.Application/<Mảng>/FooService.cs`:
   `public sealed class FooService(ICandleStore candles, IClock clock, FooOptions options, ILogger<FooService> logger)`.
2. Cần thứ gì từ bên ngoài (DB, HTTP) → khai báo interface (port), cài ở Infrastructure.
3. Đăng ký trong [`DependencyInjection.cs`](../src/CryptoBot.Infrastructure/DependencyInjection.cs), đúng nhóm, có chú
   thích: `AddScoped` nếu dùng store, `AddSingleton` nếu giữ trạng thái/cache trong bộ nhớ.
4. Test trong `tests/CryptoBot.Application.Tests/FooServiceTests.cs` với fake.

## 5. Thêm hoặc đổi cột trong database (migration)

1. Sửa entity trong Domain (property `{ get; private set; }`, cập nhật constructor/hàm thay đổi trạng thái).
2. Sửa mapping trong [`BotDbContext.OnModelCreating`](../src/CryptoBot.Infrastructure/Persistence/BotDbContext.cs):
   `HasMaxLength` cho chuỗi, `HasConversion<string>()` cho enum, index cần thiết. Property tính toán → `e.Ignore(...)`.
3. Tạo migration (từ thư mục gốc repo):
   ```bash
   dotnet tool restore
   dotnet ef migrations add TenMigration \
     --project src/CryptoBot.Infrastructure \
     --startup-project src/CryptoBot.Api \
     --output-dir Persistence/Migrations
   ```
   Tên migration PascalCase mô tả thay đổi: `CandleTakerBuyVolume`, `AutoCloseOnReverse`.
4. Đọc file migration sinh ra. Cột mới trên bảng có dữ liệu nên **nullable** (như `TakerBuyVolume`), hoặc có default
   hợp lý; có thể cần logic điền bù (như `GetFirstMissingStatsOpenTimeAsync` tải lại nến thiếu thống kê).
5. Migration chạy tự động khi khởi động. Thêm/sửa test trong `SqlitePersistenceTests` nếu đụng converter hoặc index.
6. Nhớ: `decimal` lưu `TEXT`, không viết truy vấn so sánh/sắp xếp cột decimal.

## 6. Thêm cấu hình

Xem [configuration.md → Thêm một khóa cấu hình mới](configuration.md#thêm-một-khóa-cấu-hình-mới).
Danh sách trong options mặc định `[]`, không điền sẵn phần tử.

## 7. Thêm một worker chạy nền

1. `src/CryptoBot.Api/Workers/FooWorker.cs`, `sealed class FooWorker(IServiceScopeFactory scopes, FooOptions options,
   ILogger<FooWorker> logger) : BackgroundService`.
2. Chu kỳ đều → `PeriodicTimer` (như `LivePriceWorker`, `DailyHistoryWorker`); chu kỳ thay đổi theo lỗi/backoff →
   `Task.Delay` (như `MarketDataWorker`).
3. Mỗi vòng: `await using var scope = scopes.CreateAsyncScope();` rồi lấy service Scoped từ scope.
4. Bắt lỗi theo từng symbol/mục; chỉ thoát khi `OperationCanceledException` lúc dừng app.
5. Đăng ký `builder.Services.AddHostedService<FooWorker>();` trong `Program.cs`.
6. Ghi vào bảng worker trong [architecture.md](architecture.md#3-worker-chạy-nền).

## 8. Thêm client cho một sàn

1. Port trong Application (dùng lại `IMarketDataClient`/`IDailyHistoryClient` nếu hợp, hoặc interface mới).
2. `src/CryptoBot.Infrastructure/Exchanges/<Sàn>/<Sàn>XxxClient.cs` + `<Sàn>Options` (BaseUrl, RequestTimeout).
3. Typed client: `services.Configure<SànOptions>(...)` + `services.AddHttpClient<IPort, Client>((sp, http) => ...)`.
4. Đổi lỗi tạm thời (429, 5xx, timeout, mạng) sang `MarketDataUnavailableException` (kèm `RetryAfter`); 4xx khác →
   `InvalidOperationException`. Parse số bằng `CultureInfo.InvariantCulture`, thời gian sang UTC.
5. Chỉ endpoint công khai. Không thêm API key.

## 9. Thêm card hoặc trang trên dashboard

**Card:**

1. Kiểu dữ liệu + hàm gọi trong `api/client.ts`.
2. `src/features/<mảng>/FooCard.tsx`:
   ```tsx
   /** Card hiển thị gì, số liệu có giới hạn gì. */
   export function FooCard({ symbol }: { symbol: string }) {
     const foo = useQuery({ queryKey: ['foo', symbol], queryFn: () => fooApi.get(symbol), refetchInterval: 15_000 });
     if (foo.isPending) return <Card size="small" title="Foo"><Spin /></Card>;
     if (foo.isError) return <Alert type="error" showIcon title="Không tải được Foo" description={foo.error.message} />;
     return <Card size="small" title="Foo">{fmtPrice(foo.data.value)}</Card>;
   }
   ```
3. Thêm card vào trang trong `src/pages/`.

**Trang:**

1. `src/pages/FooPage.tsx` chỉ bố cục các card.
2. Thêm `{ key: 'foo', label: 'Tên tiếng Việt' }` vào `PAGES` trong [`App.tsx`](../src/cryptobot-dashboard/src/App.tsx)
   và dòng render `page === 'foo' && <FooPage ... />`. Trang không theo chiến lược thì xử lý như `history`
   (`usesStrategy`).
3. `npm --prefix src/cryptobot-dashboard run typecheck`.

## 10. Thêm sự kiện thị trường

Thêm mục vào [`marketevents.json`](../src/CryptoBot.Api/marketevents.json): `Month` (`yyyy-MM`), `Symbols`
(rỗng = chung), `Title`, `Detail` có ngày cụ thể. Restart API. Định dạng sai làm app không khởi động, nên chạy
`dotnet test` (có `MarketEventCatalogTests`) hoặc khởi động thử trước khi commit.

## 11. Chỉnh hàm dự đoán nến kế tiếp

1. Sửa trọng số/tham số trong `PredictorSettings` hoặc thêm thành phần vào `PriceDirectionPredictor.Predict`
   ([file](../src/CryptoBot.Domain/Prediction/PriceDirectionPredictor.cs)). Thêm thành phần → thêm trọng số tương ứng
   (đặt 0 để tắt) và ghi vào `Reason`.
2. Cập nhật `PredictionSettingsDto` + `client.ts` nếu muốn hiển thị trọng số mới.
3. Chạy `PriceDirectionPredictorTests`, restart API, xem tỷ lệ đúng trên trang Dự đoán. So sánh với ~50% (đoán bừa)
   trước khi tin vào thay đổi.
