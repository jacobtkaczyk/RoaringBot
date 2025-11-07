#!/usr/bin/env python3
import sys
import json
import pandas as pd

def compute_sma_signals(df, short_n, long_n):
    if df.empty or len(df) < long_n:
        return {"signal": "HOLD", "reason": "Not enough data"}

    df["SMA_SHORT"] = df["close"].rolling(short_n).mean()
    df["SMA_LONG"] = df["close"].rolling(long_n).mean()
    df = df.dropna().copy()

    prev = df.iloc[-2]
    last = df.iloc[-1]
    signal = "HOLD"
    if prev["SMA_SHORT"] <= prev["SMA_LONG"] and last["SMA_SHORT"] > last["SMA_LONG"]:
        signal = "BUY"
    elif prev["SMA_SHORT"] >= prev["SMA_LONG"] and last["SMA_SHORT"] < last["SMA_LONG"]:
        signal = "SELL"

    return {
        "signal": signal,
        "latest_close": float(last["close"]),
        "sma_short": float(last["SMA_SHORT"]),
        "sma_long": float(last["SMA_LONG"]),
        "recent": df.tail(5).to_dict(orient="records")
    }

def main():
    try:
        # Read JSON input from stdin
        raw_input = sys.stdin.read()
        data = json.loads(raw_input)

        symbol = data.get("symbol", "AAPL")
        short_n = int(data.get("short", 5))
        long_n = int(data.get("long", 15))
        bars = data.get("bars", [])

        df = pd.DataFrame(bars)
        if df.empty:
            print(json.dumps({"signal": "HOLD", "reason": "No data provided"}))
            return

        result = compute_sma_signals(df, short_n, long_n)
        result["symbol"] = symbol
        print(json.dumps(result))

    except Exception as e:
        print(json.dumps({"error": str(e)}))

if __name__ == "__main__":
    main()
