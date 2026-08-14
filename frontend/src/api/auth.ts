import { request } from './client';
import type { LoginRequest, LoginResponse } from '../types/ticket';

export function login(credentials: LoginRequest): Promise<LoginResponse> {
  return request<LoginResponse>('/api/auth/login', { method: 'POST', body: credentials });
}
