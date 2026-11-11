"""Phần 1 của báo cáo: vì sao hàm dự đoán nến kế tiếp sai, chỉ xét nến biến động từ 80$ trở lên.

Tính lại các thành phần điểm giống PriceDirectionPredictor (TrendPeriod 20, MomentumPeriod 3, PressureWeight 0.05)
từ ảnh chụp nến, rồi so nhóm đoán sai với nhóm đoán đúng. Cuối cùng kiểm tra lại trên các nến cũ hơn ngoài 300 lần đoán.

Chạy: python3 prediction_errors.py
"""
import csv
import statistics
from datetime import datetime, timedelta
from pathlib import Path

DATA = Path(__file__).resolve().parent.parent / "data"
MIN_MOVE_USD = 80
TREND_PERIOD = 20
MOMENTUM_PERIOD = 3
PRESSURE_WEIGHT = 0.05
VIETNAM = timedelta(hours=7)


def read(name):
    with open(DATA / name) as file:
        return list(csv.DictReader(file))


candles = [dict(time=row["open_time_utc"], high=float(row["high"]), low=float(row["low"]),
                close=float(row["close"]), volume=float(row["volume"]),
                buy_percent=float(row["buy_volume"]) / float(row["volume"]) * 100
                if row["buy_volume"] and float(row["volume"]) > 0 else 50)
           for row in read("prediction-snapshot-candles-1h.csv")]
position = {candle["time"]: index for index, candle in enumerate(candles)}


def features(index):
    """Thành phần điểm khi đoán nến tại index, chỉ dùng các nến trước nó."""
    window = candles[index - TREND_PERIOD:index]
    last = window[-1]
    average = sum(candle["close"] for candle in window) / TREND_PERIOD
    trend = (last["close"] / average - 1) * 100
    momentum = (last["close"] / candles[index - MOMENTUM_PERIOD - 1]["close"] - 1) * 100
    score = trend + momentum + PRESSURE_WEIGHT * (last["buy_percent"] - 50)
    relative_volume = last["volume"] / (sum(candle["volume"] for candle in window) / TREND_PERIOD)
    previous_move = last["close"] - candles[index - 2]["close"]
    return dict(trend=trend, momentum=momentum, score=score, relative_volume=relative_volume,
                previous_move=previous_move, buy_percent=last["buy_percent"])


def hit_rate(group):
    return f"{sum(entry['correct'] for entry in group)}/{len(group)} = " \
           f"{100 * sum(entry['correct'] for entry in group) / len(group):.0f}%" if group else "-"


predictions = []
for row in read("prediction-snapshot-recent.csv"):
    index = position[row["open_time_utc"]]
    if not row["actual"]:
        continue
    entry = dict(time=row["open_time_utc"], predicted=row["predicted"], actual=row["actual"],
                 change=float(row["change"]), change_percent=float(row["change_percent"]),
                 correct=row["predicted"] == row["actual"], **features(index))
    predictions.append(entry)

big = [entry for entry in predictions if abs(entry["change"]) >= MIN_MOVE_USD]
small = [entry for entry in predictions if abs(entry["change"]) < MIN_MOVE_USD]
wrong = [entry for entry in big if not entry["correct"]]
right = [entry for entry in big if entry["correct"]]

print("== Tỷ lệ đúng")
print(f"Biến động >= {MIN_MOVE_USD}$: {hit_rate(big)}")
print(f"Biến động <  {MIN_MOVE_USD}$: {hit_rate(small)}")
print(f"Biến động lớn, |điểm| >= 2: {hit_rate([entry for entry in big if abs(entry['score']) >= 2])}")

print("\n== Trung bình nhóm sai và nhóm đúng (biến động lớn)")
for name, group in [("sai", wrong), ("đúng", right)]:
    mean = lambda key: statistics.mean(abs(entry[key]) for entry in group)
    agree = sum((entry["trend"] > 0) == (entry["momentum"] > 0) for entry in group)
    print(f"{name}: n={len(group)} |biến động| {mean('change'):.0f}$ |điểm| {mean('score'):.2f} "
          f"khối lượng nến trước {statistics.mean(entry['relative_volume'] for entry in group):.2f}x "
          f"xu hướng và đà cùng chiều {agree}/{len(group)}")

print("\n== Tỷ lệ đúng theo khối lượng nến trước (biến động lớn)")
for low, high in [(0, 1), (1, 1.5), (1.5, 2.5), (2.5, 99)]:
    group = [entry for entry in big if low <= entry["relative_volume"] < high]
    print(f"{low}x đến {high}x: {hit_rate(group)}")

print("\n== Các lần sai lớn nhất (giờ Việt Nam)")
for entry in sorted(wrong, key=lambda entry: -abs(entry["change"]))[:8]:
    local = datetime.strptime(entry["time"], "%Y-%m-%dT%H:%M:%SZ") + VIETNAM
    print(f"{local:%H:%M %d/%m} đoán {entry['predicted']:<4} thực tế {entry['change']:+.0f}$ "
          f"điểm {entry['score']:+.2f} xu hướng {entry['trend']:+.2f}% đà {entry['momentum']:+.2f}% "
          f"nến trước {entry['previous_move']:+.0f}$ khối lượng {entry['relative_volume']:.1f}x")

print("\n== Kiểm tra lại trên các nến cũ hơn (ngoài 300 lần đoán)")
first_prediction = position[read("prediction-snapshot-recent.csv")[0]["open_time_utc"]]
for name, indexes in [("nến cũ hơn", range(TREND_PERIOD + 1, first_prediction)),
                      ("300 nến gần nhất", range(first_prediction, len(candles)))]:
    groups = {"tất cả": [], "khối lượng >= 1.5x": [], "|điểm| >= 2": []}
    for index in indexes:
        move = candles[index]["close"] - candles[index - 1]["close"]
        if move == 0 or abs(move) < MIN_MOVE_USD:
            continue
        values = features(index)
        entry = dict(correct=(values["score"] >= 0) == (move > 0))
        groups["tất cả"].append(entry)
        if values["relative_volume"] >= 1.5:
            groups["khối lượng >= 1.5x"].append(entry)
        if abs(values["score"]) >= 2:
            groups["|điểm| >= 2"].append(entry)
    print(name, {key: hit_rate(group) for key, group in groups.items()})
