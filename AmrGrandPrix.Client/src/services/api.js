/**
 * Shared fetch helpers for JSON API calls: auth headers, and errors surfaced using the
 * server's `{ message }` body when there is one.
 */

import * as tokenService from './tokenService';

export const authHeaders = () => ({
  'Content-Type': 'application/json',
  Authorization: `Bearer ${tokenService.getAccessToken()}`,
});

export const readError = async (response) => {
  const text = await response.text().catch(() => '');
  if (!text) return `HTTP ${response.status}: ${response.statusText}`;
  try {
    const data = JSON.parse(text);
    return data.message || data.title || text;
  } catch {
    return text;
  }
};

/** fetch() that returns parsed JSON (null for 204) and throws an Error with the server's message. */
export const request = async (url, options = {}) => {
  const response = await fetch(url, options);
  if (!response.ok) throw new Error(await readError(response));
  return response.status === 204 ? null : response.json();
};
