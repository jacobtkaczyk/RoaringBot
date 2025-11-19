import React, { useEffect, useState, useRef } from "react";
import { API_BASE } from "../config";

const AlgosDropdown = () => {
  const [algos, setAlgos] = useState([]);
  const [open, setOpen] = useState(false);
  const [selectedAlgo, setSelectedAlgo] = useState(null);
  const [duration, setDuration] = useState("");
  const ref = useRef();

  // Close dropdown on outside click
  useEffect(() => {
    const onDoc = (e) => {
      if (ref.current && !ref.current.contains(e.target)) setOpen(false);
    };
    document.addEventListener("click", onDoc);
    return () => document.removeEventListener("click", onDoc);
  }, []);

  // Load list of algorithms
  useEffect(() => {
    const fetchAlgos = async () => {
      try {
        const res = await fetch(`${API_BASE}/api/algos`);
        if (!res.ok) throw new Error("Could not fetch algorithms");
        const data = await res.json();
        setAlgos(data);
      } catch (err) {
        console.error("Algos load error", err);
        setAlgos([]);
      }
    };
    fetchAlgos();
  }, []);

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
        disabled={!selectedAlgo || !duration}
        onClick={() => {
          console.log(
            `Run algorithm: ${selectedAlgo}, duration: ${duration}`
          );
        }}
        style={{
          padding: "8px 14px",
          borderRadius: 8,
          border: "1px solid #0a0",
          background: selectedAlgo && duration ? "#0f0" : "#ccc",
          cursor: selectedAlgo && duration ? "pointer" : "not-allowed",
          fontWeight: "bold",
        }}
      >
        Run
      </button>

      {/* DURATION INPUT */}
      <input
        type="number"
        value={duration}
        placeholder="Duration"
        onChange={(e) => setDuration(e.target.value)}
        style={{
          width: 90,
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
            border: "1px solid #ddd",
            background: "#fff",
            cursor: "pointer",
            whiteSpace: "nowrap",
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
              boxShadow: "0 6px 18px rgba(0,0,0,0.12)",
              position: "absolute",
              right: 0,
            }}
          >
            {algos.length === 0 ? (
              <div style={{ padding: 12 }}>No algos found</div>
            ) : (
              <ul style={{ listStyle: "none", margin: 0, padding: 8 }}>
                {algos.map((file) => (
                  <li key={file}>
                    <button
                      onClick={() => {
                        setSelectedAlgo(file);
                        setOpen(false);
                      }}
                      style={{
                        display: "block",
                        width: "100%",
                        textAlign: "left",
                        padding: "8px 10px",
                        border: "none",
                        background: "transparent",
                        cursor: "pointer",
                      }}
                    >
                      {file}
                    </button>
                  </li>
                ))}
              </ul>
            )}
          </div>
        )}
      </div>
    </div>
  );
};

export default AlgosDropdown;