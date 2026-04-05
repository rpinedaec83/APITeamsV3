
import { BrowserRouter as Router, Routes, Route, Navigate } from 'react-router-dom';
import { useEffect, useRef } from 'react';
import Dashboard from './pages/Dashboard';
import CompanyConfigsPage from './pages/CompanyConfigsPage';
import OperationsPage from './pages/OperationsPage';
import JobsPage from './pages/JobsPage';
import SchedulesPage from './pages/SchedulesPage';
import ScheduleExecutionsPage from './pages/ScheduleExecutionsPage';
import LogsPage from './pages/LogsPage';
import ReportsPage from './pages/ReportsPage';
import MainLayout from './components/layout/MainLayout';

import { MsalProvider, AuthenticatedTemplate, UnauthenticatedTemplate, useMsal } from "@azure/msal-react";
import { InteractionStatus, BrowserAuthError } from "@azure/msal-browser";
import { getLoginRequest, type SpaBootstrapConfig } from "./authConfig";
import { Login } from "./components/Login";
import { CompanyProvider } from "./CompanyContext";
import RequiredRoleRoute from "./components/RequiredRoleRoute";
import type { IPublicClientApplication } from "@azure/msal-browser";
import NoAccessPage from './pages/NoAccessPage';
import { AUTH_INTERACTION_REQUIRED_EVENT } from './hooks/useApiClient';

interface AppProps {
  spaConfig: SpaBootstrapConfig;
  msalInstance: IPublicClientApplication;
}

function AuthenticatedApp() {
  const { instance, accounts, inProgress } = useMsal();
  const account = accounts[0];
  const roles = (account?.idTokenClaims?.roles as string[]) || [];
  const loginRedirectPendingRef = useRef(false);

  useEffect(() => {
    if (inProgress === InteractionStatus.None) {
      loginRedirectPendingRef.current = false;
    }
  }, [inProgress]);

  useEffect(() => {
    const handleInteractiveAuthRequired = () => {
      if (window.self !== window.top) {
        return;
      }

      if (inProgress !== InteractionStatus.None || loginRedirectPendingRef.current) {
        return;
      }

      loginRedirectPendingRef.current = true;
      instance.loginRedirect(getLoginRequest()).catch((error) => {
        loginRedirectPendingRef.current = false;
        if (error instanceof BrowserAuthError && error.errorCode === "interaction_in_progress") {
          console.warn("Login skipped: interaction already in progress.");
          return;
        }

        console.error("Login redirect failed:", error);
      });
    };

    window.addEventListener(AUTH_INTERACTION_REQUIRED_EVENT, handleInteractiveAuthRequired);
    return () => {
      window.removeEventListener(AUTH_INTERACTION_REQUIRED_EVENT, handleInteractiveAuthRequired);
    };
  }, [instance, inProgress]);

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
            <Route path="/schedules" element={<SchedulesPage />} />
            <Route path="/schedules/:id/executions" element={<ScheduleExecutionsPage />} />
          </Route>

          {/* Admin, IT and Gestion Protected Routes */}
          <Route element={<RequiredRoleRoute allowedRoles={['ADMIN', 'IT', 'GESTION']} />}>
            <Route path="/logs" element={<LogsPage />} />
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
