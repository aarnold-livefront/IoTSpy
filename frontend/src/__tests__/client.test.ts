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
    setOnUnauthorized(null)
    clearToken()
    localStorage.clear()
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

    await expect(apiFetch('/api/test')).rejects.toMatchObject({ status: 403 })
    expect(cb).not.toHaveBeenCalled()
  })

  it('does not throw if no callback is registered on 401', async () => {
    mockFetchResponse(401, { error: 'Unauthorized' })
    setOnUnauthorized(null)

    await expect(apiFetch('/api/test')).rejects.toMatchObject({ status: 401 })
  })
})
