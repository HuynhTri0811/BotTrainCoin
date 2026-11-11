"""Xuất ảnh chụp dữ liệu dự đoán của bot (BTCUSDT 1h) từ API đang chạy vào ../data.

- prediction-snapshot-recent.csv: các lần đoán gần nhất trong /api/prediction (history=300).
- prediction-snapshot-candles-1h.csv: 1000 nến 1h đã đóng trong /api/market/candles (nến gốc từ Binance),
  kèm khối lượng mua chủ động để tính lại thành phần lực mua/bán.

Chạy khi API đang chạy:              python3 export_snapshot.py
Hoặc từ hai file JSON đã lưu trước:  python3 export_snapshot.py prediction.json candles.json
Ảnh chụp trong repo được xuất lúc 2026-11-10 khoảng 21:55 giờ Việt Nam.
"""
import csv
import json
import sys
import time
import urllib.request
from pathlib import Path

DATA = Path(__file__).resolve().parent.parent / "data"
API = "http://localhost:5080"


def load(source):
    if source.startswith("http"):
        with urllib.request.urlopen(source, timeout=30) as response:
            return json.load(response)
    return json.loads(Path(source).read_text())


def iso(seconds):
    return time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime(seconds))


def main():
    if len(sys.argv) == 3:
        prediction, candles = load(sys.argv[1]), load(sys.argv[2])
    else:
        prediction = load(f"{API}/api/prediction?symbol=BTCUSDT&interval=1h&history=300")
        candles = load(f"{API}/api/market/candles?symbol=BTCUSDT&interval=1h&limit=1000")

    with open(DATA / "prediction-snapshot-recent.csv", "w", newline="") as file:
        writer = csv.writer(file)
        writer.writerow(["open_time_utc", "predicted", "score", "actual", "previous_close", "close", "change",
                         "change_percent"])
        for entry in reversed(prediction["recent"]):
            writer.writerow([iso(entry["time"]), entry["predicted"], entry["score"], entry["actual"] or "",
                             entry["previousClose"], entry["close"], entry["change"], entry["changePercent"]])

    closed = [candle for candle in candles if candle["isClosed"]]
    with open(DATA / "prediction-snapshot-candles-1h.csv", "w", newline="") as file:
        writer = csv.writer(file)
        writer.writerow(["open_time_utc", "open", "high", "low", "close", "volume", "buy_volume", "sell_volume"])
        for candle in closed:
            pressure = candle.get("pressure") or {}
            writer.writerow([iso(candle["time"]), candle["open"], candle["high"], candle["low"], candle["close"],
                             candle["volume"], pressure.get("buyVolume", ""), pressure.get("sellVolume", "")])
    print(f"{len(prediction['recent'])} lần đoán, {len(closed)} nến")


if __name__ == "__main__":
    main()
