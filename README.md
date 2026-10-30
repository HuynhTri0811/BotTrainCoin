# CryptoBot (BotTrainCoin)

Bot crypto chạy **chỉ trên tiền giả lập (Paper)**: tải nến Binance, tính chỉ báo trên nến đã đóng, phát tín hiệu,
mở/đóng lệnh Futures giả lập (tay hoặc tự động), và hiển thị mọi thứ trên một dashboard React.
Live bị khóa: không có API key, không có đường nào tới tiền thật.

- **Backend:** .NET 10, ASP.NET Core Minimal API, EF Core + SQLite, Serilog.
- **Frontend:** React 19 + TypeScript, Vite, Ant Design 6, TanStack Query, Lightweight Charts.
- **Dữ liệu:** Binance (Spot + USDⓈ-M Futures, REST công khai) và Bitfinex (nến ngày từ 2016).

## Chạy nhanh

```bash
# Backend: http://localhost:5080 (migration tự chạy khi khởi động)
dotnet run --project src/CryptoBot.Api --launch-profile http

# Dashboard dev: http://localhost:8090 (proxy /api và /health sang 5080)
npm --prefix src/cryptobot-dashboard install
npm --prefix src/cryptobot-dashboard run dev

# Toàn bộ test
dotnet test
```

Production: `npm --prefix src/cryptobot-dashboard run build` xuất bản build vào `src/CryptoBot.Api/wwwroot`,
API phục vụ luôn file tĩnh.
Database SQLite nằm ở `src/CryptoBot.Api/data/cryptobot.db`, tự tạo khi chạy lần đầu.

## Dashboard có gì

| Trang | Nội dung |
| --- | --- |
| Biểu đồ | Nến + EMA 9/21 + chỉ báo tự viết, tín hiệu BUY/SELL, bảng giá trực tiếp, biên độ 24h/7d/30d, thống kê phiên và cảnh báo mua/bán áp đảo |
| Giao dịch Paper | Mở Long/Short có đòn bẩy, SL/TP, giá thanh lý; quy tắc tự mở lệnh theo giá hoặc theo chỉ báo; vị thế và lịch sử |
| Dự đoán | Hàm đoán nến kế tiếp lên/xuống kèm tỷ lệ đúng khi chạy thử; tỷ lệ đúng tín hiệu của từng chỉ báo |
| Tín hiệu | Các tín hiệu BUY/SELL gần đây của chiến lược EMA |
| Lịch sử theo tháng | Heatmap % thay đổi theo tháng từ 2016, thống kê theo tháng trong năm, sự kiện thị trường cùng tháng |
| Báo cáo ngày | Báo cáo biến động 24 giờ lập lúc 06:00 giờ VN: cao/thấp, giảm sâu nhất từ đỉnh, giờ khối lượng bất thường |

## Tài liệu

Mục lục đầy đủ ở [docs/README.md](docs/README.md). Nên đọc theo thứ tự:

1. [Tính năng đã làm](docs/features.md): mỗi tính năng làm gì, quy tắc và giới hạn.
2. [Kiến trúc](docs/architecture.md): các tầng, luồng chạy, worker, database.
3. [Tổ chức thư mục](docs/folder-structure.md): file nào nằm ở đâu, code mới đặt ở đâu.
4. [Nguyên tắc](docs/principles.md): các quy tắc không được phá (decimal, UTC, nến đã đóng, chỉ Paper…).
5. [Cách viết code](docs/coding-conventions.md): quy ước C# và TypeScript.
6. [Hướng dẫn mở rộng](docs/extending.md): thêm chỉ báo, endpoint, migration, trang dashboard.
7. [Test](docs/testing.md) và [Quy trình Git/PR](docs/git-workflow.md).

Tham khảo: [Cấu hình](docs/configuration.md), [API](docs/api.md), [Nhật ký thay đổi](docs/changelog.md),
[Hàm sinh tín hiệu](docs/signal-functions.md).

> Đây là công cụ học và thử nghiệm. Tín hiệu, dự đoán và tỷ lệ đúng trong dashboard không phải lời khuyên đầu tư.
