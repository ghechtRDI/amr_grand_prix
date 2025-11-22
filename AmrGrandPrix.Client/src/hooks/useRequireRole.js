/**
 * useRequireRole Hook
 * Checks if user has required role(s) and redirects if not
 */

import { useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import { useAuth } from './useAuth';

/**
 * @param {string|string[]} requiredRoles - Single role or array of roles
 * @param {string} redirectTo - Path to redirect if user doesn't have role
 * @returns {object} - { hasAccess: boolean, loading: boolean }
 */
export const useRequireRole = (requiredRoles, redirectTo = '/unauthorized') => {
  const { hasRole, hasAnyRole, loading, isAuthenticated } = useAuth();
  const navigate = useNavigate();

  // Normalize to array
  const roles = Array.isArray(requiredRoles) ? requiredRoles : [requiredRoles];

  const hasAccess = isAuthenticated() && (
    roles.length === 1 ? hasRole(roles[0]) : hasAnyRole(roles)
  );

  useEffect(() => {
    if (!loading && isAuthenticated() && !hasAccess) {
      navigate(redirectTo, { replace: true });
    }
  }, [hasAccess, loading, navigate, redirectTo, isAuthenticated]);

  return { hasAccess, loading };
};

export default useRequireRole;
