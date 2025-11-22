/**
 * Auth Service
 * Handles all authentication-related API calls
 */

import * as tokenService from './tokenService';

// API base URL - will use Vite proxy configuration for /api routes
const API_URL = '/api/auth';

/**
 * Helper function to make API requests with proper headers
 */
const makeRequest = async (url, options = {}) => {
  const token = tokenService.getAccessToken();

  const headers = {
    'Content-Type': 'application/json',
    ...options.headers,
  };

  if (token && !options.skipAuth) {
    headers.Authorization = `Bearer ${token}`;
  }

  const response = await fetch(url, {
    ...options,
    headers,
  });

  const data = await response.json().catch(() => null);

  if (!response.ok) {
    const error = new Error(data?.message || data?.title || 'Request failed');
    error.status = response.status;
    error.data = data;
    throw error;
  }

  return data;
};

/**
 * Register a new user
 */
export const register = async (userData) => {
  const data = await makeRequest(`${API_URL}/register`, {
    method: 'POST',
    body: JSON.stringify(userData),
    skipAuth: true,
  });

  return data;
};

/**
 * Login user and store tokens
 */
export const login = async (credentials) => {
  const data = await makeRequest(`${API_URL}/login`, {
    method: 'POST',
    body: JSON.stringify(credentials),
    skipAuth: true,
  });

  if (data.accessToken && data.refreshToken) {
    tokenService.setTokens(data.accessToken, data.refreshToken);

    // Store user data
    const user = {
      id: data.userId,
      email: data.email,
      firstName: data.firstName,
      lastName: data.lastName,
      roles: data.roles,
    };
    tokenService.setUser(user);
  }

  return data;
};

/**
 * Logout user and clear tokens
 */
export const logout = async () => {
  try {
    // Call logout endpoint to invalidate refresh token on server
    await makeRequest(`${API_URL}/logout`, {
      method: 'POST',
    });
  } catch (error) {
    console.error('Logout error:', error);
  } finally {
    // Always clear local tokens even if server request fails
    tokenService.clearTokens();
  }
};

/**
 * Refresh access token using refresh token
 */
export const refreshToken = async () => {
  const refreshToken = tokenService.getRefreshToken();

  if (!refreshToken) {
    throw new Error('No refresh token available');
  }

  const data = await makeRequest(`${API_URL}/refresh-token`, {
    method: 'POST',
    body: JSON.stringify({ refreshToken }),
    skipAuth: true,
  });

  if (data.accessToken && data.refreshToken) {
    tokenService.setTokens(data.accessToken, data.refreshToken);
  }

  return data;
};

/**
 * Confirm email with token
 */
export const confirmEmail = async (userId, token) => {
  const params = new URLSearchParams({ userId, token });
  const data = await makeRequest(`${API_URL}/confirm-email?${params}`, {
    method: 'GET',
    skipAuth: true,
  });

  return data;
};

/**
 * Resend email confirmation
 */
export const resendConfirmation = async (email) => {
  const data = await makeRequest(`${API_URL}/resend-confirmation`, {
    method: 'POST',
    body: JSON.stringify({ email }),
    skipAuth: true,
  });

  return data;
};

/**
 * Request password reset
 */
export const forgotPassword = async (email) => {
  const data = await makeRequest(`${API_URL}/forgot-password`, {
    method: 'POST',
    body: JSON.stringify({ email }),
    skipAuth: true,
  });

  return data;
};

/**
 * Reset password with token
 */
export const resetPassword = async (resetData) => {
  const data = await makeRequest(`${API_URL}/reset-password`, {
    method: 'POST',
    body: JSON.stringify(resetData),
    skipAuth: true,
  });

  return data;
};

/**
 * Get current user info
 */
export const getCurrentUser = async () => {
  // First check if we have a valid token
  const token = tokenService.getAccessToken();
  if (!token || tokenService.isTokenExpired(token)) {
    return null;
  }

  // Return cached user data
  return tokenService.getUser();
};

/**
 * Check if user is authenticated
 */
export const isAuthenticated = () => {
  const token = tokenService.getAccessToken();
  return token && !tokenService.isTokenExpired(token);
};

export default {
  register,
  login,
  logout,
  refreshToken,
  confirmEmail,
  resendConfirmation,
  forgotPassword,
  resetPassword,
  getCurrentUser,
  isAuthenticated,
};
