# Nhật ký thay đổi

Những gì đã làm, theo PR đã merge vào `develop`, mới nhất ở trên. Số trong ngoặc là issue.
Chi tiết từng tính năng: [features.md](features.md).

## 2026-10-29

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
#54 (2026-10-27). Các PR #56, #57 hiện mới ở `develop`.
