import json
import os
import subprocess
import datetime
from pathlib import Path

import pandas as pd
from dotenv import load_dotenv
from alpaca.data.historical import StockHistoricalDataClient
from alpaca.data.requests import StockBarsRequest
from alpaca.data.timeframe import TimeFrame
from backtesting import Backtest, Strategy
from backtesting.lib import crossover
from backtesting.test import SMA

load_dotenv()
API_KEY = os.getenv("ALPACA_KEY")
API_SECRET = os.getenv("ALPACA_SECRET")

if not API_KEY or not API_SECRET:
    raise RuntimeError("ALPACA_KEY / ALPACA_SECRET must be set for the backtest.")

data_client = StockHistoricalDataClient(API_KEY, API_SECRET)


def fetch_symbol_frame(symbol: str, days: int = 365) -> pd.DataFrame:
    request_params = StockBarsRequest(
        symbol_or_symbols=[symbol],
        timeframe=TimeFrame.Day,
        start=datetime.date.today() - datetime.timedelta(days=days),
        end=datetime.date.today(),
    )
    bars = data_client.get_stock_bars(request_params)
    df = bars.df
    if df is None or df.empty:
        raise RuntimeError(f"No historical data returned for {symbol}.")
    df = df.reset_index()
    df = df[df["symbol"] == symbol]
    df = df.set_index("timestamp").sort_index()
    df = df[["open", "high", "low", "close", "volume"]]
    df.columns = ["Open", "High", "Low", "Close", "Volume"]
    return df


class SmaCross(Strategy):
    n1 = 5
    n2 = 15

    def init(self):
        self.sma1 = self.I(SMA, self.data.Close, self.n1)
        self.sma2 = self.I(SMA, self.data.Close, self.n2)

    def next(self):
        if crossover(self.sma1, self.sma2):
            self.buy(size=1)
        elif crossover(self.sma2, self.sma1):
            self.sell(size=1)


def run_sma_signal_test(df: pd.DataFrame, symbol: str, short: int, long: int) -> None:
    """
    Mimic the backend interaction by piping historical bars into sma_signal_runner.py
    and printing the returned BUY/SELL/HOLD signal.
    """
    script_path = Path(__file__).resolve().parent / "sma_signal_runner.py"
    if not script_path.exists():
        raise FileNotFoundError(f"SMA signal runner not found at {script_path}")

    window = max(long, short) + 20
    tail = df.tail(window)
    bars_payload = [
        {"timestamp": idx.isoformat(), "close": float(row["Close"])}
        for idx, row in tail.iterrows()
    ]

    payload = {
        "symbol": symbol,
        "short": short,
        "long": long,
        "bars": bars_payload,
    }

    print(f"\n[Signal Test] Feeding last {len(bars_payload)} bars into {script_path.name}…")
    result = subprocess.run(
        ["python3", str(script_path)],
        input=json.dumps(payload),
        text=True,
        capture_output=True,
        check=False,
    )

    if result.stderr.strip():
        print("[Signal Test] stderr:", result.stderr.strip())
    if result.returncode != 0:
        raise RuntimeError(
            f"sma_signal_runner exited with {result.returncode}: {result.stdout.strip()}"
        )

    signal = result.stdout.strip()
    print(f"[Signal Test] Signal returned: {signal or '<<empty>>'}")


def main():
    symbol = "AAPL"
    df = fetch_symbol_frame(symbol)

    bt = Backtest(df, SmaCross, cash=100000, commission=0.002)
    stats = bt.run()
    print(stats)

    run_sma_signal_test(df, symbol, SmaCross.n1, SmaCross.n2)

    bt.plot()


if __name__ == "__main__":
    main()
