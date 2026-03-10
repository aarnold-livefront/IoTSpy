import { useState, useEffect, useCallback } from 'react'
import type { NetworkDevice, CapturedPacket } from '../types/api'
import { toast } from 'react-toastify'

export function usePacketCapture() {
  const [devices, setDevices] = useState<NetworkDevice[]>([])
  const [packets, setPackets] = useState<CapturedPacket[]>([])
  const [isCapturing, setIsCapturing] = useState(false)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    loadDevices()
  }, [])

  const loadDevices = async () => {
    try {
      const response = await fetch('/api/packet-capture/devices')
      if (!response.ok) throw new Error('Failed to load devices')
      const data = await response.json()
      setDevices(data.map((d: any) => ({
        name: d.interfaceName,
        displayName: d.displayName,
        ipAddresses: d.ipAddresses,
        macAddress: d.macAddress,
        isLoopback: d.isLoopback,
        isUp: d.isUp,
        isRunning: d.isRunning
      })))
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Unknown error')
    }
  }

  useEffect(() => {
    const wsUrl = `ws://${window.location.host}/packet-capture`
    const ws = new WebSocket(wsUrl)

    ws.onmessage = (event: MessageEvent) => {
      try {
        const message = JSON.parse(event.data)
        if (message.type === 'PACKET_CAPTURED') {
          setPackets(prev => [message.packet as CapturedPacket, ...prev].slice(0, 10000))
        } else if (message.type === 'CAPTURE_STATUS') {
          setIsCapturing(message.isCapturing)
        } else if (message.type === 'DEVICE_UPDATE') {
          setDevices(prev => prev.map(d => 
            d.name === message.deviceInterface
              ? { ...d, isUp: message.isOnline }
              : d
          ))
        }
      } catch {
        // Ignore invalid JSON
      }
    }

    ws.onerror = () => {
      console.error('WebSocket error on packet-capture endpoint')
    }

    return () => ws.close()
  }, [])

  const startCapture = useCallback(async (interfaceName: string, captureFilter: string) => {
    try {
      setError(null)
      await fetch('/api/packet-capture/start', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json'
        },
        body: JSON.stringify({ interfaceName, captureFilter })
      })
      toast.success('Packet capture started')
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Unknown error')
      toast.error('Failed to start packet capture')
    }
  }, [])

  const stopCapture = useCallback(async () => {
    try {
      await fetch('/api/packet-capture/stop', { method: 'POST' })
      toast.success('Packet capture stopped')
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Unknown error')
      toast.error('Failed to stop packet capture')
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
