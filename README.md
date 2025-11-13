# RoaringBot

A collaborative, full-stack trading bot project using C# (.NET), Python, Docker, PostgreSQL, and the Alpaca API. This project is designed to run entirely inside Docker containers for consistent environments across all developers.

---

## 🚀 Project Overview

- **Backend**: C# (.NET 9) Web API
- **Algorithms**: Python (strategies, signals)
- **Broker**: [Alpaca Markets](https://alpaca.markets/)
- **Database**: PostgreSQL (inside Docker)
- **Architecture**: Multi-container Docker project

---

## 🧱 Project Structure

```
RoaringBot/
├── backend-csharp/           # C# Alpaca-connected backend
│   └── RoaringBot/
│       ├── Program.cs
│       ├── Dockerfile
│       └── RoaringBot.csproj
├── algos-python/            # Python-based trading algorithms
│   ├── main.py
│   ├── requirements.txt
│   └── Dockerfile
├── docker-compose.yml       # Orchestrates services
└── .env                     # Environment variables 
```

---

## 🐳 Getting Started

### 1. Clone the Repo

```bash
git clone https://github.com/your-username/RoaringBot.git
cd RoaringBot
```

### 2. Start All Services

```bash
docker-compose up --build
```

This spins up:

- PostgreSQL database
- C# backend (with Alpaca connection)
- Python strategy container

### 3. Makefile

Check out the Makefile for quick ways to run the docker containers


## 🔄 SMA Signal Flow

1. The backend fetches three months of daily bars from Alpaca's data API.
2. It sends the bars, symbol, and window lengths to `algos-python/sma_signal_runner.py`, which prints `BUY`, `SELL`, or `HOLD`.
3. The `/trade/execute` endpoint reads that response; `BUY`/`SELL` triggers a market order through Alpaca's trading API while `HOLD` exits without submitting anything. Hit `/run-algo` if you only need the latest signal without sending an order.

POST `http://localhost:5075/trade/execute`

```json
{
  "symbol": "AAPL",
  "short": 5,
  "long": 15,
  "quantity": 1
}
```

If a trade is placed the API responds with the Alpaca order id and side; otherwise it reports that no action was taken.

### Local SMA Regression Test

To sanity-check the Python signal runner with real Alpaca data, run:

```bash
python3 algos-python/backtest_sma.py
```

The script downloads one year of AAPL bars, performs the SMA crossover backtest, and then pipes the latest window into `sma_signal_runner.py`, printing whatever signal the standalone script returns—mirroring the backend integration path. Requires valid `ALPACA_KEY`/`ALPACA_SECRET` in your environment.

---

## 🔍 End-to-End Testing & Result Guide

### 1. Pure Python Signal Check
- **Command:**  
  ```bash
  python3 algos-python/sma_signal_runner.py <<'JSON'
  {"symbol":"AAPL","short":5,"long":15,"bars":[{"close":100}, ... ]}
  JSON
  ```  
- **Expect:** A single word (`BUY`, `SELL`, `HOLD`). Use synthetic bars to force crossovers; errors go to stderr.

### 2. Realistic Regression Harness
- **Command:**  
  ```bash
  python3 algos-python/backtest_sma.py
  ```  
- **What it does:** Pulls 1 year of Alpaca data, runs the SMA backtest, then feeds the latest window into `sma_signal_runner.py` and prints the returned signal. Review:
  - Backtest stats → overall edge (negative Sharpe = strategy underperforming).
  - `[Signal Test] Signal returned: ...` → what the backend would see today.

### 3. Backend Signal Endpoint (no trading)
- **Command:**  
  ```bash
  curl -X POST http://localhost:5075/run-algo \
       -H "Content-Type: application/json" \
       -d '{"symbol":"AAPL","short":5,"long":15}'
  ```  
- **Response fields:**  
  - `signal` – BUY/SELL/HOLD from the Python runner.  
  - `bars_used` – how many historical points fed into the script (sanity check data availability).  
  - The echoed `short`/`long` confirm the requested windows.

### 4. Full Trade Simulation (paper account)
- **Command:**  
  ```bash
  curl -X POST http://localhost:5075/trade/execute \
       -H "Content-Type: application/json" \
       -d '{"symbol":"AAPL","short":5,"long":15,"quantity":1}'
  ```  
- **Result decoding:**  
  - `{"signal":"HOLD","message":"No action taken."}` → backend obeyed HOLD; no order placed.  
  - `{"signal":"BUY","orderId":"...","side":"Buy"}` (or SELL) → Alpaca accepted a market order; track `orderId` in dashboard or logs.

### 5. Supporting Reads
- `curl http://localhost:5075/stock/AAPL/history` – inspect the same bars being piped into Python.  
- `curl http://localhost:5075/stock/AAPL/price` – quick quote to confirm connectivity.

### Log Watching
Run `docker compose logs -f csharp-backend` in another terminal. You should see:
- `[run-algo] Python signal for AAPL: HOLD` when hitting `/run-algo`.
- `[trade/execute] Signal for AAPL: BUY` plus Alpaca order output when trading.

### Troubleshooting Signals
- **No output / script path errors:** rebuild containers (`docker compose up --build`) to ensure the backend uses `sma_signal_runner.py`.
- **Persistent HOLD:** inspect the backtest stats; if SMP crossover hasn’t triggered recently, HOLD is expected.
- **Unexpected SELL/BUY:** re-run test 1 with the same bar set to replicate the signal locally; confirm the crossover math.
