"""Phần 2 của báo cáo: sau một cây nến 1h sập mạnh, giá có hồi lên 100–200$ không.

Dùng btcusdt-1h.csv để đo nhịp hồi sau mỗi cú sập, và btcusdt-1m-after-drops.csv để xem mốc chốt lời hay
mốc cắt lỗ được chạm trước khi vào lệnh Long ngay lúc cây nến sập đóng cửa.

Chạy: python3 rebounds_after_drops.py
"""
import csv
import statistics
from collections import defaultdict
from pathlib import Path

DATA = Path(__file__).resolve().parent.parent / "data"
DROP_USD = 500


def read(name):
    with open(DATA / name) as file:
        return list(csv.DictReader(file))


candles = [dict(time=row["open_time_utc"], high=float(row["high"]), low=float(row["low"]),
                close=float(row["close"])) for row in read("btcusdt-1h.csv")]
indexes = range(1, len(candles) - 3)


def change(index):
    return candles[index]["close"] - candles[index - 1]["close"]


def percent(index):
    return (candles[index]["close"] / candles[index - 1]["close"] - 1) * 100


def share(group, condition):
    return f"{100 * sum(condition(index) for index in group) / len(group):.0f}%"


def rebound_row(name, group):
    entry = lambda index: candles[index]["close"]
    high_after = lambda index, hours: max(candles[later]["high"] for later in range(index + 1, index + 1 + hours))
    cells = [
        name, str(len(group)),
        share(group, lambda index: candles[index + 1]["close"] > entry(index)),
        share(group, lambda index: high_after(index, 1) - entry(index) >= 100),
        share(group, lambda index: high_after(index, 1) - entry(index) >= 200),
        share(group, lambda index: high_after(index, 3) - entry(index) >= 200),
        share(group, lambda index: candles[index + 1]["close"] - entry(index) >= 100),
        share(group, lambda index: candles[index + 1]["close"] - entry(index) >= 200),
        share(group, lambda index: entry(index) - candles[index + 1]["low"] >= 200),
        f"{statistics.median(candles[index + 1]['close'] - entry(index) for index in group):+.0f}$",
        f"{statistics.median(candles[index + 3]['close'] - entry(index) for index in group):+.0f}$",
    ]
    print("| " + " | ".join(cells) + " |")


print(f"Dữ liệu: {len(candles)} nến 1h, {candles[0]['time']} đến {candles[-1]['time']}")
print(f"Giá thấp nhất {min(candle['low'] for candle in candles):.0f}$, cao nhất "
      f"{max(candle['high'] for candle in candles):.0f}$\n")
print("| Nhóm | Số lần | Nến sau đóng tăng | Chạm +100$ trong 1h | Chạm +200$ trong 1h | Chạm +200$ trong 3h "
      "| Đóng +100$ sau 1h | Đóng +200$ sau 1h | Giảm thêm 200$ trong 1h | Trung vị sau 1h | Trung vị sau 3h |")
print("|" + "---|" * 11)
rebound_row("Mọi nến (mức nền)", list(indexes))
for threshold in [300, 500, 800, 1000]:
    rebound_row(f"Giảm >= {threshold}$", [index for index in indexes if change(index) <= -threshold])
for threshold in [0.5, 1.0, 1.5]:
    rebound_row(f"Giảm >= {threshold}%", [index for index in indexes if percent(index) <= -threshold])
rebound_row(f"Giảm >= {DROP_USD}$, nến trước không giảm",
            [index for index in indexes if change(index) <= -DROP_USD and change(index - 1) >= 0])
rebound_row(f"Giảm >= {DROP_USD}$, nến trước cũng giảm",
            [index for index in indexes if change(index) <= -DROP_USD and change(index - 1) < 0])

half = len(candles) // 2
print()
for name, part in [("Nửa đầu", range(1, half)), ("Nửa sau", range(half, len(candles) - 3))]:
    group = [index for index in part if change(index) <= -DROP_USD]
    print(f"{name}: {len(group)} lần sập >= {DROP_USD}$, nến sau đóng tăng "
          f"{share(group, lambda index: candles[index + 1]['close'] > candles[index]['close'])}")

# Mốc nào chạm trước: nến 1m trong 3 giờ sau mỗi cú sập.
minutes = defaultdict(list)
for row in read("btcusdt-1m-after-drops.csv"):
    minutes[row["drop_candle_open_time_utc"]].append((float(row["high"]), float(row["low"])))
entry_price = {candle["time"]: candle["close"] for candle in candles}

print(f"\nLệnh Long vào lúc cây nến sập >= {DROP_USD}$ đóng cửa, {len(minutes)} lần:\n")
print("| Chốt lời / cắt lỗ | Chạm chốt lời trước | Chạm cắt lỗ trước | Cùng một phút | Không chạm trong 3h "
      "| Thị trường ngẫu nhiên | Lãi trung bình mỗi lệnh 1 BTC trước phí |")
print("|---|---|---|---|---|---|---|")
for take_profit, stop_loss in [(100, 200), (100, 300), (200, 200), (200, 300)]:
    outcomes = defaultdict(int)
    for drop_time, bars in minutes.items():
        entry = entry_price[drop_time]
        result = "none"
        for high, low in bars:
            hit_profit, hit_loss = high - entry >= take_profit, entry - low >= stop_loss
            if hit_profit or hit_loss:
                result = "both" if hit_profit and hit_loss else "profit" if hit_profit else "loss"
                break
        outcomes[result] += 1
    total = len(minutes)
    expected = (outcomes["profit"] * take_profit - outcomes["loss"] * stop_loss) / total
    print(f"| +{take_profit}$ / -{stop_loss}$ | {100 * outcomes['profit'] / total:.0f}% "
          f"| {100 * outcomes['loss'] / total:.0f}% | {100 * outcomes['both'] / total:.0f}% "
          f"| {100 * outcomes['none'] / total:.0f}% | {100 * stop_loss / (take_profit + stop_loss):.0f}% "
          f"| {expected:+.0f}$ |")
