
import { BrowserRouter as Router, Routes, Route, Navigate } from 'react-router-dom';
import Dashboard from './pages/Dashboard';
import CompanyConfigsPage from './pages/CompanyConfigsPage';
import OperationsPage from './pages/OperationsPage';
import JobsPage from './pages/JobsPage';
import MainLayout from './components/layout/MainLayout';

import { PublicClientApplication } from "@azure/msal-browser";
import { MsalProvider, AuthenticatedTemplate, UnauthenticatedTemplate } from "@azure/msal-react";
import { createMsalConfig } from "./authConfig";
import { useMemo } from "react";
import { Login } from "./components/Login";

interface AppProps {
  spaConfig: {
    spaClientId: string;
    tenantId: string;
    companyKey: string;
  };
}

function App({ spaConfig }: AppProps) {
  const msalInstance = useMemo(() => {
    const config = createMsalConfig(spaConfig.spaClientId, spaConfig.tenantId, window.location.origin);
    return new PublicClientApplication(config);
  }, [spaConfig]);

  return (
    <MsalProvider instance={msalInstance}>
      <AuthenticatedTemplate>
        <Router>
          <Routes>
            <Route element={<MainLayout />}>
              <Route path="/" element={<Dashboard />} />
              <Route path="/admin/company-configs" element={<CompanyConfigsPage />} />
              <Route path="/operations" element={<OperationsPage />} />
              <Route path="/jobs" element={<JobsPage />} />
            </Route>
            <Route path="/auth/callback" element={<Navigate to="/" />} />
          </Routes>
        </Router>
      </AuthenticatedTemplate>
      <UnauthenticatedTemplate>
        <Login />
      </UnauthenticatedTemplate>
    </MsalProvider>
  );
}

export default App;
