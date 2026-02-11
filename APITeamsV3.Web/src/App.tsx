
import { BrowserRouter as Router, Routes, Route, Navigate } from 'react-router-dom';
import Dashboard from './pages/Dashboard';
import CompanyConfigsPage from './pages/CompanyConfigsPage';
import OperationsPage from './pages/OperationsPage';
import MainLayout from './components/layout/MainLayout';

function App() {
  return (
    <Router>
      <Routes>
        <Route element={<MainLayout />}>
          <Route path="/" element={<Dashboard />} />
          <Route path="/admin/company-configs" element={<CompanyConfigsPage />} />
          <Route path="/operations" element={<OperationsPage />} />
        </Route>
        <Route path="/auth/callback" element={<Navigate to="/" />} />
      </Routes>
    </Router>
  );
}

export default App;
