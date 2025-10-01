import { useState } from "react";

function App() {
  const [trade, setTrade] = useState(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState(null);

  const fetchTrade = async (symbol) => {
    setLoading(true);
    setError(null);

    try {
      // Use your .env backend URL
      const response = await fetch(
        `${import.meta.env.VITE_API_URL}/latest-trade/${symbol}`
      );

      if (!response.ok) {
        throw new Error(`Backend error: ${response.statusText}`);
      }

      const data = await response.json();
      setTrade(data);
    } catch (err) {
      setError(err.message);
      setTrade(null);
    } finally {
      setLoading(false);
    }
  };

  return (
    <div style={{ padding: "2rem", fontFamily: "sans-serif" }}>
      <h1>Stock Trade Viewer</h1>
      <button onClick={() => fetchTrade("AAPL")}>Get AAPL Latest Trade</button>

      {loading && <p>Loading...</p>}
      {error && <p style={{ color: "red" }}>Error: {error}</p>}

      {trade && (
        <div style={{ marginTop: "1rem" }}>
          <h2>{trade.symbol}</h2>
          <p>Price: ${trade.price}</p>
          <p>Timestamp: {new Date(trade.timestamp).toLocaleString()}</p>
        </div>
      )}
    </div>
  );
}

export default App;