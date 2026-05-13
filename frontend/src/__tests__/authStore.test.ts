import { describe, it, expect } from 'vitest'
import { authReducer, initialState } from '../store/authStore'
import type { AuthState } from '../store/authStore'

const base: AuthState = { status: 'unauthenticated', token: null, multiUser: false }

describe('authReducer — SET_MULTI_USER', () => {
  it('sets multiUser to true', () => {
    const next = authReducer(base, { type: 'SET_MULTI_USER', value: true })
    expect(next.multiUser).toBe(true)
    expect(next.status).toBe('unauthenticated')
  })

  it('sets multiUser to false', () => {
    const state: AuthState = { ...base, multiUser: true }
    const next = authReducer(state, { type: 'SET_MULTI_USER', value: false })
    expect(next.multiUser).toBe(false)
  })

  it('initial multiUser is false', () => {
    expect(initialState.multiUser).toBe(false)
  })
})
