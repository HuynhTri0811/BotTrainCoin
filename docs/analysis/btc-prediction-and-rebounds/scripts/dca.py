"""Phần 3 của báo cáo: DCA định kỳ và bot DCA bình quân giá xuống trên nến 1h BTCUSDT.

Bot DCA (Spot, không đòn bẩy, không cắt lỗ): mở lệnh gốc, mỗi khi giá giảm thêm `step` so với lần mua trước thì mua
thêm một lệnh lớn hơn `scale` lần, tối đa `safety_orders` lần; chốt cả lệnh khi giá cao hơn giá trung bình `take_profit`.
Mô phỏng trên nến 1h nên không biết thứ tự chạm trong một cây nến; tính phí 0.1% mỗi chiều.

Chạy: python3 dca.py
"""
import csv
import time
from pathlib import Path

DATA = Path(__file__).resolve().parent.parent / "data"
HOURS_PER_WEEK = 168
HOURS_PER_MONTH = 720


def epoch(text):
    return time.mktime(time.strptime(text, "%Y-%m-%dT%H:%M:%SZ"))


with open(DATA / "btcusdt-1h.csv") as file:
    all_candles = [dict(time=epoch(row["open_time_utc"]), date=row["open_time_utc"][:10], high=float(row["high"]),
                        low=float(row["low"]), close=float(row["close"])) for row in csv.DictReader(file)]


def weekly_dca(candles, amount=100):
    bought = spent = 0.0
    worst = 0.0
    for index, candle in enumerate(candles):
        if index % HOURS_PER_WEEK == 0:
            bought += amount / candle["close"]
            spent += amount
        worst = min(worst, bought * candle["low"] / spent - 1)
    return spent, bought * candles[-1]["close"], spent / bought, worst


def dca_bot(candles, step, take_profit, safety_orders, scale, base=100.0, fee=0.001):
    capital = base * sum(scale ** order for order in range(safety_orders + 1))
    deals = 0
    realized = 0.0
    worst = 0.0
    longest = 0.0
    deal = None
    for candle in candles:
        if deal is None:
            deal = dict(quantity=base / candle["close"], cost=base, last_price=candle["close"], orders=0,
                        start=candle["time"], size=base)
            continue
        average = deal["cost"] / deal["quantity"]
        if candle["high"] >= average * (1 + take_profit):
            realized += deal["quantity"] * average * (1 + take_profit) - deal["cost"] - deal["cost"] * fee * 2
            deals += 1
            longest = max(longest, candle["time"] - deal["start"])
            deal = None
            continue
        while deal["orders"] < safety_orders and candle["low"] <= deal["last_price"] * (1 - step):
            price = deal["last_price"] * (1 - step)
            deal["size"] *= scale
            deal["quantity"] += deal["size"] / price
            deal["cost"] += deal["size"]
            deal["last_price"] = price
            deal["orders"] += 1
        worst = min(worst, deal["quantity"] * candle["low"] - deal["cost"])
    unrealized = 0.0
    if deal is not None:
        unrealized = deal["quantity"] * candles[-1]["close"] - deal["cost"]
        longest = max(longest, candles[-1]["time"] - deal["start"])
    return dict(capital=capital, deals=deals, realized=realized, unrealized=unrealized, worst=worst,
                longest_days=longest / 86400, still_open=deal is not None)


first, last = all_candles[0], all_candles[-1]
print(f"BTC {first['date']}: {first['close']:.0f}$, {last['date']}: {last['close']:.0f}$")
spent, value, average_price, worst = weekly_dca(all_candles)
print(f"Mua một lần: {(last['close'] / first['close'] - 1) * 100:+.1f}%")
print(f"100$ mỗi tuần: bỏ ra {spent:.0f}$, còn {value:.0f}$ ({(value / spent - 1) * 100:+.1f}%), "
      f"giá trung bình {average_price:.0f}$, lỗ nặng nhất {worst * 100:.1f}%")

print("\nBot DCA chạy cả năm, ba cấu hình:")
for step, take_profit, safety_orders, scale in [(0.01, 0.01, 5, 1.5), (0.02, 0.015, 6, 1.5), (0.01, 0.005, 8, 1.3)]:
    result = dca_bot(all_candles, step, take_profit, safety_orders, scale)
    total = (result["realized"] + result["unrealized"]) / result["capital"] * 100
    print(f"bước {step * 100:.0f}%, chốt {take_profit * 100}%, {safety_orders} lần mua thêm x{scale}: "
          f"vốn {result['capital']:.0f}$, {result['deals']} lệnh chốt, đã lãi {result['realized']:.0f}$, "
          f"đang lỗ {result['unrealized']:.0f}$, tổng {total:+.1f}%, "
          f"lỗ nặng nhất {result['worst'] / result['capital'] * 100:.0f}%, lệnh lâu nhất {result['longest_days']:.0f} ngày")

print("\nBot DCA bước 2%, chốt 1.5%, 6 lần mua thêm x1.5, bắt đầu ở các ngày khác nhau:\n")
print("| Ngày bắt đầu | Giá BTC | Số lệnh chốt lời | Lỗ nặng nhất trên vốn | Lệnh lâu nhất | Tổng kết quả |")
print("|---|---|---|---|---|---|")
for offset in range(0, len(all_candles) - HOURS_PER_MONTH, HOURS_PER_MONTH):
    candles = all_candles[offset:]
    result = dca_bot(candles, 0.02, 0.015, 6, 1.5)
    total = (result["realized"] + result["unrealized"]) / result["capital"] * 100
    print(f"| {candles[0]['date']} | {candles[0]['close']:,.0f}$ | {result['deals']} "
          f"| {result['worst'] / result['capital'] * 100:.0f}% | {result['longest_days']:.0f} ngày | {total:+.1f}% |")
