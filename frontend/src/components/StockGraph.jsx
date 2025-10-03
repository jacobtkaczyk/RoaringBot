import { useState, useEffect } from 'react';
import { LineChart, Line, XAxis, YAxis, CartesianGrid, Tooltip, ResponsiveContainer } from 'recharts';

const StockGraph = () => {
    const [symbol, setSymbol] = useState('AAPL');
    const [inputSymbol, setInputSymbol] = useState('AAPL');
    const [currentPrice, setCurrentPrice] = useState(null);
    const [history, setHistory] = useState([]);
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState(null);

    useEffect(() => {
        const fetchData = async () => {
            if (!symbol) return;
            setLoading(true);
            setError(null);

            try {
                const apiUrl = import.meta.env.VITE_API_URL;

                // --- Fetch history ---
                const historyRes = await fetch(`${apiUrl}/stock/${symbol}/history`);
                if (!historyRes.ok) throw new Error(`Could not fetch history for ${symbol}`);
                const historyData = await historyRes.json();

                // Normalize data
                const normalizedHistory = historyData.map(d => ({
                    date: String(d.date),
                    price: Number(d.price),
                }));

                console.log("Normalized history data:", normalizedHistory);
                setHistory(normalizedHistory);

                // --- Fetch current price ---
                const priceRes = await fetch(`${apiUrl}/stock/${symbol}/price`);
                if (!priceRes.ok) throw new Error(`Could not fetch price for ${symbol}`);
                const priceData = await priceRes.json();

                setCurrentPrice(Number(priceData.price));
            } catch (err) {
                setError(err.message);
                setHistory([]);
                setCurrentPrice(null);
            } finally {
                setLoading(false);
            }
        };

        fetchData();
    }, [symbol]);

    const handleFetch = (e) => {
        e.preventDefault();
        setSymbol(inputSymbol.toUpperCase());
    };

    return (
        <div className="max-w-4xl mx-auto my-8 p-6 sm:p-8 bg-slate-50 rounded-lg shadow-lg">
            <header className="flex flex-wrap justify-between items-center mb-6">
                <h1 className="text-3xl font-bold text-gray-800">Stock Viewer</h1>
                <form onSubmit={handleFetch} className="flex gap-2 mt-4 sm:mt-0">
                    <input
                        type="text"
                        value={inputSymbol}
                        onChange={(e) => setInputSymbol(e.target.value)}
                        placeholder="e.g., NVDA"
                        className="py-2 px-3 border border-gray-300 rounded-md focus:outline-none focus:ring-2 focus:ring-blue-500"
                    />
                    <button
                        type="submit"
                        className="py-2 px-4 bg-blue-600 text-white font-semibold rounded-md hover:bg-blue-700 transition-colors"
                    >
                        Get Chart
                    </button>
                </form>
            </header>

            {loading && <p className="text-center text-gray-600">Loading data...</p>}
            {error && <p className="text-center font-bold text-red-600">{error}</p>}

            {!loading && !error && currentPrice && (
                <div className="mb-4">
                    <h2 className="text-2xl font-semibold text-gray-700 inline-block mr-4">{symbol}</h2>
                    <p className="text-3xl font-bold text-green-600 inline-block">
                        ${currentPrice.toFixed(2)}
                    </p>
                </div>
            )}

            {/* Chart */}
            <div style={{ width: '100%', height: 400 }}>
                {history.length > 0 ? (
                    <ResponsiveContainer width="100%" height="100%">
                        <LineChart data={history} margin={{ top: 20, right: 30, left: 20, bottom: 20 }}>
                            <CartesianGrid strokeDasharray="3 3" />
                            <XAxis dataKey="date" />
                            <YAxis domain={['auto', 'auto']} />
                            <Tooltip />
                            <Line
                                type="monotone"
                                dataKey="price"
                                stroke="#2563eb"
                                strokeWidth={2}
                                dot={false}
                                isAnimationActive={true}
                            />
                        </LineChart>
                    </ResponsiveContainer>
                ) : (
                    <p className="text-center text-gray-600">No history data available</p>
                )}
            </div>
        </div>
    );
};

export default StockGraph;

