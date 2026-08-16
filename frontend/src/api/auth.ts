import { request } from './client';
import type { LoginRequest, LoginResponse } from '../types/ticket';

/** Signs in. The token itself arrives as a cookie; this is only what to display. */
export function login(credentials: LoginRequest): Promise<LoginResponse> {
  return request<LoginResponse>('/api/auth/login', { method: 'POST', body: credentials });
}

/** Clears the session cookie server-side — the page cannot delete it itself. */
export function logout(): Promise<void> {
  return request<void>('/api/auth/logout', { method: 'POST' });
}

/**
 * Who the cookie says we are.
 *
 * The only way to restore a session after a refresh, now that the frontend
 * holds no token to inspect. Throws `ApiError` with status 401 when there is no
 * usable cookie, which is the ordinary signed-out case.
 */
export function currentSession(): Promise<LoginResponse> {
  return request<LoginResponse>('/api/auth/me');
}
