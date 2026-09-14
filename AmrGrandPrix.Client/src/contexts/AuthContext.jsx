/**
 * Auth Context
 * Provides authentication state and functions throughout the app
 */

import { createContext, useState, useEffect, useCallback } from 'react';
import * as authService from '../services/authService';
import * as tokenService from '../services/tokenService';

// eslint-disable-next-line react-refresh/only-export-components
export const AuthContext = createContext(null);

export const AuthProvider = ({ children }) => {
  const [user, setUser] = useState(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);

  /**
   * Initialize auth state on mount
   */
  useEffect(() => {
    const initializeAuth = async () => {
      try {
        const currentUser = await authService.getCurrentUser();
        setUser(currentUser);
      } catch (err) {
        console.error('Failed to initialize auth:', err);
        tokenService.clearTokens();
      } finally {
        setLoading(false);
      }
    };

    initializeAuth();
  }, []);

  /**
   * Logout function
   */
  const handleLogout = useCallback(async () => {
    try {
      await authService.logout();
    } catch (err) {
      console.error('Logout error:', err);
    } finally {
      setUser(null);
      setError(null);
    }
  }, []);

  /**
   * Set up token refresh interval
   */
  useEffect(() => {
    if (!user) return;

    const interval = setInterval(async () => {
      if (tokenService.shouldRefreshToken()) {
        try {
          await authService.refreshToken();
        } catch (err) {
          console.error('Token refresh failed:', err);
          handleLogout();
        }
      }
    }, 60000); // Check every minute

    return () => clearInterval(interval);
  }, [user, handleLogout]);

  /**
   * Login function
   */
  const login = useCallback(async (credentials) => {
    try {
      setError(null);
      const data = await authService.login(credentials);

      // User data is nested in the response
      if (data.user) {
        setUser(data.user);
      }

      return { success: true, data };
    } catch (err) {
      setError(err.message);
      return { success: false, error: err.message };
    }
  }, []);

  /**
   * Register function
   */
  const register = useCallback(async (userData) => {
    try {
      setError(null);
      const data = await authService.register(userData);
      return { success: true, data };
    } catch (err) {
      setError(err.message);
      return { success: false, error: err.message };
    }
  }, []);

  /**
   * Check if user has a specific role
   */
  const hasRole = useCallback((role) => {
    if (!user || !user.roles) return false;
    return user.roles.includes(role);
  }, [user]);

  /**
   * Check if user has any of the specified roles
   */
  const hasAnyRole = useCallback((roles) => {
    if (!user || !user.roles) return false;
    return roles.some(role => user.roles.includes(role));
  }, [user]);

  /**
   * Check if user is authenticated
   */
  const isAuthenticated = useCallback(() => {
    return !!user && authService.isAuthenticated();
  }, [user]);

  const value = {
    user,
    loading,
    error,
    login,
    register,
    logout: handleLogout,
    hasRole,
    hasAnyRole,
    isAuthenticated,
  };

  return (
    <AuthContext.Provider value={value}>
      {children}
    </AuthContext.Provider>
  );
};

export default AuthContext;
