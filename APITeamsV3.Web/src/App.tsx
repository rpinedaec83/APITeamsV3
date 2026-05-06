
import { BrowserRouter as Router, Routes, Route, Navigate } from 'react-router-dom';
import React, { useEffect, useRef } from 'react';
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
import MaintenancePage from './pages/MaintenancePage';
import { MAINTENANCE_REQUIRED_EVENT, getMaintenanceMessage } from './utils/maintenance';

interface AppProps {
  spaConfig: SpaBootstrapConfig;
  msalInstance: IPublicClientApplication;
}

function AuthenticatedApp() {
  const { instance, accounts, inProgress } = useMsal();
  const account = accounts[0];
  const roles = React.useMemo(() => {
    const claims = account?.idTokenClaims as any;
    if (!claims) return [];
    
    // Look for roles in various possible claims: 'roles', 'role', 'groups'
    const rawRoles = (claims.roles || claims.role || claims.groups || []) as string | string[];
    const rolesArray = Array.isArray(rawRoles) ? rawRoles : [rawRoles];
    const normalizedRoles = rolesArray.map(r => r.toString().toUpperCase().trim());
    
    console.log("[Auth-Debug] Roles detectados en el token:", normalizedRoles);
    return normalizedRoles;
  }, [account]);
  const loginRedirectPendingRef = useRef(false);
  const [maintenanceMessage, setMaintenanceMessage] = React.useState<string | null>(null);

  useEffect(() => {
    if (inProgress === InteractionStatus.None) {
      loginRedirectPendingRef.current = false;
    }
  }, [inProgress]);

  useEffect(() => {
    const handleInteractiveAuthRequired = () => {
      if (window.self !== window.top) {
        console.warn("Auth interaction required event ignored: not in top-level window.");
        return;
      }

      if (inProgress !== InteractionStatus.None || loginRedirectPendingRef.current) {
        console.log(`Auth interaction skipped: status=${inProgress}, pending=${loginRedirectPendingRef.current}`);
        return;
      }

      console.warn(`Starting interactive authentication for account: ${account?.username || 'unknown'}`);
      loginRedirectPendingRef.current = true;
      
      const request = {
        ...getLoginRequest(),
        account: account || undefined
      };

      instance.acquireTokenRedirect(request).catch((error) => {
        loginRedirectPendingRef.current = false;
        if (error instanceof BrowserAuthError && error.errorCode === "interaction_in_progress") {
          console.warn("Login skipped: interaction already in progress.");
          return;
        }

        console.error("Interactive token acquisition failed:", error);
      });
    };

    window.addEventListener(AUTH_INTERACTION_REQUIRED_EVENT, handleInteractiveAuthRequired);
    return () => {
      window.removeEventListener(AUTH_INTERACTION_REQUIRED_EVENT, handleInteractiveAuthRequired);
    };
  }, [instance, inProgress]);

  useEffect(() => {
    const handleMaintenanceRequired = (event: Event) => {
      const customEvent = event as CustomEvent<{ message?: string }>;
      setMaintenanceMessage(getMaintenanceMessage(customEvent.detail?.message));
    };

    window.addEventListener(MAINTENANCE_REQUIRED_EVENT, handleMaintenanceRequired);
    return () => {
      window.removeEventListener(MAINTENANCE_REQUIRED_EVENT, handleMaintenanceRequired);
    };
  }, []);

  const AUTHORIZED_ROLES = ['ADMIN', 'IT', 'GESTION', 'ALL', 'GESTOR'];
  const hasAuthorizedRole = roles.length > 0 && AUTHORIZED_ROLES.some(authRole => 
    roles.some(userRole => userRole.includes(authRole))
  );

  // If user has no authorized roles, show the restricted access page (no sidebar)
  if (!hasAuthorizedRole) {
    return (
      <Router>
        <Routes>
          <Route path="*" element={<NoAccessPage />} />
        </Routes>
      </Router>
    );
  }

  if (maintenanceMessage) {
    return <MaintenancePage message={maintenanceMessage} />;
  }

  return (
    <Router>
      <Routes>
        <Route element={<MainLayout />}>
          {/* Main Dashboard - Protected by any of the authorized roles */}
          <Route element={<RequiredRoleRoute allowedRoles={AUTHORIZED_ROLES} />}>
            <Route path="/" element={<Dashboard />} />
            <Route path="/operations" element={<OperationsPage />} />
          </Route>

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
