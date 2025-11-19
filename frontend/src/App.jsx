import StockGraph from './components/StockGraph.jsx';
import Logger from './components/Logger.jsx';
import AlgosDropdown from './components/AlgosDropdown.jsx'; // <-- new
import './App.css';

function App() {
  return (
    <div className="App">
      <StockGraph />
      <Logger />
      <AlgosDropdown />
    </div>
  );
}

export default App;