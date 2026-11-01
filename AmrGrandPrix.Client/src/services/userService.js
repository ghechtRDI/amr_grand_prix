/**
 * User Service
 * Admin-only account management: listing site accounts, changing roles, locking, and deleting.
 */

import { authHeaders, request } from './api';

export const ROLES = ['ReadOnly', 'Manager', 'Admin'];

/** { users: UserResponse[], page, pageSize, totalUsers, totalPages } */
export const getUsers = ({ page = 1, pageSize = 25, search = '' } = {}) =>
  request(`/api/users?${new URLSearchParams({ page, pageSize, search })}`, { headers: authHeaders() });

/** Replaces the user's role(s) with `roleName`. */
export const assignRole = (userId, roleName) =>
  request(`/api/users/${userId}/role`, {
    method: 'PUT',
    headers: authHeaders(),
    body: JSON.stringify({ roleName }),
  });

export const setLocked = (userId, locked) =>
  request(`/api/users/${userId}/lock`, {
    method: 'PUT',
    headers: authHeaders(),
    body: JSON.stringify({ locked }),
  });

export const deleteUser = (userId) =>
  request(`/api/users/${userId}`, { method: 'DELETE', headers: authHeaders() });
