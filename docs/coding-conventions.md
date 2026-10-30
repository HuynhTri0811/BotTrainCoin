# Cách viết code

Quy ước rút ra từ code hiện có. Dự án **chưa có** `.editorconfig`, ESLint hay Prettier, nên quy ước này giữ bằng review:
viết code mới giống code xung quanh nó.

## 1. Ngôn ngữ

| Chỗ | Ngôn ngữ |
| --- | --- |
| Tên class, hàm, biến, file, route, khóa cấu hình | Tiếng Anh |
| Chú thích code (`///`, `//`, `/** */`) | Tiếng Việt |
| Thông báo lỗi, lý do tín hiệu (`Reason`), log | Tiếng Việt |
| Giao diện dashboard | Tiếng Việt |
| Commit message | Tiếng Anh (xem [git-workflow.md](git-workflow.md)) |
| Tiêu đề và mô tả PR, tài liệu `docs/` | Tiếng Việt |

## 2. C# chung (mọi project)

### Cấu hình project

`net10.0`, `<Nullable>enable</Nullable>`, `<ImplicitUsings>enable</ImplicitUsings>`. Không tắt nullable, không dùng
`#nullable disable`. `null!` chỉ dùng cho property của entity được EF Core gán (`= null!`).

### Bố cục file

- **File-scoped namespace**: `namespace CryptoBot.Domain.MarketData;`. Namespace = tên project + thư mục, trừ thư
  mục `Enums/`: enum giữ namespace của mảng chứa nó (`Futures/Enums/PositionSide.cs` → `CryptoBot.Domain.Futures`).
- `using` ở đầu file, sắp theo alphabet, `System.*` trước.
- Thứ tự trong file: record nhỏ đi kèm → class chính. Enum không nằm chung file: mỗi enum một file trong
  `<Mảng>/Enums/` (Domain). DTO của endpoint ở **cuối** file endpoint.

### Kiểu và từ khóa

- Class mặc định `sealed`. Hàm tiện ích `static class`.
- Dữ liệu bất biến dùng `record` (`sealed record`) với positional parameters; mô tả từng tham số bằng
  `/// <param name="...">`.
- Entity có `{ get; private set; }`, constructor public kiểm tra bất biến, constructor rỗng `private` cho EF Core
  (`/// <summary>Dành cho EF Core khi đọc từ database.</summary>`).
- Service dùng **primary constructor**: `public sealed class CandleSyncService(IMarketDataClient client, ...)`.
- Helper chỉ dùng trong assembly để `internal` (`PercentChange`, `DailyCloses`).
- Hằng số có tên thay vì số "ma thuật": `public const int MaxLimit = 1000;`, `private static readonly TimeSpan CacheFor`.
- Collection expression: `[]`, `[1, 3, 6, 12]`, `List<CandleIssue> issues = [];`.
- Pattern matching cho null/kiểu: `if (x.Validate() is { } error)`, `if (store.Find(name) is not { } c) return null;`.
- Tiền/giá/lượng: `decimal` (xem [principles.md](principles.md#b1-tiền-giá-khối-lượng-là-decimal)).
- Thời gian: `DateTime` UTC, tên kết thúc bằng `Utc` (`OpenTimeUtc`, `CreatedAtUtc`). Khoảng thời gian: `TimeSpan`.

### `var`

| Project | Quy tắc |
| --- | --- |
| `CryptoBot.Domain` | **Không dùng `var`.** Luôn ghi kiểu tường minh (`decimal average = ...`, `List<Candle> window = ...`). Chốt ở PR #15 |
| Application, Infrastructure, Api, tests | Dùng `var` khi kiểu rõ ràng từ vế phải (đang dùng rộng rãi) |

Lý do cho Domain: đây là nơi người đọc học công thức, kiểu tường minh (`decimal` hay `double`, `decimal?` hay
`decimal`) giúp đọc đúng mà không cần IDE.

### Đặt tên

- PascalCase cho type/method/property/const; camelCase cho tham số và biến cục bộ; `_camelCase` cho field private
  (`_entries`, `_p`).
- Hàm async kết thúc bằng `Async` và nhận `CancellationToken ct` ở **cuối**.
- Hàm thử: `TryParse(..., out T value)` với `[MaybeNullWhen(false)]`.
- Factory tĩnh: `From`, `Build`, `Compute`, `Create`, `For`.
- Tên thể hiện ý nghĩa nghiệp vụ: `HasSameKey`, `HasSameOhlcv`, `IsLiquidated`, `StopHit`, `GetFresh`.

### Kiểm tra đầu vào và lỗi

- Guard ở đầu hàm public của Domain bằng helper có sẵn: `ArgumentException.ThrowIfNullOrWhiteSpace`,
  `ArgumentOutOfRangeException.ThrowIfLessThan/ThrowIfNegative/ThrowIfGreaterThan`.
- Thông báo lỗi tiếng Việt, ngắn, nói cái gì sai và cần gì: `"Cần ít nhất {n} nến đã đóng."`, `"Timestamp phải là UTC."`.
- Hàm `Validate()` trả `string?` (null = hợp lệ, chuỗi = lý do) cho tham số cấu hình và OHLCV, để gọi chỗ khác hiển
  thị lý do mà không cần bắt exception.
- Lỗi nghiệp vụ người dùng có thể gặp (lệnh bị từ chối) trả về trong kết quả (`OpenQuote.Errors`,
  `CreateRuleResult.Errors`, `CloseResult.Error`), **không** ném exception.
- Exception dành cho: sai lập trình (`InvalidOperationException`, `ArgumentException`), sàn không phục vụ
  (`MarketDataUnavailableException`).
- Bắt exception có điều kiện: `catch (Exception ex) when (ex is not OperationCanceledException)`.

### Async

- Luôn truyền `CancellationToken` xuống tới EF Core/HttpClient.
- Không `.Result`/`.Wait()`. Không `async void`.
- Việc song song độc lập thì khởi động trước rồi `await` sau (`LivePriceSampler`: Spot và Mark cùng lúc).

### Log

- `ILogger<T>` qua constructor. Message template có cấu trúc, **không nối chuỗi**:
  `logger.LogInformation("Market data {Key}: +{Inserted} nến", key, inserted);`
- Mức: `Information` cho sự kiện nghiệp vụ (tạo tín hiệu, mở/đóng lệnh), `Warning` cho lỗi dữ liệu/sàn có thể tự hồi
  phục, `Error` cho lỗi không mong đợi.

### Chú thích

- `/// <summary>` cho mọi type public và member public không hiển nhiên. Viết **cái gì và vì sao**, không lặp lại tên.
  Dùng `<see cref="..."/>`, `<paramref name="..."/>` để liên kết.
- Ghi đơn vị và quy ước dấu: `(%, số âm)`, `(Unix seconds)`, `(UTC)`, `(coin)`.
- `//` trong thân hàm chỉ để giải thích lý do không hiển nhiên ("Bitfinex trả 403 cho request không có User-Agent.").
- File mẫu cho người mới (`MyRsiIndicator`, `EmaCrossStrategy`, `TemplateIndicator`) được phép chú thích dày, có ví dụ
  số cụ thể. Code còn lại giữ chú thích vừa phải.

### Định dạng

- Thụt lề 4 dấu cách. Dòng dài tối đa khoảng 120 ký tự; xuống dòng tham số thì thụt thêm 4.
- `if` một dòng không cần ngoặc nhọn khi thân là một câu lệnh ngắn (đang dùng rộng rãi):
  ```csharp
  if (closedCandles.Count < s.RequiredCandles)
      throw new ArgumentException(...);
  ```
- Expression-bodied member (`=>`) cho hàm/property một biểu thức.

## 3. Quy ước theo tầng

### Domain

- Không `async`, không I/O, không `ILogger`, không cấu hình. Đồng hồ là tham số (`DateTime nowUtc`), không phải `UtcNow`.
- Đầu vào là `IReadOnlyList<T>` hoặc `ReadOnlySpan<T>`; không sửa danh sách được truyền vào.
- Cửa sổ nến dùng `ListSlice<T>` thay vì `Skip/Take/ToList` hay `GetRange` (không sao chép).
- Công thức dùng chung lấy từ `Common/` (`PercentChange.Between`) thay vì viết lại `(a / b - 1) * 100`.

### Application

- Service `sealed class` + primary constructor, đăng ký `Scoped` nếu dùng store (DbContext), `Singleton` nếu chỉ giữ
  trạng thái trong bộ nhớ (`MarkPriceCache`, `LivePriceHistory`, `IndicatorRegistry`).
- Options class POCO, property có `{ get; set; }` và giá trị mặc định hợp lý, chú thích `///` cho từng property.
  Danh sách mặc định **rỗng** (`= []`).
- Truy cập trạng thái dùng chung giữa luồng: `ConcurrentDictionary` + `lock` trên từng entry, hoặc `SemaphoreSlim`.

### Infrastructure

- Store cài interface của Application, nhận `BotDbContext` qua primary constructor. Truy vấn đọc dùng `AsNoTracking`
  (chỉ tracking khi cần cập nhật).
- Không truy vấn SQL trên cột decimal (xem principles B1).
- Client sàn: typed `HttpClient`, đổi lỗi tạm thời sang `MarketDataUnavailableException`, parse số bằng
  `CultureInfo.InvariantCulture`.

### Api

- Endpoint Minimal API trong `static class XxxEndpoints`, nhóm bằng `MapGroup` khi chung tiền tố.
- Kiểm tra đầu vào ngay đầu handler: `symbol` chỉ chữ/số ASCII, `interval` qua `CandleInterval.TryParse`, `limit` qua
  `Math.Clamp(limit ?? mặcĐịnh, 1, max)`.
- Sai định dạng đầu vào → `400 Results.BadRequest(new { error = "..." })`; không tìm thấy →
  `404 Results.NotFound(new { error = "..." })`; đầu vào đúng định dạng nhưng nghiệp vụ từ chối (lệnh không hợp lệ,
  quy tắc bị từ chối) → `422 Results.UnprocessableEntity(new { errors = [...] })` hoặc DTO có `Errors`.
  Frontend đọc cả `error` lẫn `errors` (`postJson` trong `client.ts`).
- Trả **DTO** (`sealed record ...Dto`), không trả entity. Enum đổi sang chuỗi (`.ToString()`). Thời gian cho chart là
  Unix seconds (`ToUnixSeconds`).
- Worker: `sealed class : BackgroundService`, mỗi vòng một DI scope, bắt lỗi theo từng symbol.

## 4. TypeScript / React

### Công cụ

React 19, TypeScript 5.9 (strict qua `tsconfig.json`), Vite 8, Ant Design 6, TanStack Query 5, Lightweight Charts 5.
Phiên bản trong `package.json` **cố định**, không `^`/`~`. Kiểm tra trước khi commit:
`npm --prefix src/cryptobot-dashboard run typecheck` (hoặc `run build`).

### Định dạng

- 2 dấu cách, dấu nháy đơn, có dấu chấm phẩy, dấu phẩy cuối ở danh sách nhiều dòng.
- Component là `export function TênComponent(props)`, **named export** (không default export).
- Props khai báo inline (`{ symbol }: { symbol: string }`) hoặc `type Props` khi dài.
- Chú thích `/** ... */` tiếng Việt ngay trên component/hằng số, nói component hiển thị gì và giới hạn của số liệu.

### Gọi API

- **Mọi** kiểu dữ liệu REST và hàm gọi API nằm trong [`api/client.ts`](../src/cryptobot-dashboard/src/api/client.ts),
  nhóm theo tính năng (`api`, `futuresApi`, `autoApi`, `indicatorApi`, `liveApi`, `predictionApi`, `historyApi`).
  Tên field là camelCase, khớp JSON của DTO backend; ghi chú `null` nghĩa là gì.
- Đọc bằng `useQuery({ queryKey: [...], queryFn, refetchInterval })`. `queryKey` gồm mọi tham số (`['prediction', symbol, interval]`).
- Ghi bằng `useMutation` + `postJson`; lỗi API thành `ApiError` (`message` + `details`).
- Tạo `commandId` bằng `crypto.randomUUID()` **một lần cho mỗi ý định** của người dùng (không tạo lại khi retry).

### Hiển thị

- Không tính nghiệp vụ ở frontend (PnL, giá thanh lý, tín hiệu): lấy từ backend.
- Thời gian qua `time.ts` (`formatVn`, `formatVnUnix`, `ago`); tiền/giá qua `features/futures/format.ts`
  (`fmtUsdt`, `fmtPrice`, `fmtPrice2`, `fmtSigned`, `pnlColor`). Hằng `Intl.NumberFormat` tạo một lần ở cấp module.
- Dùng component Ant Design (`Card size="small"`, `Table`, `Statistic`, `Tag`, `Alert`, `Segmented`) thay vì tự dựng.
- Giá trị thiếu hiển thị `—`.
- Trạng thái lỗi/đang tải hiển thị rõ (`Spin`, `Alert type="error"`), không để trống.

## 5. Checklist trước khi mở PR

- [ ] `dotnet build` 0 lỗi, không thêm cảnh báo mới.
- [ ] `dotnet test` pass (golden test pass, hoặc snapshot được cập nhật có chủ đích).
- [ ] Đổi frontend: `npm --prefix src/cryptobot-dashboard run typecheck` pass.
- [ ] Domain không có `var`, không có I/O.
- [ ] Tiền/giá là `decimal`, thời gian UTC.
- [ ] Đổi entity → có migration.
- [ ] Cấu hình mới → có trong `appsettings.json` và [configuration.md](configuration.md).
- [ ] Endpoint mới → có trong [api.md](api.md) và `api/client.ts`.
- [ ] Đổi hành vi → cập nhật [features.md](features.md) và thêm dòng vào [changelog.md](changelog.md).
