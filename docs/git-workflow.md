# Quy trình Git và PR

## Nhánh

```text
main  ←  develop  ←  feature/… | fix/… | refactor/… | docs/…
```

| Nhánh | Vai trò |
| --- | --- |
| `main` | Bản ổn định. Chỉ nhận PR "Develop" từ `develop` khi một đợt tính năng xong |
| `develop` | Nhánh tích hợp. **Mọi PR tính năng mở vào đây** |
| `feature/<tên>` | Tính năng mới, ví dụ `feature/similar-days`, `feature/auto-close-on-reverse` |
| `fix/<tên>` | Sửa lỗi hoặc sửa theo review, ví dụ `fix/rsi-trend-filter`, `fix/similar-days-review` |
| `refactor/<tên>` | Dọn code không đổi hành vi, ví dụ `refactor/market-data`, `refactor/prediction` |
| `test/<tên>` | Chỉ thêm/sửa test, ví dụ `test/more-unit-tests` |
| `docs/<tên>` | Chỉ tài liệu, ví dụ `docs/signal-functions` |

Tên nhánh tiếng Anh, chữ thường, nối bằng `-`. Tạo nhánh mới từ `origin/develop` mới nhất:

```bash
git fetch origin
git switch -c feature/ten-tinh-nang origin/develop
```

Không dùng lại nhánh đã merge cho việc mới: tạo nhánh mới từ `develop` (ví dụ `refactor/prediction` được tạo mới sau
khi `refactor/market-data` đã merge).

## Issue

Mỗi việc có một issue trên GitHub. Số issue nằm trong **mọi commit** của việc đó: `(#55)`.
PR ghi `Closes #N` để đóng issue khi merge, hoặc `Refs #N` nếu chỉ là một phần.

> `Closes #N` chỉ tự đóng issue khi PR merge vào **nhánh mặc định** (`main`). PR vào `develop` thì issue đóng khi
> đợt "Develop → main" được merge, hoặc đóng tay.

## Commit

- **Tiếng Anh, câu mệnh lệnh, viết hoa chữ đầu, không dấu chấm cuối**, kết thúc bằng số issue:
  `Add SimilarDaysForecast: outlook from the most similar past days (#47)`,
  `Skip ties and flat days in the similar-days backtest (#50)`,
  `Move PriceDirection to its own file (#55)`.
- Dòng tiêu đề nói **thay đổi gì**, ngắn (≲ 72 ký tự). Thân (sau một dòng trống) nói **vì sao**, khi lý do không hiển
  nhiên:

  ```text
  Share daily-close input checks between range and similar-days (#55)

  DailyMoveRange and SimilarDaysForecast repeated the same horizon and
  positive-close checks with the same messages; both now call
  DailyCloses.Validate.
  ```

- **Commit nhỏ, mỗi commit một ý**, và mỗi commit build + test được. Tính năng lớn thường đi từ dưới lên:
  1. Domain (công thức/entity) → 2. test Domain → 3. service Application → 4. test service → 5. đăng ký DI →
  6. endpoint → 7. kiểu trong `api/client.ts` → 8. card/trang dashboard → 9. tài liệu.
  Ví dụ chuỗi commit của #47 (ngày tương tự) trong `git log`.
- Refactor tách riêng khỏi thay đổi hành vi (không trộn trong một commit).
- Commit có trợ lý AI tham gia kết thúc bằng dòng `Co-Authored-By: ...` tương ứng.

## Pull request

- Mở vào `develop`. Có thể mở **nháp** (`gh pr create --draft`) khi còn làm.
- **Tiêu đề tiếng Việt**, ngắn: `Dự báo ngày tương tự cho thanh biên độ 24 giờ`, `Refactor Domain/Prediction`.
- **Mô tả tiếng Việt** theo khung:

  ```markdown
  Closes #N

  <1–2 câu: PR làm gì, có đổi hành vi không>

  ## Tóm tắt
  - <thay đổi chính, mỗi dòng một ý, dùng `tên code`>

  ## Commit            (tùy chọn, khi có nhiều commit cần đọc theo thứ tự)
  1. ...

  ## Kiểm tra
  - `dotnet build`: 0 lỗi, 0 cảnh báo.
  - `dotnet test`: Domain X, Application Y, Integration Z, tất cả qua.
  - <kiểm tra tay / ảnh chụp nếu đổi UI>
  ```

- PR xếp chồng (base là nhánh của PR khác) ghi rõ ở đầu: "PR xếp chồng lên #52, merge #52 trước".

  > **Cẩn thận với PR xếp chồng:** nếu PR dưới merge vào `develop` **trước** khi PR trên được merge, GitHub không
  > phải lúc nào cũng tự đổi base. PR #53 (dark mode) đã bị merge vào `fix/similar-days-review` sau khi nhánh đó vào
  > `develop`, nên dark mode không bao giờ tới `develop`. Trước khi merge PR xếp chồng, kiểm tra base là `develop`.

- Merge bằng **merge commit** (giữ lịch sử từng commit nhỏ), không squash.
- Sửa theo review: commit mới trên cùng nhánh, hoặc nhánh `fix/...-review` nếu PR gốc đã merge.

## Đưa `develop` lên `main`

Khi một đợt tính năng ổn định: mở PR `develop → main` tiêu đề "Develop", merge commit.

## Không commit

`bin/`, `obj/`, `data/`, `*.db`, `*.db-shm`, `*.db-wal`, `src/CryptoBot.Api/wwwroot/`, `node_modules/`,
`*.tsbuildinfo`, `.env`. Không commit API key hay bí mật (dự án hiện không cần bí mật nào).
