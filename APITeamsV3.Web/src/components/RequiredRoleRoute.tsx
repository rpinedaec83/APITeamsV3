import React from 'react';
import { Navigate, Outlet } from 'react-router-dom';
import { useMsal } from '@azure/msal-react';

interface RequiredRoleRouteProps {
    allowedRoles: string[];
}

const RequiredRoleRoute: React.FC<RequiredRoleRouteProps> = ({ allowedRoles }) => {
    const { accounts } = useMsal();
    const account = accounts[0];
    
    const roles = (account?.idTokenClaims?.roles as string[]) || [];
    const hasRequiredRole = allowedRoles.some(role => roles.includes(role));

    if (!hasRequiredRole) {
        return <Navigate to="/" replace />;
    }

    return <Outlet />;
};

export default RequiredRoleRoute;
