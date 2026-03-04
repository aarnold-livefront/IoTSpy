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

export function useTrafficStream({ onCapture }: Options) {
  const [connectionState, setConnectionState] = useState<ConnectionState>('disconnected')
  const hubRef = useRef<signalR.HubConnection | null>(null)
  const onCaptureRef = useRef(onCapture)
  const activeFiltersRef = useRef<TrafficFilter>({})
  onCaptureRef.current = onCapture

  useEffect(() => {
    const token = getToken()
    if (!token) return

    const connection = new signalR.HubConnectionBuilder()
      .withUrl(`/hubs/traffic?access_token=${encodeURIComponent(token)}`)
      .withAutomaticReconnect()
      .configureLogging(signalR.LogLevel.Warning)
      .build()

    hubRef.current = connection

    connection.onreconnecting(() => setConnectionState('reconnecting'))
    connection.onreconnected(() => {
      setConnectionState('connected')
      // Re-subscribe to active filters after reconnect
      void resubscribeAll(connection, activeFiltersRef.current)
    })
    connection.onclose(() => setConnectionState('disconnected'))

    connection.on('TrafficCapture', (event: TrafficCaptureEvent) => {
      onCaptureRef.current(event)
    })

    setConnectionState('connecting')
    connection
      .start()
      .then(() => setConnectionState('connected'))
      .catch(() => setConnectionState('disconnected'))

    return () => {
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
