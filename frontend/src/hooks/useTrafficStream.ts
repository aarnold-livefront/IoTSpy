import { useEffect, useRef, useState } from 'react'
import * as signalR from '@microsoft/signalr'
import { getToken } from '../api/client'
import type { TrafficCaptureEvent } from '../types/api'

export type ConnectionState = 'disconnected' | 'connecting' | 'connected' | 'reconnecting'

interface Options {
  onCapture: (event: TrafficCaptureEvent) => void
}

export function useTrafficStream({ onCapture }: Options) {
  const [connectionState, setConnectionState] = useState<ConnectionState>('disconnected')
  const hubRef = useRef<signalR.HubConnection | null>(null)
  const onCaptureRef = useRef(onCapture)
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
    connection.onreconnected(() => setConnectionState('connected'))
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

  return { connectionState }
}
