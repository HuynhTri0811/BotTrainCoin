"""Tải nến BTCUSDT từ REST công khai của Binance Spot (không cần API key) vào ../data.

- btcusdt-1h.csv: nến 1h, mở từ 2025-10-31 16:00 UTC đến 2026-11-10 14:00 UTC (8,999 nến).
- btcusdt-1m-after-drops.csv: 180 nến 1m ngay sau mỗi nến 1h đóng thấp hơn nến trước từ 500$ trở lên.

Chạy: python3 fetch_binance.py   (mất khoảng 1–2 phút)
"""
import csv
import json
import time
import urllib.request
from pathlib import Path

DATA = Path(__file__).resolve().parent.parent / "data"
BASE = "https://api.binance.com/api/v3/klines"
FIRST_OPEN_MS = 1761926400 * 1000
LAST_OPEN_MS = 1794319200 * 1000
DROP_USD = 500
MINUTES_AFTER = 180
COLUMNS = ["open_time_utc", "open", "high", "low", "close", "volume", "close_time_utc", "quote_volume",
           "trades", "taker_buy_volume", "taker_buy_quote_volume"]


def klines(interval, start_ms, end_ms=None, limit=1000):
    url = f"{BASE}?symbol=BTCUSDT&interval={interval}&startTime={start_ms}&limit={limit}"
    if end_ms is not None:
        url += f"&endTime={end_ms}"
    with urllib.request.urlopen(url, timeout=15) as response:
        return json.load(response)


def iso(ms):
    return time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime(ms // 1000))


def number(value):
    """Bỏ số 0 thừa của chuỗi giá Binance ("74399.99000000" → "74399.99") cho file nhẹ hơn."""
    text = str(value)
    return text.rstrip("0").rstrip(".") if "." in text else text


def row(kline):
    return [iso(kline[0]), *map(number, kline[1:6]), iso(kline[6] + 1), *map(number, kline[7:11])]


def minute_row(kline):
    return [iso(kline[0]), *map(number, kline[1:6])]


def fetch_hourly():
    rows = []
    start = FIRST_OPEN_MS
    while start <= LAST_OPEN_MS:
        batch = klines("1h", start, LAST_OPEN_MS)
        if not batch:
            break
        rows.extend(batch)
        start = batch[-1][0] + 3_600_000
        time.sleep(0.2)
    return rows


def main():
    DATA.mkdir(exist_ok=True)
    hourly = fetch_hourly()
    with open(DATA / "btcusdt-1h.csv", "w", newline="") as file:
        writer = csv.writer(file)
        writer.writerow(COLUMNS)
        writer.writerows(row(kline) for kline in hourly)
    print(f"btcusdt-1h.csv: {len(hourly)} nến")

    drops = [index for index in range(1, len(hourly) - 3)
             if float(hourly[index][4]) - float(hourly[index - 1][4]) <= -DROP_USD]
    with open(DATA / "btcusdt-1m-after-drops.csv", "w", newline="") as file:
        writer = csv.writer(file)
        writer.writerow(["drop_candle_open_time_utc", *COLUMNS[:6]])
        for index in drops:
            minutes = klines("1m", hourly[index + 1][0], limit=MINUTES_AFTER)
            writer.writerows([iso(hourly[index][0]), *minute_row(kline)] for kline in minutes)
            time.sleep(0.05)
    print(f"btcusdt-1m-after-drops.csv: {len(drops)} lần sập")


if __name__ == "__main__":
    main()
