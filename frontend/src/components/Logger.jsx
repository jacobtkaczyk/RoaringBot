// frontend/src/components/Logger.jsx
import React, { useEffect, useState } from "react";

const Logger = ({ endpoint = "http://localhost:5075/logs/stream" }) => {
  const [logs, setLogs] = useState([]);

  useEffect(() => {
    const eventSource = new EventSource(endpoint);

    eventSource.onmessage = (event) => {
      setLogs((prevLogs) => [...prevLogs, event.data]);
    };

    eventSource.onerror = (err) => {
      console.error("Logger connection error:", err);
      eventSource.close();
    };

    return () => eventSource.close();
  }, [endpoint]);

  return (
    <div
      style={{
        position: "fixed",
        bottom: "20px",
        left: "20px",
        width: "400px",
        height: "180px",
        backgroundColor: "#1a1a1a",
        color: "#00ff00",
        padding: "10px",
        borderRadius: "8px",
        fontFamily: "monospace",
        fontSize: "0.85rem",
        overflowY: "auto",
        boxShadow: "0 0 10px rgba(0,0,0,0.4)",
      }}
    >
      <strong>Container Logs:</strong>
      <div style={{ marginTop: "5px", whiteSpace: "pre-wrap" }}>
        {logs.length > 0
          ? logs.map((line, idx) => <div key={idx}>{line}</div>)
          : "Waiting for logs..."}
      </div>
    </div>
  );
};

export default Logger;