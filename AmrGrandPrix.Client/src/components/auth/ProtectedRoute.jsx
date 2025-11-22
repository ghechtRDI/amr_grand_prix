/**
 * ProtectedRoute Component
 * HOC for route protection with optional role checking
 */

import { Navigate } from 'react-router-dom';
import { useAuth } from '../../hooks/useAuth';

/**
 * ProtectedRoute - Requires authentication
 * @param {object} props
 * @param {React.ReactNode} props.children - Child components to render if authenticated
 * @param {string|string[]} props.roles - Optional role(s) required to access the route
 * @param {string} props.redirectTo - Path to redirect if not authenticated (default: /login)
 */
export const ProtectedRoute = ({ children, roles, redirectTo = '/login' }) => {
  const { isAuthenticated, hasRole, hasAnyRole, loading } = useAuth();

  // Show loading state while checking authentication
  if (loading) {
    return (
      <div className="auth-loading">
        <p>Loading...</p>
      </div>
    );
  }

  // Check if user is authenticated
  if (!isAuthenticated()) {
    return <Navigate to={redirectTo} replace />;
  }

  // If roles are specified, check if user has required role(s)
  if (roles) {
    const requiredRoles = Array.isArray(roles) ? roles : [roles];
    const hasAccess = requiredRoles.length === 1
      ? hasRole(requiredRoles[0])
      : hasAnyRole(requiredRoles);

    if (!hasAccess) {
      return <Navigate to="/unauthorized" replace />;
    }
  }

  return children;
};

export default ProtectedRoute;
