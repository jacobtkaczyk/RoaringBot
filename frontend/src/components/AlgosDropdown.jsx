import React, { useEffect, useState, useRef } from "react";
import { API_BASE } from "../config";

const AlgosDropdown = () => {
  const [algos, setAlgos] = useState([]);
  const [open, setOpen] = useState(false);
  const [selectedAlgo, setSelectedAlgo] = useState(null);

  const [duration, setDuration] = useState("");         // minutes
  const [intervalSec, setIntervalSec] = useState("");   // seconds
  const [ticker, setTicker] = useState("");

  const [running, setRunning] = useState(false);
  const ref = useRef();

  // close dropdown
  useEffect(() => {
    const onDoc = (e) => {
      if (ref.current && !ref.current.contains(e.target)) setOpen(false);
    };
    document.addEventListener("click", onDoc);
    return () => document.removeEventListener("click", onDoc);
  }, []);

  // load algos
  useEffect(() => {
    const fetchAlgos = async () => {
      try {
        const res = await fetch(`${API_BASE}/api/algos`);
        const data = await res.json();
        setAlgos(data);
      } catch {
        setAlgos([]);
      }
    };
    fetchAlgos();
  }, []);

  const handleRun = async () => {
    if (!selectedAlgo || !duration || !intervalSec || !ticker) return;

    setRunning(true);
    try {
      const payload = {
        Algo: selectedAlgo,
        Symbol: ticker.toUpperCase(),
        DurationMinutes: Number(duration),
        IntervalSeconds: Number(intervalSec),
        Short: 5,
        Long: 20
      };

      const res = await fetch(`${API_BASE}/run-selected-algo/start`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(payload)
      });

      const json = await res.json();
      if (!res.ok) alert(json.detail || "Error starting algo");
      else alert("Algo started!");
    } catch (e) {
      alert("Run error");
    }
  };

  const handleStop = async () => {
    try {
      const res = await fetch(`${API_BASE}/run-selected-algo/stop`, {
        method: "POST"
      });

      const json = await res.json();
      if (!res.ok) alert(json.detail || "Failed to stop");
      else alert("Algo stopped.");
    } catch (e) {
      alert("Stop error");
    }
    setRunning(false);
  };

  return (
    <div
      style={{
        position: "fixed",
        top: 16,
        right: 16,
        zIndex: 9999,
        display: "flex",
        gap: 10,
        alignItems: "center",
      }}
    >
      {/* RUN BUTTON */}
      <button
        disabled={!selectedAlgo || !duration || !ticker || !intervalSec || running}
        onClick={handleRun}
        style={{
          padding: "8px 14px",
          borderRadius: 8,
          border: "1px solid #0a0",
          background: !running ? "#0f0" : "#ccc",
          cursor: !running ? "pointer" : "not-allowed",
          fontWeight: "bold",
        }}
      >
        {running ? "Running…" : "Run"}
      </button>

      {/* STOP BUTTON */}
      <button
        disabled={!running}
        onClick={handleStop}
        style={{
          padding: "8px 14px",
          borderRadius: 8,
          border: "1px solid red",
          background: running ? "#f55" : "#ccc",
          cursor: running ? "pointer" : "not-allowed",
          fontWeight: "bold",
        }}
      >
        Stop
      </button>

      {/* TICKER */}
      <input
        type="text"
        value={ticker}
        placeholder="Ticker"
        onChange={(e) => setTicker(e.target.value.toUpperCase())}
        style={{
          width: 110,
          padding: "8px",
          borderRadius: 8,
          border: "1px solid #ddd",
          textTransform: "uppercase",
        }}
      />

      {/* DURATION MINUTES */}
      <input
        type="number"
        value={duration}
        placeholder="Minutes"
        onChange={(e) => setDuration(e.target.value)}
        style={{
          width: 90,
          padding: "8px",
          borderRadius: 8,
          border: "1px solid #ddd",
        }}
      />

      {/* INTERVAL SECONDS */}
      <input
        type="number"
        value={intervalSec}
        placeholder="Interval sec"
        onChange={(e) => setIntervalSec(e.target.value)}
        style={{
          width: 120,
          padding: "8px",
          borderRadius: 8,
          border: "1px solid #ddd",
        }}
      />

      {/* DROPDOWN */}
      <div ref={ref} style={{ position: "relative" }}>
        <button
          onClick={() => setOpen((s) => !s)}
          style={{
            padding: "8px 12px",
            borderRadius: 8,
            background: "#222",
            color: "#fff",
            cursor: "pointer",
            fontWeight: "500",
          }}
        >
          {selectedAlgo ? `Algo: ${selectedAlgo}` : "Algorithms ▾"}
        </button>

        {open && (
          <div
            style={{
              marginTop: 8,
              minWidth: 220,
              maxHeight: 260,
              overflowY: "auto",
              background: "#fff",
              border: "1px solid #eee",
              borderRadius: 8,
              position: "absolute",
              right: 0,
            }}
          >
            <ul style={{ listStyle: "none", padding: 8 }}>
              {algos.map((file) => (
                <li key={file}>
                  <button
                    onClick={() => {
                      setSelectedAlgo(file);
                      setOpen(false);
                    }}
                    style={{
                      width: "100%",
                      padding: "8px 10px",
                      border: "none",
                      background: "#222",
                      color: "#fff",
                      cursor: "pointer",
                      borderRadius: 6,
                      marginBottom: 6
                    }}
                  >
                    {file}
                  </button>
                </li>
              ))}
            </ul>
          </div>
        )}
      </div>
    </div>
  );
};

export default AlgosDropdown;
