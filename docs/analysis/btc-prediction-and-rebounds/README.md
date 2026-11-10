# Phân tích BTCUSDT: dự đoán sai, nhịp hồi sau sập và DCA

Ngày phân tích: 2026-11-10. Mọi giờ trong báo cáo là giờ Việt Nam (UTC+7), trừ tên cột trong file CSV.

Báo cáo trả lời ba câu hỏi:

1. Vì sao hàm dự đoán nến kế tiếp trên trang Dự đoán hay sai ở các nến biến động mạnh?
2. Sau một cây nến sập mạnh, giá có thường hồi lên 100–200$ không, và có kiếm được tiền từ đó không?
3. Đánh DCA, cả DCA định kỳ lẫn bot DCA bình quân giá xuống, thì kết quả ra sao trên cùng dữ liệu?

## Kết luận nhanh

- **Hàm dự đoán không có lợi thế.** Ở các nến biến động từ 80$ trở lên, nó đúng 50%, bằng tung đồng xu. Điểm càng
  cao lại càng không đáng tin: nhóm điểm từ 2 trở lên chỉ đúng 41%.
- **Không có mẫu hình đơn giản nào để sửa.** "Nến trước có khối lượng lớn thì hay đoán sai" đúng trên 300 nến gần nhất
  nhưng sai trên các nến cũ hơn.
- **Sau sập, giá đúng là hay chạm +100$ đến +200$**, nhưng cũng hay rơi thêm 200$. Tỷ lệ chạm chốt lời trước cắt lỗ
  chỉ cao hơn thị trường ngẫu nhiên 1 đến 3 điểm phần trăm, và phí giao dịch lớn hơn phần lãi đó.
- **Bot DCA thắng nhiều lệnh nhỏ nhưng có thể kẹt vốn gần một năm.** Bắt đầu sát đỉnh thì lỗ 22.5% vốn. Dùng đòn bẩy
  khoảng 2.5x trở lên là bị thanh lý trong đợt giảm từ 108,000$ xuống 58,000$.

## Dữ liệu

Tất cả nằm trong [data/](data/). Nến đều là BTCUSDT Spot của Binance.

| File | Nguồn | Nội dung |
| --- | --- | --- |
| [btcusdt-1h.csv](data/btcusdt-1h.csv) | REST công khai Binance | 8,999 nến 1h, mở từ 31/10/2025 23:00 đến 10/11/2026 21:00 |
| [btcusdt-1m-after-drops.csv](data/btcusdt-1m-after-drops.csv) | REST công khai Binance | 180 nến 1m ngay sau mỗi nến 1h sập từ 500$ trở lên, 484 lần |
| [prediction-snapshot-recent.csv](data/prediction-snapshot-recent.csv) | API của bot, `/api/prediction` | 300 lần đoán gần nhất, xuất lúc khoảng 21:55 ngày 10/11/2026 |
| [prediction-snapshot-candles-1h.csv](data/prediction-snapshot-candles-1h.csv) | API của bot, `/api/market/candles` | 999 nến 1h đã đóng cùng lúc, kèm khối lượng mua/bán chủ động |

Trong khoảng một năm này BTC đi từ 108,845$ xuống 74,400$, thấp nhất 57,800$ và cao nhất 111,250$. Đây là một năm giá
giảm, nên mọi kết luận về DCA cần đọc với bối cảnh đó.

## 1. Vì sao dự đoán sai

Trang Dự đoán giờ hiện 300 lần đoán gần nhất thay vì 20, để có đủ mẫu phân tích. Hàm dự đoán nằm ở
[PriceDirectionPredictor.cs](../../../src/CryptoBot.Domain/Prediction/DirectionPredictor/PriceDirectionPredictor.cs).
Điểm của nó là tổng ba thành phần:

- **Xu hướng:** giá đóng nằm trên hay dưới trung bình 20 nến bao nhiêu %, trọng số 1.
- **Đà:** giá đã đi bao nhiêu % trong 3 nến gần nhất, trọng số 1.
- **Lực mua/bán:** mua chủ động hơn hay kém 50% bao nhiêu điểm ở nến cuối, trọng số 0.05.

Điểm từ 0 trở lên là đoán Lên, dưới 0 là đoán Xuống.

### Tỷ lệ đúng

Chỉ xét nến biến động từ 80$ trở lên, như yêu cầu.

| Nhóm | Số lần | Tỷ lệ đúng |
| --- | --- | --- |
| Biến động từ 80$ trở lên | 197 | 50% |
| Biến động dưới 80$ | 102 | 49% |
| Biến động lớn, điểm từ 2 trở lên | 51 | 41% |

### Các lần sai lớn nhất

| Giờ | Đoán | Thực tế | Điểm | Bối cảnh |
| --- | --- | --- | --- | --- |
| 03:00 30/10 | Lên | -1,997$ | +0.20 | Cú sập đột ngột, điểm gần bằng 0 |
| 11:00 04/11 | Xuống | +894$ | -2.07 | Khối lượng nến trước gấp 3.1 lần trung bình |
| 10:00 30/10 | Xuống | +799$ | -3.76 | Giá đã bật lên 599$ ở nến trước |
| 19:00 05/11 | Lên | -728$ | +0.05 | Điểm gần bằng 0 |
| 15:00 09/11 | Xuống | +701$ | -3.15 | Sau 5 nến giảm liên tiếp |
| 21:00 10/11 | Xuống | +651$ | -2.07 | Khối lượng nến trước gấp 4.2 lần trung bình |
| 09:00 30/10 | Xuống | +599$ | -6.62 | Sau 4 nến giảm liên tiếp |

### Nguyên nhân

- **Hàm chỉ đi theo xu hướng.** Cả xu hướng lẫn đà đều nhìn về quá khứ, nên hàm luôn đoán giá đi tiếp theo hướng cũ.
- **Hai thành phần chính gần như là một.** Xu hướng và đà cùng chiều trong khoảng 70% số lần, nên điểm đếm một tín
  hiệu hai lần. Lực mua/bán có trọng số 0.05, gần như không bao giờ đổi được kết quả.
- **Điểm cao nhất đến khi giá đã chạy xong.** Sau 4 hoặc 5 nến cùng chiều, điểm lên 3 đến 6, đúng lúc giá hay hồi
  lại. Nhóm đoán sai có điểm trung bình 1.63, cao hơn nhóm đoán đúng là 1.36.
- **Lần sai lớn nhất là một cú sốc.** Cú giảm 1,997$ lúc 03:00 ngày 30/10 xảy ra khi điểm gần bằng 0. Chỉ báo dựa
  trên giá đóng cửa không thấy trước loại biến động đến từ tin tức hay thanh lý hàng loạt.
- **Biến động 1 giờ của BTC gần như ngẫu nhiên.** Quy tắc ngược lại, đoán nến sau đảo chiều nến trước, cũng chỉ
  đúng khoảng 50%.

### Mẫu hình không đứng vững

Trên 300 nến gần nhất, khi nến trước có khối lượng gấp 1.5 đến 2.5 lần trung bình 20 nến, hàm chỉ đúng 5 trên 22 lần.
Kiểm tra lại trên các nến cũ hơn nằm ngoài 300 lần đoán thì điều kiện đó đúng 54%:

| Điều kiện | Nến cũ hơn | 300 nến gần nhất |
| --- | --- | --- |
| Tất cả biến động lớn | 48% (436 lần) | 50% (197 lần) |
| Nến trước có khối lượng từ 1.5 lần trở lên | 54% (89 lần) | 31% (36 lần) |
| Điểm từ 2 trở lên | 50% (70 lần) | 41% (51 lần) |

Sửa hàm theo mẫu hình này nhiều khả năng chỉ là khớp theo dữ liệu gần đây.

## 2. Nhịp hồi sau một cú sập

Một lần "sập" là một nến 1h đóng thấp hơn nến trước từ 500$ trở lên. Trong năm có 484 lần.

### Giá có hồi không

| Sau cây nến | Sập từ 500$ | Mọi nến |
| --- | --- | --- |
| Nến kế tiếp đóng tăng | 55% | 50% |
| Chạm +100$ trong 1 giờ | 85% | 63% |
| Chạm +200$ trong 1 giờ | 69% | 39% |
| Chạm +200$ trong 3 giờ | 78% | 60% |
| Nến kế tiếp đóng trên +100$ | 47% | 32% |
| Giảm thêm 200$ trong 1 giờ | 63% | 41% |
| Trung vị thay đổi sau 1 giờ | +47$ | 0$ |
| Trung vị thay đổi sau 3 giờ | +3$ | -4$ |

Sau một cú sập, giá dao động mạnh hơn theo cả hai chiều. Nó dễ chạm +200$ nhưng cũng dễ rơi thêm 200$. Kết quả giống
nhau khi đổi mốc sập thành 300$, 800$, 1,000$ hay 0.5% đến 1.5%. Nó cũng giống nhau ở nửa đầu và nửa sau của năm: nến
kế tiếp đóng tăng 55% ở cả hai nửa.

### Mốc nào chạm trước

Giả lập lệnh Long vào ngay khi cây nến sập đóng cửa, rồi dùng nến 1 phút để xem mốc chốt lời hay mốc cắt lỗ chạm
trước trong 3 giờ.

| Chốt lời / cắt lỗ | Chạm chốt lời trước | Thị trường ngẫu nhiên | Lãi trung bình mỗi lệnh 1 BTC trước phí |
| --- | --- | --- | --- |
| +100$ / -200$ | 68% | 67% | +3$ |
| +100$ / -300$ | 78% | 75% | +11$ |
| +200$ / -200$ | 49% | 50% | -3$ |
| +200$ / -300$ | 62% | 60% | +11$ |

Cột "Thị trường ngẫu nhiên" là tỷ lệ chạm chốt lời trước nếu giá đi hoàn toàn ngẫu nhiên, bằng cắt lỗ chia cho tổng
chốt lời và cắt lỗ. Chốt lời gần hơn cắt lỗ thì chốt lời chạm thường xuyên hơn. Đó là toán học, không phải tín hiệu.

Phí taker Futures của Binance khoảng 0.05% mỗi chiều, tức khoảng 80$ cho một lần mở và đóng lệnh 1 BTC ở giá quanh
80,000$. Phí lớn hơn phần lãi trung bình ở cả bốn cặp mốc.

## 3. DCA

DCA có hai dạng:

- **DCA định kỳ:** mua một số tiền cố định theo lịch, bất kể giá. Đây là cách tích trữ dài hạn.
- **Bot DCA bình quân giá xuống:** mở một lệnh nhỏ. Mỗi khi giá giảm thêm một mức, bot mua thêm một lệnh lớn hơn để
  kéo giá trung bình xuống. Khi giá hồi lên một chút so với giá trung bình, bot chốt cả lệnh. Dạng này dựa đúng vào ý
  "sập xong sẽ hồi" ở phần 2.

### DCA định kỳ

| Cách mua | Kết quả cuối kỳ |
| --- | --- |
| Mua một lần ngày 31/10/2025 | -31.6% |
| 100$ mỗi tuần, tổng 5,400$ | -1.7% |
| Lúc lỗ nặng nhất khi mua mỗi tuần | -34.4% |

Mua đều đặn kéo giá mua trung bình xuống còn 75,719$. Cách này chỉ có lãi khi giá về sau tăng lại.

### Bot DCA bình quân giá xuống

Cấu hình: Spot, không đòn bẩy, không cắt lỗ. Lệnh gốc 100$. Mua thêm mỗi khi giá giảm 2% so với lần mua trước, tối
đa 6 lần, mỗi lần lớn hơn lần trước 1.5 lần. Chốt lời khi giá cao hơn giá trung bình 1.5%. Phí 0.1% mỗi chiều. Bot cần
giữ sẵn 3,217$ để mua đủ cả 6 lần, và tỷ lệ phần trăm bên dưới tính trên số vốn đó.

| Ngày bắt đầu | Giá BTC | Số lệnh chốt lời | Lỗ nặng nhất trên vốn | Lệnh lâu nhất | Tổng kết quả |
| --- | --- | --- | --- | --- | --- |
| 31/10/2025 | 108,845$ | 6 | -41% | 364 ngày | -22.5% |
| 30/11/2025 | 91,825$ | 33 | -29% | 236 ngày | +6.6% |
| 29/01/2026 | 85,270$ | 83 | -23% | 99 ngày | +14.3% |
| 29/04/2026 | 75,804$ | 25 | -23% | 99 ngày | +4.1% |
| 28/06/2026 | 59,691$ | 43 | -2% | 14 ngày | +4.2% |
| 26/09/2026 | 84,120$ | 6 | -2% | 14 ngày | +0.7% |

Bảng đầy đủ theo từng tháng có trong kết quả của [scripts/dca.py](scripts/dca.py).

- **Thắng nhiều nhưng lãi mỏng.** Lệnh nào chốt được cũng có lãi, nhưng mỗi lệnh chỉ vài đô la.
- **Thua là kẹt vốn rất lâu.** Bắt đầu sát đỉnh, bot mua hết 6 lần rồi giữ lệnh lỗ cả năm, có lúc lỗ 41% vốn.
- **Đòn bẩy rất nguy hiểm.** Lỗ 41% vốn khi không đòn bẩy tương đương bị thanh lý ở khoảng 2.5x. Bot dùng 5x hay 10x
  sẽ cháy trong đợt giảm từ 108,000$ xuống 58,000$.
- **Lãi đến từ việc giá quay lại vùng đã mua**, không phải từ khả năng dự đoán, đúng như phần 2.

Thử thêm hai cấu hình khác cho cả năm, bước 1% chốt 1% và bước 1% chốt 0.5%. Cả hai đều kẹt lệnh hơn 360 ngày và
kết thúc ở khoảng -23% đến -30% vốn.

## Giới hạn

- Dữ liệu chỉ có khoảng một năm, và đó là một năm giá giảm.
- Phần 1 chỉ có 300 lần đoán, nên chênh lệch vài điểm phần trăm là nhiễu.
- Bot DCA mô phỏng trên nến 1h nên không biết giá chạm mốc nào trước trong cùng một giờ.
- Phí được ước lượng theo mức taker thông thường, chưa tính funding hay trượt giá.
- Đây là phân tích để học, không phải lời khuyên đầu tư.

## Chạy lại

Chỉ cần Python 3, không cần thư viện ngoài.

```bash
cd docs/analysis/btc-prediction-and-rebounds/scripts
python3 prediction_errors.py      # Phần 1
python3 rebounds_after_drops.py   # Phần 2
python3 dca.py                    # Phần 3
```

Tải lại dữ liệu Binance cho đúng khoảng thời gian trên, mất khoảng 1–2 phút:

```bash
python3 fetch_binance.py
```

Xuất ảnh chụp dự đoán mới khi API đang chạy ở cổng 5080. Ảnh chụp mới sẽ ghi đè file cũ và cho số khác với báo cáo:

```bash
python3 export_snapshot.py
```
