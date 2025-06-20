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

## 🧠 Features Implemented

- ✅ C# backend with Alpaca.Markets SDK
- ✅ Python strategy container (with `debugpy` support)
- ✅ PostgreSQL in Docker
- ✅ SQL schema designed in dbdiagram.io
- ✅ TablePlus-compatible DB setup
- ✅ Shared dev environment via VS Code Dev Containers
- ✅ REST API endpoint for latest Alpaca trade

---

## 🧪 Testing API

Once running, test this endpoint:

```
GET http://localhost:5075/latest-trade/AAPL
```

You should receive JSON containing the latest AAPL trade.

---

## 📦 Troubleshooting

| Problem                        | Fix                                                            |
| ------------------------------ | -------------------------------------------------------------- |
| Red squiggles in C# for Alpaca | Run `dotnet restore` locally or inside Dev Container           |
| Python debugger doesn't output | Add `flush=True` to `print()` or use `PYTHONUNBUFFERED=1`      |
| Cannot connect to API          | Make sure ports like `5075` are mapped in `docker-compose.yml` |


---

## 🤝 Contributing

- Please follow the project folder structure
- Do not commit `bin/`, `obj/`


---

## 🧾 License

TBD

