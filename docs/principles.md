# Nguyên tắc

Các quy tắc dưới đây đã được chốt qua nhiều PR. Mỗi quy tắc có **lý do**, để khi gặp trường hợp mới bạn biết nên làm
gì. Muốn phá một quy tắc thì nêu rõ trong mô tả PR và cập nhật file này.

## A. An toàn tiền bạc

### A1. Chỉ Paper. Không có đường nào tới tiền thật

- `Environment = "Paper"`, `LiveLocked = true`. Không lưu, không đọc API key. Client sàn chỉ gọi endpoint công khai,
  không ký request.
- Lệnh tự động (`AutoOrderService`) mở lệnh **chỉ qua** `PaperFuturesService`.
- **Lý do:** đây là dự án học/thử nghiệm. Thêm giao dịch thật cần một thiết kế riêng (testnet trước, giới hạn rủi ro,
  xác nhận tay), không phải một cờ cấu hình.

### A2. Mọi lệnh đi qua một đường duy nhất

Lệnh tay và lệnh tự động cùng gọi `PaperFuturesService.OpenAsync`/`CloseAsync`. Không viết đường mở lệnh thứ hai.
**Lý do:** các kiểm tra (SL trước giá thanh lý, số dư, min notional, mark còn mới, đòn bẩy tối đa) chỉ cần đúng một chỗ.

### A3. Idempotent bằng `commandId`

Mỗi thao tác ghi (mở, đóng, tạo quy tắc) mang một GUID do phía gọi tạo. Gửi lại cùng GUID trả về kết quả cũ, không làm
lần hai. DB có index duy nhất để chặn ở tầng cuối (`(Environment, OpenCommandId)`, `AutoOrderRule.CommandId`).
**Lý do:** double-click, retry mạng, worker crash giữa chừng rồi chạy lại đều không được mở hai vị thế.

### A4. Mặc định thận trọng, mô phỏng bất lợi

Slippage luôn **bất lợi** cho người đặt lệnh. Thanh lý được xét **trước** SL/TP. Quy tắc tự động mặc định 2x, SL 1%,
có giới hạn lỗ ngày. Không có mark price mới (≤ 10 giây) thì **không làm gì** thay vì dùng giá cũ.
**Lý do:** kết quả paper nên tệ hơn hoặc bằng thực tế, không được đẹp hơn.

### A5. Một khóa cho mọi thay đổi số dư/vị thế

`PaperFuturesLock` (một `SemaphoreSlim`) bao mọi thao tác ghi. Vị thế và bút toán sổ vốn ghi trong cùng
`SaveAsync` (một transaction). Số dư = tổng bút toán sổ vốn, không có cột "balance" riêng để lệch.

## B. Số và thời gian

### B1. Tiền, giá, khối lượng là `decimal`

- Không dùng `double`/`float` cho tiền/giá/lượng. Ngoại lệ có chủ đích: phép thống kê cần `Math.Exp`/`Math.Log`/căn
  (σ trong `DailyMoveRange`, khoảng cách trong `SimilarDaysForecast`), đổi sang `double` ở đúng chỗ đó rồi đổi lại.
- SQLite lưu `decimal` dạng chuỗi invariant → **không SUM/ORDER BY/so sánh cột decimal trong SQL**; đọc lên rồi tính
  trong C#.
- Client gửi số tiền có thể dạng chuỗi (`"50.5"`), API chấp nhận (`AllowReadingFromString`).
- Dashboard: số chỉ để hiển thị, **mọi tính toán nằm ở backend**.

### B2. Mọi thời gian là UTC

- Lưu Unix milliseconds. Ghi `DateTime` không phải UTC sẽ ném lỗi (`Candle`, `UtcUnixMillisecondsConverter`).
- API trả UTC (ISO hoặc Unix seconds cho chart). UI đổi sang giờ Việt Nam **chỉ để hiển thị** (`time.ts`).
- "Ngày" trong các tính năng theo ngày (giới hạn lỗ ngày, nến 1d) là ngày UTC.

### B3. Thời gian đến từ `IClock` hoặc từ sàn

- Application/Domain không gọi `DateTime.UtcNow`. Dùng `IClock` (test được bằng `FakeClock`).
- Nến "đã đóng" xác định theo **giờ server Binance**, không theo giờ máy/trình duyệt.

### B4. Culture invariant

Process chạy `InvariantCulture`. Format/parse số luôn dấu chấm, không phụ thuộc máy chủ.

## C. Dữ liệu nến và tín hiệu

### C1. Chỉ dùng nến đã đóng

Chiến lược và chỉ báo chỉ nhận nến `IsClosed`; nến chưa đóng là lỗi (`EmaCrossStrategy`) hoặc bị loại.
**Lý do:** nến đang chạy còn đổi giá, tín hiệu trên nó có thể biến mất (repaint).

### C2. Cửa sổ cố định

Mỗi nến được tính trên **đúng N nến đã đóng gần nhất** kết thúc tại nến đó (`LookbackCandles`/`RequiredCandles`).
**Lý do:** EMA/RSI phụ thuộc điểm bắt đầu. Cố định cửa sổ giúp chart, backtest, live và lệnh tự động ra **cùng một
giá trị** trên cùng dữ liệu. Đổi lại phải tính lại cả cửa sổ cho mỗi nến; đã tối ưu phần sao chép
(`ListSlice`, `LastTwo`) nhưng không bỏ quy tắc này. Xem [signal-functions.md](signal-functions.md#5-đã-tối-ưu-những-gì).

### C3. Thiếu dữ liệu thì không đoán

Cửa sổ có nến thiếu → Hold/None. Chưa đủ nến → "Warm-up". Chỉ báo trễ quá một khung + 2 phút → không kích hoạt quy
tắc. Thiếu dữ liệu mua/bán chủ động → thành phần đó = 0 và ghi rõ "chưa có dữ liệu".

### C4. Không sửa dữ liệu âm thầm

Nến OHLC không hợp lệ, trùng, sai thứ tự → **không lưu**, báo trong `/api/status` + log. Nến đã đóng không bị ghi đè;
sàn trả giá khác → giữ bản cũ, báo xung đột. Chỉ được **điền thêm** cột còn trống (`??=`).

### C5. Mỗi nến được đánh giá đúng một lần

Checkpoint `LastEvaluatedCandleOpenTimeUtc` chỉ tiến lên (`MarkEvaluated` ném lỗi nếu đi lùi). Tín hiệu + checkpoint
ghi trong một transaction. Đổi cấu hình → `Version` mới, đánh giá lại từ đầu, tín hiệu cũ giữ với version cũ.

### C6. Tín hiệu cũ không bao giờ đặt lệnh

Tín hiệu sinh từ nến cũ (tải bù, khởi động) có `IsBackfill = true`. Quy tắc theo chỉ báo chỉ dùng tín hiệu trên nến
đóng **sau** lúc tạo quy tắc (mở) hoặc **sau** lúc mở lệnh (đóng).

### C7. Hàm tín hiệu/dự đoán thuần túy và có golden test

Đổi cách tính tín hiệu phải chạy golden test. Tối ưu/tách hàm → golden phải pass y nguyên. Cố ý đổi công thức →
cập nhật snapshot và đọc diff (xem [testing.md](testing.md#golden-test)).

## D. Độ tin cậy khi chạy

### D1. Lỗi một phần không làm dừng toàn bộ

- Lỗi một symbol/chiến lược/chỉ báo chỉ ảnh hưởng chính nó; worker ghi log và đi tiếp.
- Cấu hình chiến lược/chỉ báo sai → bỏ qua mục đó, ghi lỗi rõ ràng (dashboard + log), app vẫn chạy.
- Phần phụ hỏng (ví dụ dự báo ngày tương tự) trả `null` + log, phần chính vẫn hiển thị.
- Ngoại lệ có chủ đích: dữ liệu ghi tay sai cú pháp (`marketevents.json`) → không khởi động, lỗi chỉ đúng chỗ sai.

### D2. Phân biệt lỗi tạm thời và lỗi thật

Lỗi sàn có thể thử lại (429/418, 5xx, timeout, mạng) → `MarketDataUnavailableException` (kèm `RetryAfter`), worker
backoff cấp số nhân + jitter, tối đa 5 phút. Lỗi 4xx khác → `InvalidOperationException` (lỗi code/tham số, cần sửa).

### D3. Không che lỗi bằng dữ liệu cũ

Bảng giá: nguồn lỗi thì để trống kèm lý do. Mark price hết hạn thì chặn giao dịch. Feed stale thì đánh dấu `isStale`.

### D4. Trạng thái trong bộ nhớ chỉ để quan sát

Registry trạng thái, lịch sử giá trực tiếp, cache… mất khi restart và không được dùng làm nguồn sự thật cho quyết định
có hậu quả (mở/đóng lệnh dựa trên DB + mark còn mới).

## E. Trung thực với người dùng

### E1. Nói rõ giới hạn của số liệu

Biên độ "không đoán hướng". Thống kê theo tháng "không dự đoán tháng tới". Sự kiện "cùng tháng, không khẳng định nguyên
nhân". Dự đoán luôn đi kèm tỷ lệ đúng khi chạy thử. Tính năng không có lợi thế khi kiểm chứng thì không bật
(ngày tương tự cho 7/30 ngày).

### E2. Kiểm chứng không nhìn trước tương lai

Backtest chỉ dùng dữ liệu trước điểm đoán (`PredictAt`, walk-forward trong `SimilarDaysForecast`). Bỏ qua trường hợp
hòa/đứng yên thay vì tính là đúng.

### E3. Lý do bằng chữ cho mọi quyết định

`StrategyDecision.Reason`, `IndicatorResult.Reason`, `PricePrediction.Reason`, `AutoOrderRule.Note/TriggerDetail`:
người dùng luôn thấy **vì sao** bot làm (hoặc không làm) một việc.

## F. Thiết kế code

- **Domain thuần túy, Application điều phối, Infrastructure kỹ thuật, Api ghép nối** ([architecture.md](architecture.md)).
- **Thư mục theo tính năng**, giống nhau ở mọi tầng ([folder-structure.md](folder-structure.md)).
- **Đơn giản trước:** REST polling thay vì WebSocket, SQLite thay vì server DB, một worker tuần tự thay vì nhiều luồng
  ghi. Chỉ thêm phức tạp khi có số đo cho thấy cần (ví dụ bảng số đo trong signal-functions.md).
- **Gom trùng lặp khi đã lặp lại** (≥ 2 chỗ cùng công thức, cùng thông báo lỗi): `PercentChange`, `DailyCloses.Validate`,
  `Crossover`, `ListSlice`, `PredictAt`. Không tạo abstraction trước khi có chỗ thứ hai dùng.
- **Refactor không đổi hành vi**, tách thành nhiều commit nhỏ, mỗi commit build và test được (xem PR #56, #57).
