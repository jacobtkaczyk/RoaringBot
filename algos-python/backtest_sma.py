import os
import datetime
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

# Alpaca client for historical data
data_client = StockHistoricalDataClient(API_KEY, API_SECRET)

# === Fetch 1 year of historical AAPL data ===
request_params = StockBarsRequest(
    symbol_or_symbols=["AAPL"],
    timeframe=TimeFrame.Day,
    start=datetime.date.today() - datetime.timedelta(days=365),
    end=datetime.date.today()
)
bars = data_client.get_stock_bars(request_params)

# === Clean DataFrame ===
df = bars.df
df = df.reset_index()              # remove MultiIndex
df = df[df['symbol'] == 'AAPL']    # keep only AAPL
df = df.set_index('timestamp')     # datetime index
df = df[['open', 'high', 'low', 'close', 'volume']]
df.columns = ['Open', 'High', 'Low', 'Close', 'Volume']

# === Strategy: SMA crossover (5 vs 15) ===
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

# === Run Backtest ===
bt = Backtest(df, SmaCross, cash=100000, commission=.002)
stats = bt.run()
print(stats)

# Plot results
bt.plot()
