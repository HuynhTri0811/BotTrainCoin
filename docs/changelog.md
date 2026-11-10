# Nhật ký thay đổi

Những gì đã làm, theo PR đã merge vào `develop`, mới nhất ở trên. Số trong ngoặc là issue.
Chi tiết từng tính năng: [features.md](features.md).

## 2026-11-10

- **#143 Hiện 300 lần đoán gần nhất và báo cáo phân tích** (#142). Bảng Dự đoán và bảng tín hiệu gần nhất của chỉ báo
  hiện tối đa 300 dòng thay vì 20. Thêm báo cáo
  [analysis/btc-prediction-and-rebounds](analysis/btc-prediction-and-rebounds/README.md): vì sao hàm dự đoán chỉ đúng
  khoảng 50%, nhịp hồi sau nến 1h sập, DCA định kỳ và bot DCA trên một năm nến BTCUSDT; kèm dữ liệu Binance và script
  Python chạy lại. `.gitignore` cho phép `docs/analysis/*/data/`.

## 2026-11-08

- **#130 Mỗi file một kiểu, chia thư mục con theo chức năng** (#131–#140). Thêm XML doc cho `FuturesMath`, các kiểu
  dùng chung của chỉ báo, `Rsi`, `Ema`, `Crossover`, `TemplateIndicator`. Tách 41 file nhiều kiểu thành mỗi kiểu một
  file (bỏ `Ports.cs`, `Stores.cs`, `IndicatorContracts.cs`, `PaperFuturesContracts.cs`…); DTO endpoint và fake trong
  test cũng mỗi cái một file. Mảng lớn chia thư mục con theo chức năng (`MarketData/DailyHistory/`,
  `Endpoints/Prediction/`…); thư mục con không đổi namespace. Port chỉ Signals dùng chuyển vào `Signals/Storage/`,
  `IMarkPriceClient` dùng chung chuyển vào `Abstractions/MarkPrices/`. Docs ghi quy tắc mới. Không đổi hành vi.
- **#129 Chuyển bảng giao dịch theo phiên xuống trang Dự đoán.** Card Giao dịch theo phiên (số giao dịch, khối lượng,
  mua/bán chủ động, so với trung bình 20 phiên) rời trang Biểu đồ, nằm cuối trang Dự đoán; dùng chung cache nến với
  trang Biểu đồ.

## 2026-11-07

- **#127 Sửa các lỗi review của bộ lọc controlled, lần 4** (#119–#126, review #118). Bảng Dự đoán đưa bộ lọc về rỗng
  và về trang 1 cả khi chỉ đổi chế độ lúc khung là 1d; Báo cáo ngày đưa bộ lọc về rỗng khi bảng Các ngày trước bị ẩn
  và file luôn giữ báo cáo đang hiện trên màn hình; cột Lịch sử vị thế chỉ dựng lại khi danh sách symbol đổi;
  `activeFilters` duyệt cột bằng `forEachColumn` thay vì dựng lại cây; `useScopedFilters` gọn hơn.

## 2026-11-05

- **#118 Sửa các lỗi review của bộ lọc controlled** (#110–#117, review #108). Đổi symbol/khung/chế độ lại đưa bộ lọc
  về rỗng (`useScopedFilters`, đặt lại ngay khi render) và bảng Dự đoán về trang 1; Báo cáo ngày không áp bộ lọc khi
  bảng Các ngày trước không hiện, và file luôn có báo cáo mới nhất; `withFilteredValues` nhận diện cột lọc giống antd;
  `activeFilters` và `withFilteredValues` dùng chung `mapColumns`; cột bảng và bộ lọc API được `useMemo`.
- **#108 Bộ lọc controlled cho các bảng xuất Excel** (#101–#107, review #100). Lịch sử vị thế, Dự đoán và Các ngày
  trước nhận bộ lọc qua `filteredValue` (`withFilteredValues`): bảng và file xuất đọc chung một state nên không còn lệch
  khi đổi symbol/chế độ hay khi bảng mount lại; bỏ `useTableFilters`. Đổi symbol/chế độ giờ **giữ** bộ lọc. Cảnh báo
  khi xuất 0 dòng mà dữ liệu bị cắt ở giới hạn API; `filterKey` xử lý `dataIndex` falsy giống antd; đổi tên
  `truncatedAt` → `limitIfTruncated`.

## 2026-11-04

- **#100 Sửa các lỗi review của xuất Excel** (#91–#99, review #89). Bộ lọc của bảng Dự đoán và Các ngày trước về rỗng khi đổi
  symbol/khung (trước đó file xuất vẫn lọc theo bộ lọc cũ đã mất trên bảng); `applyColumnFilters` tìm key giống antd
  (`dataIndex` mảng, vị trí cột, cột con); Báo cáo ngày xuất theo bộ lọc đang chọn; cảnh báo khi dữ liệu bị cắt ở giới
  hạn API (`API_MAX_LIMIT`); gom cách tính đúng/sai của dự đoán vào `outcomeOf`; cột Excel khai báo ngoài component.
- **#89 Xuất Excel cho các bảng trên dashboard**. Nút "Xuất Excel" (.xlsx) ở Lịch sử vị thế, Tín hiệu, Báo cáo ngày
  và Dự đoán; file giữ bộ lọc đang chọn, số là số, giờ theo giờ Việt Nam. Thêm `write-excel-file` (tải khi bấm nút),
  `components/excel.ts`, `ExportExcelButton`, `applyColumnFilters`. Không đổi backend.

## 2026-10-29

- **#59 Thêm unit test cho entity Domain và các service Application**. Không đổi code chạy.
  Domain: `AutoOrderRule`, `FuturesPosition`, `CandleInterval`, `StrategyInstance`/`TradingSignal`, `IndicatorWindow`/
  `TemplateIndicator`, thêm trường hợp biên cho `FuturesMath` và `Candle`. Application: `StrategyConfigurator`/
  `StrategyFactory`, `MarketDataStatusRegistry`, `MarkPriceCache`; thêm fake `InMemoryStrategyInstanceStore`.
  Tổng số test: Domain 185, Application 104, Integration 7.
- **#58 Tài liệu dự án**. Viết lại `README.md` thành trang vào; thêm 12 file trong `docs/` (mục lục, tính năng, kiến
  trúc, thư mục, nguyên tắc, cách viết code, test, Git/PR, cấu hình, API, mở rộng, nhật ký thay đổi).
- **#57 Refactor Domain/Prediction** (#55). Không đổi hành vi.
  `PercentChange` chuyển sang `Domain/Common` và dùng cho xu hướng/đà/`SignalAccuracy.MovePercent`;
  `PriceDirection` tách file riêng; thêm `PriceDirectionPredictor.PredictAt` đoán trên một `ListSlice` thay cho
  `Skip/Take/ToList` ở `Backtest` và endpoint `/api/prediction`. Thêm 2 test.

## 2026-10-28

- **#56 Refactor Domain/MarketData** (#55). Không đổi hành vi.
  `CandleInterval.TryParse` dùng `[MaybeNullWhen(false)]` (build 0 cảnh báo); `DailyCloses.Validate` gom kiểm tra
  chung; `PercentChange.Between/UpShare` gom công thức %; `Candle.ApplyUpdate` tách `HasSameKey/HasSameOhlcv`;
  tài liệu cho `CandleIssueKind`; `ListSlice` chuyển sang `Domain/Common`.

## 2026-10-27

- **#52 Sửa các lỗi review của dự báo ngày tương tự** (#50). Neo khoảng giá vào giá đóng hôm qua (UTC) thay vì giá
  hiện tại, cache theo ngày; bỏ qua ngày hòa và ngày đứng yên trong backtest; API trả `anchorPrice`, `targetCloseUtc`;
  UI ghi rõ khoảng dành cho giá đóng nào.
- **#53 Dark mode và trang Cài đặt** (#51). ⚠️ Merge vào `fix/similar-days-review` *sau khi* nhánh đó đã vào
  `develop`, nên **chưa có trong `develop`/`main`**. 5 commit nằm trên `origin/fix/similar-days-review`.
- **#49 Dự báo ngày tương tự cho thanh biên độ 24 giờ** (#47). `SimilarDaysForecast` (5 đặc trưng, 60 ngày giống
  nhất, khoảng 68% lệch được, backtest walk-forward), `SimilarDaysService` + cache, hiển thị dưới thanh biên độ 24h.
- **#48 Sửa bộ lọc EMA của MyRsi** (#46). Khi bật `TrendEmaPeriod`, chuyển sang kiểu hồi trong xu hướng:
  `PullbackLong` 40 / `PullbackShort` 60 (bản cũ "cắt 30/70 cùng chiều EMA" cho 0 tín hiệu). Golden cập nhật.

## 2026-10-26

- **#45 Xem lại sự kiện trong các tháng giá tăng mạnh** (#44). Thêm sự kiện tháng tăng mạnh, card chuyển qua lại
  giảm/tăng.
- **#43 Xem lại sự kiện trong các tháng giá giảm mạnh** (#42). `marketevents.json` (2016–2026), `MarketEventCatalog`,
  `/api/history/monthly` trả `events`, heatmap đánh dấu tháng có sự kiện, card tháng giảm mạnh.
- **#40 Lịch sử theo tháng + tách dashboard thành menu** (#36). `MonthlyChangeReport`, `/api/history/monthly`,
  heatmap năm × tháng, thống kê theo tháng trong năm. Dashboard tách thành menu 5 trang (#hash). Dashboard dev chuyển
  sang cổng 8090. (#34 là bản đầu, đã đóng, làm lại ở #40.)
- **#33 Lưu nến ngày BTC/ETC/ETH từ 2016 (Bitfinex)**. `IDailyHistoryClient`, `BitfinexDailyHistoryClient`,
  `DailyHistorySyncService`, `DailyHistoryWorker`.

## 2026-10-24

- **#32 Biên độ sau 7 và 30 ngày** (#31). Nút 24 giờ / 7 ngày / 30 ngày; σ 365 ngày × √N.
- **#26 Hàm dự đoán nến kế tiếp lên hay xuống** (#25). `PriceDirectionPredictor` (xu hướng + đà + lực mua), backtest,
  trang Dự đoán. Gồm PR xếp chồng **#28** (dự đoán theo 1 ngày) và **#30** (tỷ lệ đúng tín hiệu chỉ báo,
  `SignalAccuracy`).
- **#23 Biên độ giá có thể tăng/giảm sau 24 giờ** (#22). `DailyMoveRange`, `DailyRangeService`, thanh biên độ.
- **#21 Cảnh báo khi khối lượng bán/mua áp đảo** (#20). Cột `TakerBuyVolume` (migration), `VolumePressure`, cảnh báo
  ở card phiên.

## 2026-10-23

- **#19 Số giao dịch và khối lượng theo phiên 15 phút** (#18). Cột `QuoteVolume`, `TradeCount` (migration), tải bù
  cho nến cũ, `SessionStatsCard`.
- **#17 Tự đóng lệnh khi chỉ báo đảo chiều**. `CloseOnReverse` trên quy tắc theo chỉ báo, `CloseReason.Signal`
  (migration).
- **#14 Chú thích dễ hiểu cho hàm dự đoán** (#13). Chú thích dày có ví dụ số cho `EmaCrossStrategy`, `MyRsiIndicator`,
  `Crossover`, `Ema`, `Rsi`. Gồm PR xếp chồng **#15**: bỏ `var` trong Domain, dùng kiểu tường minh.
- **#12 Chú thích tiếng Việt cho các entity trong Domain** (#11).
- **#10 Tài liệu hàm sinh tín hiệu**. [signal-functions.md](signal-functions.md).
- **#9 Tách nhỏ và tối ưu hàm sinh tín hiệu** (#8). `Ema.LastTwo`, `Rsi.LastTwo`, `Crossover`, `ListSlice`,
  `GapIndex`; golden test 3,026 dòng bảo đảm kết quả không đổi.

## 2026-10-19

- **#7 Chỉ báo tự viết (C#)** (#6). `ICustomIndicator`, `[Indicator]`, `IndicatorRegistry`, `MyRsiIndicator`,
  `TemplateIndicator`, chart chỉ báo, quy tắc tự động theo chỉ báo (migration `IndicatorAutoOrders`).
- **#4 Tự mở lệnh Paper khi mark price chạm mức** (#3). `AutoOrderRule`, `AutoOrderService`, giới hạn lỗ ngày
  (migration `AutoOrders`).

## 2026-10-18

- **#2 Bảng giá trực tiếp Spot + Mark mỗi 15 giây** (#1).
- **Commit đầu tiên:** vertical slice EMA 9/21 (đồng bộ nến Binance, đánh giá tín hiệu, SQLite, dashboard) và Paper
  Futures (isolated, one-way, SL bắt buộc, giá thanh lý, sổ vốn). Migration `InitialCreate`, `PaperFutures`.

## Đưa lên `main`

PR "Develop" (`develop → main`): #5 (2026-10-19), #16 (2026-10-23), #24 (2026-10-24), #41 (2026-10-26),
#54 (2026-10-27), #60 (2026-10-29, gồm #56–#59).

Issue #55 (refactor `Domain/MarketData`, `Domain/Prediction`) vẫn mở: PR vào `develop` không tự đóng issue, cần
đóng tay khi xong.
