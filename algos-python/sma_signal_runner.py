#!/usr/bin/env python3
"""
Standalone SMA crossover signal emitter.
Reads JSON on stdin with keys:
    symbol: ticker string (optional, default 'AAPL')
    short: short SMA window (int)
    long: long SMA window (int)
    bars: list of dicts containing at least a 'close' price
Prints one of BUY, SELL, or HOLD to stdout.
"""

import json
import sys

import pandas as pd


def compute_signal(df: pd.DataFrame, short_n: int, long_n: int) -> str:
    """Return BUY/SELL/HOLD based on SMA crossover."""
    if df.empty or len(df) < max(short_n, long_n):
        return "HOLD"

    df = df.copy()
    df["SMA_SHORT"] = df["close"].rolling(short_n).mean()
    df["SMA_LONG"] = df["close"].rolling(long_n).mean()
    df = df.dropna()
    if len(df) < 2:
        return "HOLD"

    prev = df.iloc[-2]
    last = df.iloc[-1]
    if prev["SMA_SHORT"] <= prev["SMA_LONG"] and last["SMA_SHORT"] > last["SMA_LONG"]:
        return "BUY"
    if prev["SMA_SHORT"] >= prev["SMA_LONG"] and last["SMA_SHORT"] < last["SMA_LONG"]:
        return "SELL"
    return "HOLD"


def main() -> None:
    try:
        raw = sys.stdin.read()
        payload = json.loads(raw)
        short_n = int(payload.get("short", 5))
        long_n = int(payload.get("long", 15))
        bars = payload.get("bars", [])
        df = pd.DataFrame(bars)
        if "close" not in df.columns:
            print("HOLD")
            return
        signal = compute_signal(df, short_n, long_n)
        print(signal)
    except Exception as exc:  # safeguard so caller always sees a signal
        print("HOLD")
        print(f"error: {exc}", file=sys.stderr)


if __name__ == "__main__":
    main()
