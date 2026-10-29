# Test

```bash
dotnet test                                          # toàn bộ
dotnet test tests/CryptoBot.Domain.Tests             # một project
dotnet test --filter "FullyQualifiedName~VolumePressure"   # lọc theo tên
UPDATE_GOLDEN=1 dotnet test                          # ghi lại snapshot golden (có chủ đích)
npm --prefix src/cryptobot-dashboard run typecheck   # frontend: chỉ typecheck, chưa có test
```

Tại thời điểm 2026-10-29 (nhánh `test/more-unit-tests`): Domain 185, Application 104, Integration 7 test, tất cả pass.
Framework: xUnit 2.9, `using Xunit;` là global using trong mỗi project test.

## Ba project test

| Project | Tham chiếu | Test cái gì | Fake / hạ tầng |
| --- | --- | --- | --- |
| [`CryptoBot.Domain.Tests`](../tests/CryptoBot.Domain.Tests) | Domain | Công thức, entity, chiến lược, chỉ báo, dự đoán | Không cần fake. Helper [`TestCandles`](../tests/CryptoBot.Domain.Tests/TestCandles.cs) |
| [`CryptoBot.Application.Tests`](../tests/CryptoBot.Application.Tests) | Application | Service: sync, đánh giá tín hiệu, paper futures, quy tắc tự động, registry, cache, golden | [`Fakes.cs`](../tests/CryptoBot.Application.Tests/Fakes.cs): `FakeClock`, `InMemoryCandleStore`, store/client giả |
| [`CryptoBot.IntegrationTests`](../tests/CryptoBot.IntegrationTests) | Infrastructure | EF Core mapping, converter, index duy nhất, migration thật | File SQLite tạm trong `%TEMP%`, xóa sau test |

Quy tắc chọn chỗ: test ở **tầng thấp nhất** chứa logic. Công thức → Domain test. Điều phối nhiều port → Application
test với fake. Chỉ những gì phụ thuộc SQLite thật (decimal round-trip, UTC, unique index, transaction) → Integration.

## Đặt tên và bố cục

- Một file cho mỗi kiểu được test: `<Kiểu>Tests.cs`, class `public class <Kiểu>Tests`.
- Tên test là **câu tiếng Anh snake_case** mô tả hành vi, không phải tên hàm:
  `Running_last_month_is_incomplete_and_left_out_of_stats`, `Backtest_skips_unchanged_closes`,
  `PredictAt_uses_only_the_candles_before_index`.
- Arrange / Act / Assert tách bằng dòng trống; chú thích tiếng Việt ngắn khi dữ liệu không hiển nhiên
  (`// Tăng đều: mọi lần đoán đều Up và đều đúng.`).
- Dữ liệu nhỏ, viết tay, đủ để thấy rõ kết quả mong đợi (ví dụ `FromCloses(100, 101, 102, 103, 104, 105)`).
  Tham số nhỏ để không cần hàng trăm nến (`new PredictorSettings { TrendPeriod = 5, MomentumPeriod = 2 }`).
- Thời gian cố định: `T0 = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)`; không dùng `DateTime.UtcNow` trong test.
- `var` được dùng trong test (kể cả Domain.Tests); quy tắc "không var" chỉ áp cho `src/CryptoBot.Domain`.

## Nên test gì

Với mỗi hàm/tính năng mới, tối thiểu:

1. **Đường chính:** đầu vào điển hình cho kết quả đúng.
2. **Biên:** chưa đủ dữ liệu (warm-up), đúng bằng ngưỡng, danh sách rỗng, giá đứng yên/hòa.
3. **Từ chối:** tham số sai ném đúng loại exception hoặc trả đúng lỗi (`Assert.Throws<ArgumentOutOfRangeException>`).
4. **Quy tắc an toàn** liên quan: không nhìn trước tương lai (backtest), không ghi đè nến đã đóng, gửi lại
   `commandId` không mở hai lần, mark cũ thì chặn, giới hạn lỗ ngày chặn kích hoạt.

Ví dụ tốt có sẵn:

| Muốn xem cách test… | File |
| --- | --- |
| Công thức thuần túy | [`DailyMoveRangeTests`](../tests/CryptoBot.Domain.Tests/DailyMoveRangeTests.cs), [`FuturesMathTests`](../tests/CryptoBot.Domain.Tests/FuturesMathTests.cs) |
| Chỉ báo (warm-up, cắt mức, bộ lọc) | [`MyRsiIndicatorTests`](../tests/CryptoBot.Domain.Tests/MyRsiIndicatorTests.cs) |
| Entity và bất biến | [`CandleTests`](../tests/CryptoBot.Domain.Tests/CandleTests.cs) |
| Service với fake + đồng hồ giả | [`PaperFuturesServiceTests`](../tests/CryptoBot.Application.Tests/PaperFuturesServiceTests.cs), [`AutoOrderServiceTests`](../tests/CryptoBot.Application.Tests/AutoOrderServiceTests.cs) |
| Cache và tính một lần | [`SimilarDaysServiceTests`](../tests/CryptoBot.Application.Tests/SimilarDaysServiceTests.cs) |
| SQLite thật | [`SqlitePersistenceTests`](../tests/CryptoBot.IntegrationTests/SqlitePersistenceTests.cs) |

## Golden test

[`SignalGoldenTests`](../tests/CryptoBot.Application.Tests/SignalGoldenTests.cs) chạy các đường sinh tín hiệu (đánh giá
EMA, chart EMA, MyRsi có/không bộ lọc xu hướng) trên 1,500 nến giả lập cố định (random seed 42, bỏ một nến để có
khoảng thiếu), rồi so **từng ký tự** với
[`Golden/signal_outputs.txt`](../tests/CryptoBot.Application.Tests/Golden/signal_outputs.txt).

- **Refactor/tối ưu:** golden phải pass y nguyên. Fail nghĩa là kết quả đã đổi, phải xem lại.
- **Cố ý đổi công thức:** chạy `UPDATE_GOLDEN=1 dotnet test`, đọc kỹ `git diff` của `signal_outputs.txt` để thấy chính
  xác tín hiệu nào đổi, commit snapshot **cùng commit** với thay đổi công thức, nêu trong mô tả PR.

Chi tiết: [signal-functions.md §6](signal-functions.md#6-golden-test-bảo-đảm-kết-quả-không-đổi).

## Fake

- Fake viết tay trong `Fakes.cs` (không dùng thư viện mock). Fake **giữ trạng thái thật** (ví dụ
  `InMemoryCandleStore` dùng `SortedDictionary` và gọi `Candle.ApplyUpdate` giống store thật), nên test kiểm tra được
  hành vi chứ không chỉ "có gọi hàm".
- Đổi interface trong `Ports.cs` → sửa fake tương ứng để nó vẫn giống cài đặt thật.
- `FakeClock.UtcNow` có setter: tua thời gian trong test (mark hết hạn, qua 00:00 UTC…).

## Trước khi mở PR

```bash
dotnet build && dotnet test
npm --prefix src/cryptobot-dashboard run typecheck   # nếu đổi frontend
```

Refactor nhiều commit thì từng commit cũng phải build và test được (có thể kiểm tra trong một git worktree riêng).
