import { apiFetch } from './client'
import type {
  AuthStatusResponse,
  CurrentUser,
  LoginRequest,
  LoginResponse,
  SetupRequest,
} from '../types/api'

export function getAuthStatus(): Promise<AuthStatusResponse> {
  return apiFetch<AuthStatusResponse>('/api/auth/status')
}

export function getMe(): Promise<{ user: CurrentUser }> {
  return apiFetch<{ user: CurrentUser }>('/api/auth/me')
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
