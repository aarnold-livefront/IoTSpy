import { apiFetch } from './client'
import type {
  AuthStatusResponse,
  LoginRequest,
  LoginResponse,
  SetupRequest,
} from '../types/api'

export function getAuthStatus(): Promise<AuthStatusResponse> {
  return apiFetch<AuthStatusResponse>('/api/auth/status')
}

export function login(req: LoginRequest): Promise<LoginResponse> {
  return apiFetch<LoginResponse>('/api/auth/login', {
    method: 'POST',
    body: JSON.stringify(req),
  })
}

export function setup(req: SetupRequest): Promise<{ message: string }> {
  return apiFetch<{ message: string }>('/api/auth/setup', {
    method: 'POST',
    body: JSON.stringify(req),
  })
}
