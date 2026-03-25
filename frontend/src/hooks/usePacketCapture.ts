import { useState, useEffect, useCallback, useRef } from 'react'
import * as signalR from '@microsoft/signalr'
import { getToken, apiFetch } from '../api/client'
import type { CapturedPacket } from '../types/api'

export interface CaptureDevice {
  id: string
  name: string
  displayName: string
  ipAddress: string
  macAddress: string
}

export function usePacketCapture() {
  const [devices, setDevices] = useState<CaptureDevice[]>([])
  const [packets, setPackets] = useState<CapturedPacket[]>([])
  const [isCapturing, setIsCapturing] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const hubRef = useRef<signalR.HubConnection | null>(null)

  // Load devices from API
  useEffect(() => {
    loadDevices()
  }, [])

  const loadDevices = async () => {
    try {
      const data = await apiFetch<CaptureDevice[]>('/api/packet-capture/devices')
      setDevices(data)
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Unknown error')
    }
  }

  // Connect to SignalR hub for live packets
  useEffect(() => {
    const token = getToken()
    if (!token) return

    const connection = new signalR.HubConnectionBuilder()
      .withUrl(`/hubs/packets?access_token=${encodeURIComponent(token)}`)
      .withAutomaticReconnect()
      .configureLogging(signalR.LogLevel.Warning)
      .build()

    hubRef.current = connection

    connection.on('PacketCaptured', (packet: CapturedPacket) => {
      setPackets(prev => [packet, ...prev].slice(0, 10000))
    })

    connection.on('CaptureStatus', (data: { isCapturing: boolean }) => {
      setIsCapturing(data.isCapturing)
    })

    connection
      .start()
      .catch(err => console.warn('Packet capture hub connection failed:', err))

    return () => {
      void connection.stop()
    }
  }, [])

  const startCapture = useCallback(async (deviceId: string) => {
    try {
      setError(null)
      await apiFetch<void>('/api/packet-capture/start', {
        method: 'POST',
        body: JSON.stringify({ deviceId }),
      })
      setIsCapturing(true)
      setPackets([])
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Unknown error')
    }
  }, [])

  const stopCapture = useCallback(async () => {
    try {
      await apiFetch<void>('/api/packet-capture/stop', { method: 'POST' })
      setIsCapturing(false)
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Unknown error')
    }
  }, [])

  const clearPackets = useCallback(() => {
    setPackets([])
  }, [])

  return {
    devices,
    packets,
    isCapturing,
    startCapture,
    stopCapture,
    clearPackets,
    error
  }
}
