import { useEffect } from 'react'
import { useNavigate } from 'react-router-dom'
import { getToken } from '../api/client'
import { getAuthStatus, login as apiLogin, setup as apiSetup } from '../api/auth'
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

  useEffect(() => {
    let cancelled = false
    async function init() {
      try {
        const { passwordSet } = await getAuthStatus()
        if (cancelled) return
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
        navigate('/', { replace: true })
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
  }, [state.status, dispatch, navigate])

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
