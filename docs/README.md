# Mục lục tài liệu

Tài liệu viết cho người phát triển dự án: người sửa code, thêm tính năng, review PR (kể cả trợ lý AI).
Cập nhật lần cuối: 2026-10-29, sau PR #57.

## Bắt đầu

| File | Đọc khi |
| --- | --- |
| [features.md](features.md) | Muốn biết bot đang làm được gì, mỗi tính năng chạy theo quy tắc nào và còn thiếu gì |
| [architecture.md](architecture.md) | Muốn hiểu các tầng, ai gọi ai, các worker chạy nền, dữ liệu đi từ sàn tới dashboard ra sao |
| [folder-structure.md](folder-structure.md) | Đang tìm một file, hoặc chưa biết đặt code mới ở thư mục nào |

## Viết code

| File | Nội dung |
| --- | --- |
| [principles.md](principles.md) | Các nguyên tắc bắt buộc và lý do: decimal, UTC, nến đã đóng, cửa sổ cố định, idempotent, chỉ Paper |
| [coding-conventions.md](coding-conventions.md) | Quy ước đặt tên, kiểu dữ liệu, chú thích, xử lý lỗi cho C# và TypeScript |
| [extending.md](extending.md) | Từng bước: thêm chỉ báo, chiến lược, endpoint, cấu hình, migration, trang/card dashboard |
| [testing.md](testing.md) | 3 project test, fake, golden test, cách đặt tên và viết test |
| [git-workflow.md](git-workflow.md) | Nhánh, commit, PR, merge vào `develop` và `main` |

## Tham khảo

| File | Nội dung |
| --- | --- |
| [configuration.md](configuration.md) | Mọi section của `appsettings.json` và `marketevents.json` |
| [api.md](api.md) | Danh sách endpoint REST, tham số, dữ liệu trả về |
| [signal-functions.md](signal-functions.md) | Chi tiết hàm sinh tín hiệu EMA cross và MyRsi, phần tối ưu, golden test |
| [changelog.md](changelog.md) | Những gì đã làm, theo từng PR và ngày |

## Giữ tài liệu đúng với code

- PR đổi hành vi (luật vào lệnh, cấu hình mới, endpoint mới, thư mục mới) cập nhật luôn file tài liệu liên quan
  trong cùng PR.
- Thêm một dòng vào [changelog.md](changelog.md) cho mỗi PR được merge vào `develop`.
- Tài liệu dẫn tới file bằng link tương đối (`../src/...`) để bấm được trên GitHub; đổi tên/chuyển file thì sửa link.
