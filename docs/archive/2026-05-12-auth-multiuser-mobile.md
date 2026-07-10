# Auth Session Redirect, Multi-User Login & XS Header Fix — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Redirect to `/login` on expired JWT, show username field on the login page when the server is in multi-user mode, and make the header fit on XS/SM phones via CSS compaction + an overflow dropdown for secondary actions.

**Architecture:** A registered callback slot in `client.ts` decouples `apiFetch` from React routing — `useAuthInit` registers the redirect handler after successful auth. The `multiUser` flag flows from `getAuthStatus()` through the auth store to `LoginPage` without an extra fetch. The header fix is layered: CSS hides logo text / badge label / Root CA text at ≤640px, while a JS-driven `⋮` dropdown moves Admin/Theme/Settings/Logout off the toolbar on mobile.

**Tech Stack:** Vite 6 + React 19 + TypeScript, Vitest + @testing-library/react, CSS custom properties

---

## File Map

| File | Role |
|---|---|
| `frontend/src/api/client.ts` | Add `setOnUnauthorized` export; call callback + `clearToken()` on 401 in `apiFetch` |
| `frontend/src/store/authStore.ts` | Extend `AuthState` with `multiUser: boolean`; add `SET_MULTI_USER` action; export reducer for testing |
| `frontend/src/hooks/useAuth.ts` | Dispatch `SET_MULTI_USER` from `getAuthStatus()` result; register `setOnUnauthorized` after successful auth |
| `frontend/src/pages/LoginPage.tsx` | Read `multiUser` from `useAuthState()`; conditionally render username field |
| `frontend/src/components/proxy/ProxyStatusBadge.tsx` | Wrap label text in `.proxy-status-badge__label` span |
| `frontend/src/components/layout/Header.tsx` | Add `menuOpen` state + `⋮` hamburger + dropdown; label span on Root CA; `header__btn--desktop` on secondary icon buttons |
| `frontend/src/styles/header.css` | ≤640px media query: hide logo text, badge label, Root CA label, desktop buttons; show hamburger; add dropdown styles |
| `frontend/src/__tests__/client.test.ts` | New — unit tests for 401 callback behaviour in `apiFetch` |
| `frontend/src/__tests__/authStore.test.ts` | New — reducer unit tests for `SET_MULTI_USER` |
| `frontend/src/__tests__/LoginPage.test.tsx` | New — component tests for conditional username field and form submission |
| `frontend/src/__tests__/Header.test.tsx` | New — hamburger open/close and menu item rendering |

---

## Task 1: `client.ts` — 401 callback + tests

**Files:**
- Modify: `frontend/src/api/client.ts`
- Create: `frontend/src/__tests__/client.test.ts`

- [ ] **Step 1.1: Write failing tests**

Create `frontend/src/__tests__/client.test.ts`:

```ts
import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest'
import { apiFetch, setOnUnauthorized, setToken, clearToken } from '../api/client'

function mockFetchResponse(status: number, body: object) {
  vi.stubGlobal(
    'fetch',
    vi.fn().mockResolvedValue(new Response(JSON.stringify(body), { status })),
  )
}

describe('apiFetch — 401 handling', () => {
  beforeEach(() => {
    // Reset the callback between tests
    setOnUnauthorized(null)
    clearToken()
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('calls the registered onUnauthorized callback on 401', async () => {
    mockFetchResponse(401, { error: 'Unauthorized' })
    const cb = vi.fn()
    setOnUnauthorized(cb)
    setToken('tok')

    await expect(apiFetch('/api/test')).rejects.toThrow()
    expect(cb).toHaveBeenCalledOnce()
  })

  it('clears the stored token on 401', async () => {
    mockFetchResponse(401, { error: 'Unauthorized' })
    setToken('tok')

    await expect(apiFetch('/api/test')).rejects.toThrow()
    expect(localStorage.getItem('iotspy_token')).toBeNull()
  })

  it('does not call the callback on non-401 errors', async () => {
    mockFetchResponse(403, { error: 'Forbidden' })
    const cb = vi.fn()
    setOnUnauthorized(cb)

    await expect(apiFetch('/api/test')).rejects.toThrow()
    expect(cb).not.toHaveBeenCalled()
  })

  it('does not throw if no callback is registered on 401', async () => {
    mockFetchResponse(401, { error: 'Unauthorized' })
    setOnUnauthorized(null)

    // Should reject with ApiError, not crash from missing callback
    await expect(apiFetch('/api/test')).rejects.toMatchObject({ status: 401 })
  })
})
```

- [ ] **Step 1.2: Run tests to confirm they fail**

```bash
cd frontend && npm test -- client
```

Expected: 4 failures — `setOnUnauthorized is not a function` or similar.

- [ ] **Step 1.3: Implement changes to `client.ts`**

Open `frontend/src/api/client.ts`. The full updated file:

```ts
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

export async function apiFetch<T>(
  path: string,
  init: RequestInit = {},
): Promise<T> {
  const token = getToken()
  const headers: Record<string, string> = {
    'Content-Type': 'application/json',
    ...(init.headers as Record<string, string>),
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
```

- [ ] **Step 1.4: Run tests to confirm they pass**

```bash
cd frontend && npm test -- client
```

Expected: 4 passed.

- [ ] **Step 1.5: Commit**

```bash
cd frontend && git add src/api/client.ts src/__tests__/client.test.ts
git commit -m "feat: add setOnUnauthorized callback to apiFetch for 401 interception"
```

---

## Task 2: `authStore.ts` — add `multiUser` + `SET_MULTI_USER` action

**Files:**
- Modify: `frontend/src/store/authStore.ts`
- Create: `frontend/src/__tests__/authStore.test.ts`

- [ ] **Step 2.1: Write failing tests**

Create `frontend/src/__tests__/authStore.test.ts`:

```ts
import { describe, it, expect } from 'vitest'
import { authReducer } from '../store/authStore'
import type { AuthState } from '../store/authStore'

const base: AuthState = { status: 'unauthenticated', token: null, multiUser: false }

describe('authReducer — SET_MULTI_USER', () => {
  it('sets multiUser to true', () => {
    const next = authReducer(base, { type: 'SET_MULTI_USER', value: true })
    expect(next.multiUser).toBe(true)
    expect(next.status).toBe('unauthenticated') // other fields unchanged
  })

  it('sets multiUser to false', () => {
    const state: AuthState = { ...base, multiUser: true }
    const next = authReducer(state, { type: 'SET_MULTI_USER', value: false })
    expect(next.multiUser).toBe(false)
  })

  it('initial multiUser is false', () => {
    // Simulate the default action path — unknown action returns state unchanged
    const next = authReducer(base, { type: 'SET_MULTI_USER', value: false })
    expect(next.multiUser).toBe(false)
  })
})
```

- [ ] **Step 2.2: Run tests to confirm they fail**

```bash
cd frontend && npm test -- authStore
```

Expected: failures — `authReducer` is not exported / `multiUser` property missing.

- [ ] **Step 2.3: Implement changes to `authStore.ts`**

Open `frontend/src/store/authStore.ts`. Replace with:

```ts
import {
  createContext,
  useContext,
  useReducer,
  type Dispatch,
  type ReactNode,
} from 'react'
import { clearToken, setToken } from '../api/client'

// ── State ────────────────────────────────────────────────────────────────────

export type AuthStatus = 'unknown' | 'no-password' | 'unauthenticated' | 'authenticated'

export interface AuthState {
  status: AuthStatus
  token: string | null
  multiUser: boolean
}

const initialState: AuthState = {
  status: 'unknown',
  token: null,
  multiUser: false,
}

// ── Actions ───────────────────────────────────────────────────────────────────

type AuthAction =
  | { type: 'SET_NO_PASSWORD' }
  | { type: 'SET_UNAUTHENTICATED' }
  | { type: 'SET_AUTHENTICATED'; token: string }
  | { type: 'SET_MULTI_USER'; value: boolean }
  | { type: 'LOGOUT' }

export function authReducer(state: AuthState, action: AuthAction): AuthState {
  switch (action.type) {
    case 'SET_NO_PASSWORD':
      return { ...state, status: 'no-password', token: null }
    case 'SET_UNAUTHENTICATED':
      return { ...state, status: 'unauthenticated', token: null }
    case 'SET_AUTHENTICATED':
      return { ...state, status: 'authenticated', token: action.token }
    case 'SET_MULTI_USER':
      return { ...state, multiUser: action.value }
    case 'LOGOUT':
      clearToken()
      return { ...state, status: 'unauthenticated', token: null }
    default:
      return state
  }
}

// ── Context ───────────────────────────────────────────────────────────────────

const AuthStateCtx = createContext<AuthState>(initialState)
const AuthDispatchCtx = createContext<Dispatch<AuthAction>>(() => undefined)

import { createElement } from 'react'

export function AuthProvider({ children }: { children: ReactNode }) {
  const [state, dispatch] = useReducer(authReducer, initialState)
  return createElement(
    AuthStateCtx.Provider,
    { value: state },
    createElement(AuthDispatchCtx.Provider, { value: dispatch }, children),
  )
}

export function useAuthState(): AuthState {
  return useContext(AuthStateCtx)
}

export function useAuthDispatch(): Dispatch<AuthAction> {
  return useContext(AuthDispatchCtx)
}

// ── Helpers for dispatch consumers ────────────────────────────────────────────

export function dispatchLogin(dispatch: Dispatch<AuthAction>, token: string): void {
  setToken(token)
  dispatch({ type: 'SET_AUTHENTICATED', token })
}
```

Key changes from original:
- `AuthState` gains `multiUser: boolean`
- `initialState` sets `multiUser: false`
- Reducer is renamed from the inner `reducer` to exported `authReducer`
- `AuthProvider` uses `authReducer` instead of `reducer`
- Existing `{ status: 'no-password', token: null }` shapes use spread to preserve `multiUser`

- [ ] **Step 2.4: Run tests to confirm they pass**

```bash
cd frontend && npm test -- authStore
```

Expected: 3 passed.

- [ ] **Step 2.5: Commit**

```bash
cd frontend && git add src/store/authStore.ts src/__tests__/authStore.test.ts
git commit -m "feat: add multiUser field and SET_MULTI_USER action to auth store"
```

---

## Task 3: `useAuth.ts` — wire up `SET_MULTI_USER` dispatch and `setOnUnauthorized` registration

**Files:**
- Modify: `frontend/src/hooks/useAuth.ts`

No new unit test file for this task — the hook orchestrates side effects that are hard to unit-test in isolation. The integration is verified by `npm test` at the end of Task 4 (LoginPage tests mock `useAuthState` directly).

- [ ] **Step 3.1: Update `useAuthInit` in `frontend/src/hooks/useAuth.ts`**

The full updated file:

```ts
import { useEffect } from 'react'
import { useNavigate, useLocation } from 'react-router-dom'
import { getToken, setOnUnauthorized } from '../api/client'
import { getAuthStatus, getMe, login as apiLogin, setup as apiSetup } from '../api/auth'
import {
  dispatchLogin,
  useAuthDispatch,
  useAuthState,
  type AuthState,
} from '../store/authStore'
import type { CurrentUser, LoginRequest, SetupRequest } from '../types/api'

const CURRENT_USER_KEY = 'iotspy-user'

export function useCurrentUser(): CurrentUser | null {
  const raw = typeof localStorage !== 'undefined' ? localStorage.getItem(CURRENT_USER_KEY) : null
  if (!raw) return null
  try {
    return JSON.parse(raw) as CurrentUser
  } catch {
    return null
  }
}

export function useAuthInit(): AuthState {
  const dispatch = useAuthDispatch()
  const state = useAuthState()
  const navigate = useNavigate()
  const location = useLocation()

  useEffect(() => {
    let cancelled = false
    async function init() {
      try {
        const { passwordSet, multiUser } = await getAuthStatus()
        if (cancelled) return
        dispatch({ type: 'SET_MULTI_USER', value: multiUser ?? false })
        if (!passwordSet) {
          dispatch({ type: 'SET_NO_PASSWORD' })
          navigate('/setup', { replace: true })
          return
        }
        const token = getToken()
        if (!token) {
          dispatch({ type: 'SET_UNAUTHENTICATED' })
          navigate('/login', { replace: true })
          return
        }
        dispatch({ type: 'SET_AUTHENTICATED', token })
        setOnUnauthorized(() => {
          dispatch({ type: 'LOGOUT' })
          navigate('/login', { replace: true })
        })
        // Ensure user profile is in localStorage (missing for legacy/pre-multiuser logins)
        if (!localStorage.getItem(CURRENT_USER_KEY)) {
          try {
            const { user } = await getMe()
            localStorage.setItem(CURRENT_USER_KEY, JSON.stringify(user))
          } catch { /* non-fatal — wrench icon just won't show */ }
        }
        // Only redirect to / if currently on an unauthenticated page
        if (['/login', '/setup'].includes(location.pathname)) {
          navigate('/', { replace: true })
        }
      } catch {
        if (!cancelled) {
          dispatch({ type: 'SET_UNAUTHENTICATED' })
          navigate('/login', { replace: true })
        }
      }
    }
    if (state.status === 'unknown') {
      void init()
    }
    return () => {
      cancelled = true
    }
  }, [state.status, dispatch, navigate, location.pathname])

  return state
}

export function useLogin() {
  const dispatch = useAuthDispatch()
  const navigate = useNavigate()

  return async (req: LoginRequest) => {
    const { token, user } = await apiLogin(req)
    if (user) {
      localStorage.setItem(CURRENT_USER_KEY, JSON.stringify(user))
    }
    dispatchLogin(dispatch, token)
    navigate('/', { replace: true })
  }
}

export function useSetup() {
  const navigate = useNavigate()

  return async (req: SetupRequest) => {
    await apiSetup(req)
    navigate('/login', { replace: true })
  }
}

export function useLogout() {
  const dispatch = useAuthDispatch()
  const navigate = useNavigate()

  return () => {
    localStorage.removeItem(CURRENT_USER_KEY)
    dispatch({ type: 'LOGOUT' })
    navigate('/login', { replace: true })
  }
}
```

Changes from original:
- Import `setOnUnauthorized` from `../api/client`
- Destructure `multiUser` from `getAuthStatus()` result
- Dispatch `SET_MULTI_USER` immediately after status resolves (before the `!passwordSet` check, so it's always set)
- Call `setOnUnauthorized(...)` after `SET_AUTHENTICATED` — only when we have a valid token

- [ ] **Step 3.2: Confirm TypeScript still compiles**

```bash
cd frontend && npx tsc --noEmit
```

Expected: no errors.

- [ ] **Step 3.3: Run full test suite to confirm nothing broken**

```bash
cd frontend && npm test
```

Expected: all previously passing tests still pass.

- [ ] **Step 3.4: Commit**

```bash
cd frontend && git add src/hooks/useAuth.ts
git commit -m "feat: dispatch SET_MULTI_USER and register 401 redirect handler in useAuthInit"
```

---

## Task 4: `LoginPage.tsx` — conditional username field + tests

**Files:**
- Modify: `frontend/src/pages/LoginPage.tsx`
- Create: `frontend/src/__tests__/LoginPage.test.tsx`

- [ ] **Step 4.1: Write failing tests**

Create `frontend/src/__tests__/LoginPage.test.tsx`:

```tsx
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, it, expect, vi, beforeEach } from 'vitest'
import LoginPage from '../pages/LoginPage'
import { ApiError } from '../api/client'
import type { AuthState } from '../store/authStore'

const mockLogin = vi.fn()

vi.mock('../hooks/useAuth', () => ({
  useLogin: () => mockLogin,
}))

vi.mock('../store/authStore', () => ({
  useAuthState: vi.fn(),
}))

import { useAuthState } from '../store/authStore'

const singleUserState: AuthState = { status: 'unauthenticated', token: null, multiUser: false }
const multiUserState: AuthState = { status: 'unauthenticated', token: null, multiUser: true }

describe('LoginPage', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    vi.mocked(useAuthState).mockReturnValue(singleUserState)
  })

  it('shows only password field when multiUser is false', () => {
    render(<LoginPage />)
    expect(screen.queryByLabelText('Username')).not.toBeInTheDocument()
    expect(screen.getByLabelText('Password')).toBeInTheDocument()
  })

  it('shows username and password fields when multiUser is true', () => {
    vi.mocked(useAuthState).mockReturnValue(multiUserState)
    render(<LoginPage />)
    expect(screen.getByLabelText('Username')).toBeInTheDocument()
    expect(screen.getByLabelText('Password')).toBeInTheDocument()
  })

  it('submits with hardcoded admin username when multiUser is false', async () => {
    mockLogin.mockResolvedValueOnce(undefined)
    render(<LoginPage />)
    await userEvent.type(screen.getByLabelText('Password'), 'secret')
    await userEvent.click(screen.getByRole('button', { name: 'Sign in' }))
    expect(mockLogin).toHaveBeenCalledWith({ username: 'admin', password: 'secret' })
  })

  it('submits with the typed username when multiUser is true', async () => {
    vi.mocked(useAuthState).mockReturnValue(multiUserState)
    mockLogin.mockResolvedValueOnce(undefined)
    render(<LoginPage />)
    await userEvent.type(screen.getByLabelText('Username'), 'alice')
    await userEvent.type(screen.getByLabelText('Password'), 'secret')
    await userEvent.click(screen.getByRole('button', { name: 'Sign in' }))
    expect(mockLogin).toHaveBeenCalledWith({ username: 'alice', password: 'secret' })
  })

  it('shows error message on failed login', async () => {
    mockLogin.mockRejectedValueOnce(new ApiError(401, 'Invalid credentials'))
    render(<LoginPage />)
    await userEvent.type(screen.getByLabelText('Password'), 'wrong')
    await userEvent.click(screen.getByRole('button', { name: 'Sign in' }))
    await waitFor(() => expect(screen.getByText('Invalid credentials')).toBeInTheDocument())
  })

  it('disables the submit button while loading', async () => {
    mockLogin.mockImplementation(() => new Promise(() => {})) // never resolves
    render(<LoginPage />)
    await userEvent.type(screen.getByLabelText('Password'), 'secret')
    await userEvent.click(screen.getByRole('button', { name: 'Sign in' }))
    expect(screen.getByRole('button', { name: 'Signing in…' })).toBeDisabled()
  })
})
```

- [ ] **Step 4.2: Run tests to confirm they fail**

```bash
cd frontend && npm test -- LoginPage
```

Expected: failures — `useAuthState` not imported in `LoginPage` / `multiUser` undefined.

- [ ] **Step 4.3: Implement changes to `LoginPage.tsx`**

Replace `frontend/src/pages/LoginPage.tsx` with:

```tsx
import { useState } from 'react'
import { ApiError } from '../api/client'
import { useLogin } from '../hooks/useAuth'
import { useAuthState } from '../store/authStore'
import '../styles/auth.css'

export default function LoginPage() {
  const login = useLogin()
  const { multiUser } = useAuthState()
  const [username, setUsername] = useState('')
  const [password, setPassword] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [loading, setLoading] = useState(false)

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault()
    setError(null)
    setLoading(true)
    try {
      await login({ username: multiUser ? username : 'admin', password })
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Login failed.')
    } finally {
      setLoading(false)
    }
  }

  return (
    <div className="auth-page">
      <div className="auth-card">
        <div className="auth-logo">
          <div className="auth-logo-icon">I</div>
          <span className="auth-title">IoTSpy</span>
        </div>
        <p className="auth-subtitle">Sign in to your dashboard.</p>
        <form className="auth-form" onSubmit={handleSubmit}>
          {error && <div className="auth-error">{error}</div>}
          {multiUser && (
            <div className="form-group">
              <label className="form-label" htmlFor="username">Username</label>
              <input
                id="username"
                className="form-input"
                type="text"
                value={username}
                onChange={(e) => setUsername(e.target.value)}
                autoFocus
                required
              />
            </div>
          )}
          <div className="form-group">
            <label className="form-label" htmlFor="password">Password</label>
            <input
              id="password"
              className="form-input"
              type="password"
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              autoFocus={!multiUser}
              required
            />
          </div>
          <button className="btn-primary" type="submit" disabled={loading}>
            {loading ? 'Signing in…' : 'Sign in'}
          </button>
        </form>
      </div>
    </div>
  )
}
```

- [ ] **Step 4.4: Run tests to confirm they pass**

```bash
cd frontend && npm test -- LoginPage
```

Expected: 6 passed.

- [ ] **Step 4.5: Run full suite**

```bash
cd frontend && npm test
```

Expected: all tests pass (previously 61 + 10 new = 71 total).

- [ ] **Step 4.6: Commit**

```bash
cd frontend && git add src/pages/LoginPage.tsx src/__tests__/LoginPage.test.tsx
git commit -m "feat: show username field on login page when server reports multiUser mode"
```

---

## Task 5: `ProxyStatusBadge.tsx` — wrap label text

**Files:**
- Modify: `frontend/src/components/proxy/ProxyStatusBadge.tsx`

Small structural change so CSS can target the text independently of the status dot.

- [ ] **Step 5.1: Update `ProxyStatusBadge.tsx`**

Replace `frontend/src/components/proxy/ProxyStatusBadge.tsx` with:

```tsx
import '../../styles/header.css'

interface Props {
  isRunning: boolean
  port: number
  signalRConnected: boolean
}

export default function ProxyStatusBadge({ isRunning, port, signalRConnected }: Props) {
  const dotClass = isRunning
    ? 'proxy-status-badge__dot proxy-status-badge__dot--running'
    : signalRConnected
      ? 'proxy-status-badge__dot proxy-status-badge__dot--connecting'
      : 'proxy-status-badge__dot'

  const label = isRunning ? `Proxy: :${port}` : 'Proxy: Stopped'

  return (
    <div className="proxy-status-badge" title={isRunning ? `Listening on port ${port}` : 'Proxy not running'}>
      <span className={dotClass} />
      <span className="proxy-status-badge__label">{label}</span>
    </div>
  )
}
```

Only change: the label `<span>` gains class `proxy-status-badge__label`.

- [ ] **Step 5.2: Run full suite**

```bash
cd frontend && npm test
```

Expected: all tests still pass.

- [ ] **Step 5.3: Commit**

```bash
cd frontend && git add src/components/proxy/ProxyStatusBadge.tsx
git commit -m "refactor: add proxy-status-badge__label class for mobile CSS targeting"
```

---

## Task 6: `Header.tsx` + `header.css` — hamburger menu + CSS compaction

**Files:**
- Modify: `frontend/src/components/layout/Header.tsx`
- Modify: `frontend/src/styles/header.css`
- Create: `frontend/src/__tests__/Header.test.tsx`

CSS is disabled in Vitest (`css: false`), so we test JS behaviour (menu open/close, item rendering) and verify CSS visually.

- [ ] **Step 6.1: Write failing Header tests**

Create `frontend/src/__tests__/Header.test.tsx`:

```tsx
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, it, expect, vi, beforeEach } from 'vitest'
import { MemoryRouter } from 'react-router-dom'
import Header from '../components/layout/Header'

const noop = vi.fn()
const mockLogout = vi.fn()

vi.mock('../hooks/useAuth', () => ({
  useCurrentUser: vi.fn(),
  useLogout: () => mockLogout,
}))
vi.mock('../components/proxy/ProxyStatusBadge', () => ({
  default: () => <div data-testid="proxy-status-badge" />,
}))
vi.mock('../components/proxy/SettingsModal', () => ({
  default: ({ onClose }: { onClose: () => void }) => (
    <div data-testid="settings-modal">
      <button onClick={onClose}>Close</button>
    </div>
  ),
}))

import { useCurrentUser } from '../hooks/useAuth'

const defaultProps = {
  isRunning: false,
  port: 8080,
  settings: null,
  signalRConnected: false,
  loading: false,
  theme: 'dark' as const,
  onStart: noop,
  onStop: noop,
  onSaveSettings: vi.fn(async () => null),
  onToggleTheme: noop,
}

function renderHeader(props = {}) {
  return render(
    <MemoryRouter>
      <Header {...defaultProps} {...props} />
    </MemoryRouter>,
  )
}

describe('Header — hamburger menu', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    vi.mocked(useCurrentUser).mockReturnValue(null)
  })

  it('renders a hamburger button', () => {
    renderHeader()
    expect(screen.getByRole('button', { name: 'More options' })).toBeInTheDocument()
  })

  it('menu is hidden by default', () => {
    renderHeader()
    expect(screen.queryByRole('menu')).not.toBeInTheDocument()
  })

  it('opens the menu on hamburger click', async () => {
    renderHeader()
    await userEvent.click(screen.getByRole('button', { name: 'More options' }))
    expect(screen.getByRole('menu')).toBeInTheDocument()
  })

  it('closes the menu on second hamburger click', async () => {
    renderHeader()
    const btn = screen.getByRole('button', { name: 'More options' })
    await userEvent.click(btn)
    await userEvent.click(btn)
    expect(screen.queryByRole('menu')).not.toBeInTheDocument()
  })

  it('menu contains Theme, Settings, and Sign out items', async () => {
    renderHeader()
    await userEvent.click(screen.getByRole('button', { name: 'More options' }))
    expect(screen.getByRole('menuitem', { name: /light mode|dark mode/i })).toBeInTheDocument()
    expect(screen.getByRole('menuitem', { name: /settings/i })).toBeInTheDocument()
    expect(screen.getByRole('menuitem', { name: /sign out/i })).toBeInTheDocument()
  })

  it('does not show Admin menu item when user is not admin', async () => {
    vi.mocked(useCurrentUser).mockReturnValue({ id: '1', username: 'op', displayName: 'Op', role: 'operator' })
    renderHeader()
    await userEvent.click(screen.getByRole('button', { name: 'More options' }))
    expect(screen.queryByRole('menuitem', { name: /admin/i })).not.toBeInTheDocument()
  })

  it('shows Admin menu item when user is admin', async () => {
    vi.mocked(useCurrentUser).mockReturnValue({ id: '1', username: 'a', displayName: 'Admin', role: 'admin' })
    renderHeader()
    await userEvent.click(screen.getByRole('button', { name: 'More options' }))
    expect(screen.getByRole('menuitem', { name: /admin/i })).toBeInTheDocument()
  })

  it('calls logout and closes menu on Sign out click', async () => {
    renderHeader()
    await userEvent.click(screen.getByRole('button', { name: 'More options' }))
    await userEvent.click(screen.getByRole('menuitem', { name: /sign out/i }))
    expect(mockLogout).toHaveBeenCalledOnce()
    await waitFor(() => expect(screen.queryByRole('menu')).not.toBeInTheDocument())
  })

  it('opens settings modal and closes menu on Settings click', async () => {
    renderHeader({ settings: { mode: 'Intercept', port: 8080, upstreamProxy: null } })
    await userEvent.click(screen.getByRole('button', { name: 'More options' }))
    await userEvent.click(screen.getByRole('menuitem', { name: /settings/i }))
    expect(screen.getByTestId('settings-modal')).toBeInTheDocument()
    await waitFor(() => expect(screen.queryByRole('menu')).not.toBeInTheDocument())
  })
})
```

- [ ] **Step 6.2: Run tests to confirm they fail**

```bash
cd frontend && npm test -- Header
```

Expected: failures — hamburger button not found / menu not found.

- [ ] **Step 6.3: Implement changes to `Header.tsx`**

Replace `frontend/src/components/layout/Header.tsx` with:

```tsx
import { useState, useEffect, useRef } from 'react'
import { Link } from 'react-router-dom'
import ProxyStatusBadge from '../proxy/ProxyStatusBadge'
import SettingsModal from '../proxy/SettingsModal'
import { useCurrentUser, useLogout } from '../../hooks/useAuth'
import type { ProxySettings, ProxySettingsUpdate } from '../../types/api'
import type { Theme } from '../../hooks/useTheme'
import '../../styles/header.css'

interface Props {
  isRunning: boolean
  port: number
  settings: ProxySettings | null
  signalRConnected: boolean
  loading: boolean
  theme: Theme
  onStart: () => void
  onStop: () => void
  onSaveSettings: (update: ProxySettingsUpdate) => Promise<ProxySettings | null>
  onToggleTheme: () => void
}

export default function Header({
  isRunning,
  port,
  settings,
  signalRConnected,
  loading,
  theme,
  onStart,
  onStop,
  onSaveSettings,
  onToggleTheme,
}: Props) {
  const logout = useLogout()
  const currentUser = useCurrentUser()
  const [settingsOpen, setSettingsOpen] = useState(false)
  const [menuOpen, setMenuOpen] = useState(false)
  const menuWrapRef = useRef<HTMLDivElement>(null)

  useEffect(() => {
    if (!menuOpen) return
    function handleOutsideClick(e: MouseEvent) {
      if (menuWrapRef.current && !menuWrapRef.current.contains(e.target as Node)) {
        setMenuOpen(false)
      }
    }
    document.addEventListener('mousedown', handleOutsideClick)
    return () => document.removeEventListener('mousedown', handleOutsideClick)
  }, [menuOpen])

  return (
    <>
      <header className="header">
        <a className="header__logo" href="/">
          <div className="header__logo-icon">
            <svg width="16" height="16" viewBox="0 0 16 16" fill="none" aria-hidden="true">
              <circle cx="8" cy="11" r="2" fill="currentColor"/>
              <path d="M5 8.5A4.24 4.24 0 0 1 8 7.5a4.24 4.24 0 0 1 3 1" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round"/>
              <path d="M2 5.5A8.5 8.5 0 0 1 8 4a8.5 8.5 0 0 1 6 1.5" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" opacity="0.6"/>
            </svg>
          </div>
          <span className="header__logo-text">IoTSpy</span>
        </a>

        <ProxyStatusBadge
          isRunning={isRunning}
          port={port}
          signalRConnected={signalRConnected}
        />
        {isRunning && settings?.mode === 'Passive' && (
          <span className="header__passive-badge" title="Proxy is in passive observe-only mode — no manipulation or DB writes">
            Passive Mode
          </span>
        )}

        <div className="header__spacer" />

        <div className="header__actions">
          {isRunning ? (
            <button
              className="header__btn header__btn--stop"
              onClick={onStop}
              disabled={loading}
              title="Stop proxy"
            >
              Stop
            </button>
          ) : (
            <button
              className="header__btn header__btn--start"
              onClick={onStart}
              disabled={loading}
              title="Start proxy"
            >
              Start
            </button>
          )}

          <a
            className="header__btn"
            href="/api/certificates/root-ca/download"
            download
            title="Download root CA certificate"
          >
            <svg width="14" height="14" viewBox="0 0 14 14" fill="none" aria-hidden="true">
              <path d="M7 1v8M4 6l3 3 3-3" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round"/>
              <path d="M2 11h10" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round"/>
            </svg>
            <span className="header__btn-label">Root CA</span>
          </a>

          {currentUser?.role === 'admin' && (
            <Link
              className="header__btn header__btn--icon header__btn--desktop"
              to="/admin"
              title="System administration"
              aria-label="Admin"
            >
              &#x1F527;
            </Link>
          )}

          <button
            className="header__btn header__btn--icon header__btn--desktop"
            onClick={onToggleTheme}
            title={`Switch to ${theme === 'dark' ? 'light' : 'dark'} mode`}
            aria-label="Toggle theme"
          >
            {theme === 'dark' ? '☀' : '☾'}
          </button>

          <button
            className="header__btn header__btn--icon header__btn--desktop"
            onClick={() => setSettingsOpen(true)}
            title="Proxy settings"
            aria-label="Proxy settings"
          >
            &#x2699;
          </button>

          <button
            className="header__btn header__btn--icon header__btn--desktop"
            onClick={logout}
            title="Sign out"
            aria-label="Sign out"
          >
            <svg width="16" height="16" viewBox="0 0 16 16" fill="none" aria-hidden="true">
              <path d="M6 2H3a1 1 0 0 0-1 1v10a1 1 0 0 0 1 1h3" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round"/>
              <path d="M10.5 10.5 13 8l-2.5-2.5M13 8H6" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round"/>
            </svg>
          </button>

          {/* Overflow menu — visible only on mobile (≤640px) */}
          <div className="header__menu-wrap" ref={menuWrapRef}>
            <button
              className="header__btn header__btn--icon header__hamburger"
              onClick={() => setMenuOpen(o => !o)}
              aria-label="More options"
              title="More options"
            >
              <svg width="16" height="16" viewBox="0 0 16 16" fill="none" aria-hidden="true">
                <circle cx="8" cy="3" r="1.5" fill="currentColor"/>
                <circle cx="8" cy="8" r="1.5" fill="currentColor"/>
                <circle cx="8" cy="13" r="1.5" fill="currentColor"/>
              </svg>
            </button>

            {menuOpen && (
              <div className="header__menu" role="menu">
                {currentUser?.role === 'admin' && (
                  <Link
                    className="header__menu-item"
                    to="/admin"
                    role="menuitem"
                    onClick={() => setMenuOpen(false)}
                  >
                    <span aria-hidden="true">&#x1F527;</span> Admin
                  </Link>
                )}
                <button
                  className="header__menu-item"
                  role="menuitem"
                  onClick={() => { onToggleTheme(); setMenuOpen(false) }}
                >
                  <span aria-hidden="true">{theme === 'dark' ? '☀' : '☾'}</span>
                  {theme === 'dark' ? 'Light mode' : 'Dark mode'}
                </button>
                <button
                  className="header__menu-item"
                  role="menuitem"
                  onClick={() => { setSettingsOpen(true); setMenuOpen(false) }}
                >
                  <span aria-hidden="true">&#x2699;</span> Settings
                </button>
                <button
                  className="header__menu-item header__menu-item--danger"
                  role="menuitem"
                  onClick={() => { logout(); setMenuOpen(false) }}
                >
                  <svg width="14" height="14" viewBox="0 0 16 16" fill="none" aria-hidden="true">
                    <path d="M6 2H3a1 1 0 0 0-1 1v10a1 1 0 0 0 1 1h3" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round"/>
                    <path d="M10.5 10.5 13 8l-2.5-2.5M13 8H6" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round"/>
                  </svg>
                  Sign out
                </button>
              </div>
            )}
          </div>
        </div>
      </header>

      {settingsOpen && settings && (
        <SettingsModal
          settings={settings}
          onSave={onSaveSettings}
          onClose={() => setSettingsOpen(false)}
        />
      )}
    </>
  )
}
```

- [ ] **Step 6.4: Run Header tests to confirm they pass**

```bash
cd frontend && npm test -- Header
```

Expected: 8 passed.

- [ ] **Step 6.5: Add CSS to `header.css`**

Append to the end of `frontend/src/styles/header.css`:

```css
/* ── Hamburger button label ──────────────────────────────────────────────────── */
.header__btn-label {
  /* visible on desktop; hidden on mobile via media query below */
}

/* ── Overflow dropdown menu ─────────────────────────────────────────────────── */
.header__menu-wrap {
  position: relative;
}

.header__hamburger {
  display: none; /* shown only at ≤640px */
}

.header__menu {
  position: absolute;
  right: 0;
  top: calc(100% + 4px);
  min-width: 160px;
  background-color: var(--color-surface);
  border: 1px solid var(--color-border);
  border-radius: var(--radius-md);
  box-shadow: var(--shadow-lg);
  z-index: 100;
  overflow: hidden;
  display: flex;
  flex-direction: column;
}

.header__menu-item {
  display: flex;
  align-items: center;
  gap: var(--space-2);
  padding: var(--space-2) var(--space-3);
  font-size: var(--font-size-sm);
  color: var(--color-text);
  background: transparent;
  border: none;
  text-align: left;
  cursor: pointer;
  text-decoration: none;
  white-space: nowrap;
  transition: background-color 0.1s;
}

.header__menu-item:hover {
  background-color: var(--color-surface-2);
  text-decoration: none;
}

.header__menu-item--danger {
  color: var(--color-danger);
}

/* ── Mobile breakpoint ≤640px ────────────────────────────────────────────────── */
@media (max-width: 640px) {
  .header__logo-text        { display: none; }
  .proxy-status-badge__label { display: none; }
  .header__btn-label         { display: none; }
  .header__btn--desktop      { display: none; }
  .header__hamburger         { display: flex; }
}
```

- [ ] **Step 6.6: Run full test suite**

```bash
cd frontend && npm test
```

Expected: all tests pass (71 + 8 new = 79 total).

- [ ] **Step 6.7: Verify visually** — start the dev server, resize to 375px in browser devtools and confirm:
  - Logo shows icon only (no "IoTSpy" text)
  - Status badge shows coloured dot only (no "Proxy: Stopped" text)
  - Root CA shows download icon only (no "Root CA" text)
  - Start/Stop button remains visible with text
  - `⋮` button appears; clicking opens the dropdown with Admin (if admin), Light/Dark mode, Settings, Sign out
  - On desktop (>640px) all original buttons are visible inline and `⋮` is hidden

```bash
cd frontend && npm run dev
```

- [ ] **Step 6.8: Commit**

```bash
cd frontend && git add src/components/layout/Header.tsx src/styles/header.css src/__tests__/Header.test.tsx
git commit -m "feat: hamburger overflow menu + CSS compaction for mobile header (≤640px)"
```

---

## Self-Review Checklist

- **Spec coverage:** 401 callback (Tasks 1–3) ✓; multiUser field (Tasks 2–4) ✓; mobile header compaction (Tasks 5–6) ✓
- **Placeholder scan:** No TBDs, all code blocks complete. ✓
- **Type consistency:** `SET_MULTI_USER` matches across `authStore.ts`, `useAuth.ts`, and test fixtures. `authReducer` exported in Task 2 and imported in its tests. `setOnUnauthorized` exported in Task 1, imported in Task 3. `multiUser: boolean` in `AuthState` used in Tasks 3, 4. `header__btn--desktop` class applied in Task 6 and targeted by CSS in same task. `proxy-status-badge__label` added in Task 5, targeted by CSS in Task 6. ✓
