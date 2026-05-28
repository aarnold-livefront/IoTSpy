import { useEffect, useRef, useState } from 'react'
import type { ConnectionState } from './useTrafficStream'

export type BackendStatus = 'unknown' | 'up' | 'down' | 'reconnecting'

// Poll /api/auth/status (no auth required) to detect server availability
// independently of the SignalR connection. This ensures the banner appears
// even when the server is unreachable on initial load.
const HEALTH_URL = '/api/auth/status'
const POLL_DOWN_MS = 5_000   // fast polling while server is down
const POLL_UP_MS   = 30_000  // slow polling while server is up

async function checkHealth(): Promise<boolean> {
  try {
    const res = await fetch(HEALTH_URL, { method: 'HEAD' })
    return res.ok || res.status < 500
  } catch {
    return false
  }
}

export function useBackendHealth(connectionState: ConnectionState): BackendStatus {
  const [status, setStatus] = useState<BackendStatus>('unknown')
  const timerRef = useRef<ReturnType<typeof setTimeout> | null>(null)
  const mountedRef = useRef(true)

  // Drive status from SignalR first — it's the authoritative real-time signal.
  useEffect(() => {
    if (connectionState === 'connected') {
      setStatus('up')
    } else if (connectionState === 'reconnecting') {
      setStatus('reconnecting')
    }
    // 'disconnected' alone doesn't flip to 'down' — the HTTP poll decides that,
    // so we don't flicker to 'down' during brief connection gaps.
  }, [connectionState])

  // HTTP health poll — independent of SignalR.
  // Covers: initial load with server down, and SignalR giving up after disconnect.
  useEffect(() => {
    mountedRef.current = true

    function schedule(delayMs: number) {
      timerRef.current = setTimeout(async () => {
        if (!mountedRef.current) return
        const alive = await checkHealth()
        if (!mountedRef.current) return
        if (alive) {
          setStatus(prev => prev === 'unknown' || prev === 'down' ? 'up' : prev)
          schedule(POLL_UP_MS)
        } else {
          setStatus(prev => prev !== 'reconnecting' ? 'down' : prev)
          schedule(POLL_DOWN_MS)
        }
      }, delayMs)
    }

    // First check: slightly delayed so the SignalR path can set 'up' first
    // if the server is reachable — avoids a redundant HTTP call on happy path.
    schedule(2_000)

    return () => {
      mountedRef.current = false
      if (timerRef.current !== null) clearTimeout(timerRef.current)
    }
  }, [])

  return status
}
