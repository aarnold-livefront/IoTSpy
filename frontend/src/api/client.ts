const TOKEN_KEY = 'iotspy_token'

export function getToken(): string | null {
  return localStorage.getItem(TOKEN_KEY)
}

export function setToken(token: string): void {
  localStorage.setItem(TOKEN_KEY, token)
}

export function clearToken(): void {
  localStorage.removeItem(TOKEN_KEY)
}

export class ApiError extends Error {
  constructor(
    public readonly status: number,
    message: string,
  ) {
    super(message)
    this.name = 'ApiError'
  }
}

let onUnauthorized: (() => void) | null = null

export function setOnUnauthorized(cb: (() => void) | null): void {
  onUnauthorized = cb
}

function normalizeHeaders(h: HeadersInit | undefined): Record<string, string> {
  if (!h) return {}
  if (h instanceof Headers) {
    const out: Record<string, string> = {}
    h.forEach((v, k) => { out[k] = v })
    return out
  }
  if (Array.isArray(h)) {
    return Object.fromEntries(h)
  }
  return h as Record<string, string>
}

export async function apiFetch<T>(
  path: string,
  init: RequestInit = {},
): Promise<T> {
  const token = getToken()
  const headers: Record<string, string> = {
    'Content-Type': 'application/json',
    ...normalizeHeaders(init.headers),
  }
  if (token) {
    headers['Authorization'] = `Bearer ${token}`
  }

  const res = await fetch(path, { ...init, headers })

  if (!res.ok) {
    let message = res.statusText
    try {
      const body = await res.json()
      message = body.error ?? body.message ?? message
    } catch {
      // ignore parse errors
    }

    if (res.status === 401) {
      clearToken()
      onUnauthorized?.()
    }

    throw new ApiError(res.status, message)
  }

  // 204 No Content
  if (res.status === 204) {
    return undefined as T
  }

  return res.json() as Promise<T>
}
