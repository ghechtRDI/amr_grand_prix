/**
 * Token Service
 * Handles JWT token storage, retrieval, and validation
 */

const ACCESS_TOKEN_KEY = 'access_token';
const REFRESH_TOKEN_KEY = 'refresh_token';
const USER_KEY = 'user';

/**
 * Store authentication tokens in localStorage
 */
export const setTokens = (accessToken, refreshToken) => {
  if (accessToken) {
    localStorage.setItem(ACCESS_TOKEN_KEY, accessToken);
  }
  if (refreshToken) {
    localStorage.setItem(REFRESH_TOKEN_KEY, refreshToken);
  }
};

/**
 * Get access token from localStorage
 */
export const getAccessToken = () => {
  return localStorage.getItem(ACCESS_TOKEN_KEY);
};

/**
 * Get refresh token from localStorage
 */
export const getRefreshToken = () => {
  return localStorage.getItem(REFRESH_TOKEN_KEY);
};

/**
 * Store user data in localStorage
 */
export const setUser = (user) => {
  if (user) {
    localStorage.setItem(USER_KEY, JSON.stringify(user));
  }
};

/**
 * Get user data from localStorage
 */
export const getUser = () => {
  const userStr = localStorage.getItem(USER_KEY);
  if (userStr) {
    try {
      return JSON.parse(userStr);
    } catch (error) {
      console.error('Failed to parse user data:', error);
      return null;
    }
  }
  return null;
};

/**
 * Clear all authentication data from localStorage
 */
export const clearTokens = () => {
  localStorage.removeItem(ACCESS_TOKEN_KEY);
  localStorage.removeItem(REFRESH_TOKEN_KEY);
  localStorage.removeItem(USER_KEY);
};

/**
 * Decode JWT token payload (without verification)
 * Note: This is client-side decoding only. Server still validates the token.
 */
export const decodeToken = (token) => {
  try {
    const base64Url = token.split('.')[1];
    const base64 = base64Url.replace(/-/g, '+').replace(/_/g, '/');
    const jsonPayload = decodeURIComponent(
      atob(base64)
        .split('')
        .map((c) => '%' + ('00' + c.charCodeAt(0).toString(16)).slice(-2))
        .join('')
    );
    return JSON.parse(jsonPayload);
  } catch (error) {
    console.error('Failed to decode token:', error);
    return null;
  }
};

/**
 * Check if a token is expired
 */
export const isTokenExpired = (token) => {
  if (!token) return true;

  const decoded = decodeToken(token);
  if (!decoded || !decoded.exp) return true;

  // exp is in seconds, Date.now() is in milliseconds
  const currentTime = Date.now() / 1000;
  return decoded.exp < currentTime;
};

/**
 * Check if access token is expired or about to expire (within 1 minute)
 */
export const shouldRefreshToken = () => {
  const token = getAccessToken();
  if (!token) return false;

  const decoded = decodeToken(token);
  if (!decoded || !decoded.exp) return false;

  // Refresh if token expires in less than 1 minute
  const currentTime = Date.now() / 1000;
  const timeUntilExpiry = decoded.exp - currentTime;
  return timeUntilExpiry < 60; // 60 seconds
};

/**
 * Get user roles from access token
 */
export const getUserRoles = () => {
  const token = getAccessToken();
  if (!token) return [];

  const decoded = decodeToken(token);
  if (!decoded) return [];

  // Roles can be in different claim types depending on the JWT structure
  const roles = decoded['http://schemas.microsoft.com/ws/2008/06/identity/claims/role']
    || decoded.role
    || decoded.roles
    || [];

  // Ensure we return an array
  return Array.isArray(roles) ? roles : [roles];
};

/**
 * Check if user has a specific role
 */
export const hasRole = (role) => {
  const roles = getUserRoles();
  return roles.includes(role);
};

/**
 * Check if user has any of the specified roles
 */
export const hasAnyRole = (requiredRoles) => {
  const userRoles = getUserRoles();
  return requiredRoles.some(role => userRoles.includes(role));
};

export default {
  setTokens,
  getAccessToken,
  getRefreshToken,
  setUser,
  getUser,
  clearTokens,
  decodeToken,
  isTokenExpired,
  shouldRefreshToken,
  getUserRoles,
  hasRole,
  hasAnyRole,
};
