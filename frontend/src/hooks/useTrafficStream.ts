import { useEffect, useRef, useState, useCallback } from 'react'
import * as signalR from '@microsoft/signalr'
import { getToken } from '../api/client'
import type { TrafficCaptureEvent } from '../types/api'

export type ConnectionState = 'disconnected' | 'connecting' | 'connected' | 'reconnecting'

export interface TrafficFilter {
  deviceId?: string
  host?: string
  method?: string
  statusCode?: number
  protocol?: string
}

interface Options {
  onCapture: (event: TrafficCaptureEvent) => void
}

// Retry forever with exponential backoff, capped at 30 s.
// The default policy retries only 4 times (0 / 2 / 10 / 30 s) then gives up.
const infiniteReconnectPolicy: signalR.IRetryPolicy = {
  nextRetryDelayInMilliseconds(ctx) {
    return Math.min(1_000 * 2 ** ctx.previousRetryCount, 30_000)
  },
}

export function useTrafficStream({ onCapture }: Options) {
  const [connectionState, setConnectionState] = useState<ConnectionState>('disconnected')
  const hubRef = useRef<signalR.HubConnection | null>(null)
  const onCaptureRef = useRef(onCapture)
  const activeFiltersRef = useRef<TrafficFilter>({})
  const retryTimerRef = useRef<ReturnType<typeof setTimeout> | null>(null)
  const stoppedRef = useRef(false)
  onCaptureRef.current = onCapture

  useEffect(() => {
    const token = getToken()
    if (!token) return

    stoppedRef.current = false

    const connection = new signalR.HubConnectionBuilder()
      .withUrl(`/hubs/traffic?access_token=${encodeURIComponent(token)}`)
      .withAutomaticReconnect(infiniteReconnectPolicy)
      .configureLogging(signalR.LogLevel.Warning)
      .build()

    hubRef.current = connection

    connection.onreconnecting(() => setConnectionState('reconnecting'))
    connection.onreconnected(() => {
      setConnectionState('connected')
      void resubscribeAll(connection, activeFiltersRef.current)
    })
    connection.onclose(() => {
      // onclose fires when the connection is deliberately stopped or after all
      // reconnect attempts fail. With infiniteReconnectPolicy the latter never
      // happens during normal operation, so this only fires on explicit stop().
      setConnectionState('disconnected')
    })

    connection.on('TrafficCapture', (event: TrafficCaptureEvent) => {
      onCaptureRef.current(event)
    })

    // Attempt the initial connection, retrying with backoff if the server is
    // not yet reachable (e.g. backend restarting). withAutomaticReconnect only
    // handles drops after a successful connect — it does not retry start().
    let attempt = 0
    function tryStart() {
      if (stoppedRef.current) return
      setConnectionState('connecting')
      connection.start()
        .then(() => setConnectionState('connected'))
        .catch(() => {
          if (stoppedRef.current) return
          setConnectionState('disconnected')
          const delay = Math.min(1_000 * 2 ** attempt, 30_000)
          attempt++
          retryTimerRef.current = setTimeout(tryStart, delay)
        })
    }
    tryStart()

    return () => {
      stoppedRef.current = true
      if (retryTimerRef.current !== null) clearTimeout(retryTimerRef.current)
      void connection.stop()
    }
  }, []) // only once on mount

  const subscribe = useCallback(async (filter: TrafficFilter) => {
    const connection = hubRef.current
    if (!connection || connection.state !== signalR.HubConnectionState.Connected) return

    const prev = activeFiltersRef.current

    // Unsubscribe from old filters that changed
    if (prev.deviceId && prev.deviceId !== filter.deviceId)
      await connection.invoke('UnsubscribeFromDevice', prev.deviceId)
    if (prev.host && prev.host !== filter.host)
      await connection.invoke('UnsubscribeFromHost', prev.host)
    if (prev.method && prev.method !== filter.method)
      await connection.invoke('UnsubscribeFromMethod', prev.method)
    if (prev.statusCode && prev.statusCode !== filter.statusCode)
      await connection.invoke('UnsubscribeFromStatusCode', prev.statusCode)
    if (prev.protocol && prev.protocol !== filter.protocol)
      await connection.invoke('UnsubscribeFromProtocol', prev.protocol)

    // Subscribe to new filters
    if (filter.deviceId)
      await connection.invoke('SubscribeToDevice', filter.deviceId)
    if (filter.host)
      await connection.invoke('SubscribeToHost', filter.host)
    if (filter.method)
      await connection.invoke('SubscribeToMethod', filter.method)
    if (filter.statusCode)
      await connection.invoke('SubscribeToStatusCode', filter.statusCode)
    if (filter.protocol)
      await connection.invoke('SubscribeToProtocol', filter.protocol)

    activeFiltersRef.current = { ...filter }
  }, [])

  const unsubscribeAll = useCallback(async () => {
    const connection = hubRef.current
    if (!connection || connection.state !== signalR.HubConnectionState.Connected) return

    const prev = activeFiltersRef.current
    if (prev.deviceId) await connection.invoke('UnsubscribeFromDevice', prev.deviceId)
    if (prev.host) await connection.invoke('UnsubscribeFromHost', prev.host)
    if (prev.method) await connection.invoke('UnsubscribeFromMethod', prev.method)
    if (prev.statusCode) await connection.invoke('UnsubscribeFromStatusCode', prev.statusCode)
    if (prev.protocol) await connection.invoke('UnsubscribeFromProtocol', prev.protocol)

    activeFiltersRef.current = {}
  }, [])

  return { connectionState, subscribe, unsubscribeAll }
}

async function resubscribeAll(connection: signalR.HubConnection, filter: TrafficFilter) {
  try {
    if (filter.deviceId) await connection.invoke('SubscribeToDevice', filter.deviceId)
    if (filter.host) await connection.invoke('SubscribeToHost', filter.host)
    if (filter.method) await connection.invoke('SubscribeToMethod', filter.method)
    if (filter.statusCode) await connection.invoke('SubscribeToStatusCode', filter.statusCode)
    if (filter.protocol) await connection.invoke('SubscribeToProtocol', filter.protocol)
  } catch {
    // Best-effort re-subscribe; connection may drop again
  }
}
