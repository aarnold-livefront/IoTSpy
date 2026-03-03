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
}

const initialState: AuthState = {
  status: 'unknown',
  token: null,
}

// ── Actions ───────────────────────────────────────────────────────────────────

type AuthAction =
  | { type: 'SET_NO_PASSWORD' }
  | { type: 'SET_UNAUTHENTICATED' }
  | { type: 'SET_AUTHENTICATED'; token: string }
  | { type: 'LOGOUT' }

function reducer(state: AuthState, action: AuthAction): AuthState {
  switch (action.type) {
    case 'SET_NO_PASSWORD':
      return { status: 'no-password', token: null }
    case 'SET_UNAUTHENTICATED':
      return { status: 'unauthenticated', token: null }
    case 'SET_AUTHENTICATED':
      return { status: 'authenticated', token: action.token }
    case 'LOGOUT':
      clearToken()
      return { status: 'unauthenticated', token: null }
    default:
      return state
  }
}

// ── Context ───────────────────────────────────────────────────────────────────

const AuthStateCtx = createContext<AuthState>(initialState)
const AuthDispatchCtx = createContext<Dispatch<AuthAction>>(() => undefined)

import { createElement } from 'react'

export function AuthProvider({ children }: { children: ReactNode }) {
  const [state, dispatch] = useReducer(reducer, initialState)
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
