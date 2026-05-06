import React from 'react';
import { Navigate, Outlet } from 'react-router-dom';
import { useMsal } from '@azure/msal-react';

interface RequiredRoleRouteProps {
    allowedRoles: string[];
}

const RequiredRoleRoute: React.FC<RequiredRoleRouteProps> = ({ allowedRoles }) => {
    const { accounts } = useMsal();
    const account = accounts[0];
    
    const roles = React.useMemo(() => {
        const claims = account?.idTokenClaims as any;
        if (!claims) return [];
        
        // Look for roles in various possible claims: 'roles', 'role', 'groups'
        const rawRoles = (claims.roles || claims.role || claims.groups || []) as string | string[];
        const rolesArray = Array.isArray(rawRoles) ? rawRoles : [rawRoles];
        
        return rolesArray.map(r => r.toString().toUpperCase().trim());
    }, [account]);

    const hasRequiredRole = allowedRoles.some(authRole => 
        roles.some(userRole => userRole.includes(authRole.toUpperCase()))
    );

    if (!hasRequiredRole) {
        return <Navigate to="/" replace />;
    }

    return <Outlet />;
};

export default RequiredRoleRoute;
