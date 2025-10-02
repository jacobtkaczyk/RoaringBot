import os
import sys
import math
import datetime as dt
import pandas as pd
from zoneinfo import ZoneInfo
from dotenv import load_dotenv

from alpaca.data.historical import StockHistoricalDataClient
from alpaca.data.requests import StockBarsRequest
from alpaca.data.timeframe import TimeFrame
from alpaca.trading.client import TradingClient
from alpaca.trading.requests import MarketOrderRequest, GetOrdersRequest
from alpaca.trading.enums import OrderSide, TimeInForce, QueryOrderStatus

# ---------- Config ----------
SYMBOL = os.getenv("SYMBOL", "AAPL")
SHORT = int(os.getenv("SMA_SHORT", "5"))
LONG = int(os.getenv("SMA_LONG", "15"))
QTY = float(os.getenv("ORDER_QTY", "1"))  # supports fractional if enabled
USE_PAPER = os.getenv("ALPACA_PAPER", "true").lower() == "true"
# ---------------------------

def get_clients():
    load_dotenv()
    key = os.getenv("ALPACA_KEY")
    secret = os.getenv("ALPACA_SECRET")
    if not key or not secret:
        print("Missing ALPACA_KEY/ALPACA_SECRET in environment.", file=sys.stderr)
        sys.exit(1)
    data_client = StockHistoricalDataClient(key, secret)
    trading_client = TradingClient(key, secret, paper=USE_PAPER)
    return data_client, trading_client

def market_is_open(trading_client):
    clock = trading_client.get_clock()
    return bool(clock.is_open)

def fetch_bars(data_client, symbol, start_days=90, timeframe=TimeFrame.Day):
    end = dt.date.today()
    start = end - dt.timedelta(days=start_days)
    req = StockBarsRequest(
        symbol_or_symbols=[symbol],
        timeframe=timeframe,
        start=start,
        end=end
    )
    bars = data_client.get_stock_bars(req)
    df = bars.df
    if df is None or df.empty:
        return pd.DataFrame()
    df = df.reset_index()
    df = df[df['symbol'] == symbol]
    df = df.set_index('timestamp').sort_index()
    return df[['open','high','low','close','volume']].copy()

def compute_sma_signals(df, short_n, long_n):
    if df.empty:
        return None
    df['SMA_SHORT'] = df['close'].rolling(short_n).mean()
    df['SMA_LONG']  = df['close'].rolling(long_n).mean()
    df = df.dropna().copy()
    if len(df) < 2:
        return None
    prev = df.iloc[-2]
    last = df.iloc[-1]
    signal = "HOLD"
    if prev['SMA_SHORT'] <= prev['SMA_LONG'] and last['SMA_SHORT'] > last['SMA_LONG']:
        signal = "BUY"
    elif prev['SMA_SHORT'] >= prev['SMA_LONG'] and last['SMA_SHORT'] < last['SMA_LONG']:
        signal = "SELL"
    return {
        "prev": prev,
        "last": last,
        "signal": signal,
        "df_tail": df.tail(10)
    }

def get_current_qty(trading_client, symbol):
    positions = trading_client.get_all_positions()
    for p in positions:
        if p.symbol == symbol:
            try:
                return float(p.qty)
            except Exception:
                # Alpaca may return fractional as string
                return float(p.qty_available)
    return 0.0

def has_open_orders(trading_client, symbol):
    req = GetOrdersRequest(
        status=QueryOrderStatus.OPEN,
        symbols=[symbol]
    )
    open_orders = trading_client.get_orders(filter=req)
    return len(open_orders) > 0

def submit_market(trading_client, symbol, side, qty):
    if qty <= 0:
        return None
    # round to 3 decimals for fractional shares, or int if whole-share only
    if qty.is_integer():
        qty_final = int(qty)
    else:
        qty_final = round(qty, 3)
    order = MarketOrderRequest(
        symbol=symbol,
        qty=qty_final,
        side=OrderSide.BUY if side == "BUY" else OrderSide.SELL,
        time_in_force=TimeInForce.DAY,
    )
    return trading_client.submit_order(order)

def main():
    ny = ZoneInfo("America/New_York")
    now_et = dt.datetime.now(ny)
    data_client, trading_client = get_clients()

    if not market_is_open(trading_client):
        # You can still run near close on daily bars; this just prevents intraday misfires.
        print("Market appears closed per Alpaca clock. Proceeding using latest daily bars…")

    df = fetch_bars(data_client, SYMBOL, start_days=max(60, LONG + 30))
    if df.empty:
        print("No price data returned; aborting.")
        return

    signals = compute_sma_signals(df, SHORT, LONG)
    if signals is None:
        print("Not enough data to compute SMAs; aborting.")
        return

    last_close_ts = df.index[-1]
    print(f"[{now_et:%Y-%m-%d %H:%M ET}] {SYMBOL} latest bar: {last_close_ts} UTC")
    print(signals["df_tail"][["close","SMA_SHORT","SMA_LONG"]].tail())

    signal = signals["signal"]
    current_qty = get_current_qty(trading_client, SYMBOL)
    print(f"Current position: {current_qty} shares of {SYMBOL}")
    if has_open_orders(trading_client, SYMBOL):
        print("Open orders detected; skipping to avoid double-execution.")
        return

    if signal == "BUY" and current_qty <= 0:
        print("BUY signal detected. Submitting market order…")
        resp = submit_market(trading_client, SYMBOL, "BUY", QTY)
        print(f"Order response: {resp}")
    elif signal == "SELL" and current_qty > 0:
        print("SELL signal detected. Submitting market order…")
        resp = submit_market(trading_client, SYMBOL, "SELL", current_qty)
        print(f"Order response: {resp}")
    else:
        print("HOLD (no new crossover or already positioned).")

    acct = trading_client.get_account()
    print(f"Cash: {acct.cash} | Portfolio: {acct.portfolio_value}")

if __name__ == "__main__":
    # Suggest running around 15:55–15:59 ET for daily bars (e.g., cron) so you act on nearly-complete data.
    main()
