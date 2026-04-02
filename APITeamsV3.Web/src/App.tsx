
import { BrowserRouter as Router, Routes, Route, Navigate } from 'react-router-dom';
import Dashboard from './pages/Dashboard';
import CompanyConfigsPage from './pages/CompanyConfigsPage';
import OperationsPage from './pages/OperationsPage';
import JobsPage from './pages/JobsPage';
import SchedulesPage from './pages/SchedulesPage';
import LogsPage from './pages/LogsPage';
import ReportsPage from './pages/ReportsPage';
import MainLayout from './components/layout/MainLayout';

import { MsalProvider, AuthenticatedTemplate, UnauthenticatedTemplate, useMsal } from "@azure/msal-react";
import { type SpaBootstrapConfig } from "./authConfig";
import { Login } from "./components/Login";
import { CompanyProvider } from "./CompanyContext";
import RequiredRoleRoute from "./components/RequiredRoleRoute";
import type { IPublicClientApplication } from "@azure/msal-browser";
import NoAccessPage from './pages/NoAccessPage';

interface AppProps {
  spaConfig: SpaBootstrapConfig;
  msalInstance: IPublicClientApplication;
}

function AuthenticatedApp() {
  const { accounts } = useMsal();
  const account = accounts[0];
  const roles = (account?.idTokenClaims?.roles as string[]) || [];

  // If user has no roles, show the restricted access page (no sidebar)
  if (roles.length === 0) {
    return (
      <Router>
        <Routes>
          <Route path="*" element={<NoAccessPage />} />
        </Routes>
      </Router>
    );
  }

  return (
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
  );
}

function App({ spaConfig, msalInstance }: AppProps) {

  return (
    <CompanyProvider companyKey={spaConfig.companyKey}>
      <MsalProvider instance={msalInstance}>
        <AuthenticatedTemplate>
          <AuthenticatedApp />
        </AuthenticatedTemplate>
        <UnauthenticatedTemplate>
          <Login />
        </UnauthenticatedTemplate>
      </MsalProvider>
    </CompanyProvider>
  );
}

export default App;
