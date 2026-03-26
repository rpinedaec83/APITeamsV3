
import { BrowserRouter as Router, Routes, Route, Navigate } from 'react-router-dom';
import Dashboard from './pages/Dashboard';
import CompanyConfigsPage from './pages/CompanyConfigsPage';
import OperationsPage from './pages/OperationsPage';
import JobsPage from './pages/JobsPage';
import SchedulesPage from './pages/SchedulesPage';
import LogsPage from './pages/LogsPage';
import ReportsPage from './pages/ReportsPage';
import MainLayout from './components/layout/MainLayout';

import { MsalProvider, AuthenticatedTemplate, UnauthenticatedTemplate } from "@azure/msal-react";
import { type SpaBootstrapConfig } from "./authConfig";
import { Login } from "./components/Login";
import { CompanyProvider } from "./CompanyContext";
import RequiredRoleRoute from "./components/RequiredRoleRoute";
import type { IPublicClientApplication } from "@azure/msal-browser";

interface AppProps {
  spaConfig: SpaBootstrapConfig;
  msalInstance: IPublicClientApplication;
}

function App({ spaConfig, msalInstance }: AppProps) {

  return (
    <CompanyProvider companyKey={spaConfig.companyKey}>
      <MsalProvider instance={msalInstance}>
        <AuthenticatedTemplate>
          <Router>
            <Routes>
              <Route element={<MainLayout />}>
                <Route path="/" element={<Dashboard />} />
                <Route path="/operations" element={<OperationsPage />} />
                
                {/* Admin and IT Protected Routes */}
                <Route element={<RequiredRoleRoute allowedRoles={['ADMIN', 'IT']} />}>
                  <Route path="/admin/company-configs" element={<CompanyConfigsPage />} />
                  <Route path="/jobs" element={<JobsPage />} />
                  <Route path="/logs" element={<LogsPage />} />
                  <Route path="/schedules" element={<SchedulesPage />} />
                  <Route path="/reports" element={<ReportsPage />} />
                </Route>
              </Route>
              <Route path="/auth/callback" element={<Navigate to="/" />} />
            </Routes>
          </Router>
        </AuthenticatedTemplate>
        <UnauthenticatedTemplate>
          <Login />
        </UnauthenticatedTemplate>
      </MsalProvider>
    </CompanyProvider>
  );
}

export default App;
