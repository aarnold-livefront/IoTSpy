import { useEffect, useRef, useState } from 'react'
import type { ConnectionState } from './useTrafficStream'

export type BackendStatus = 'unknown' | 'up' | 'down' | 'reconnecting'

export function useBackendHealth(connectionState: ConnectionState): BackendStatus {
  const hasConnectedRef = useRef(false)
  const [status, setStatus] = useState<BackendStatus>('unknown')

  useEffect(() => {
    if (connectionState === 'connected') {
      hasConnectedRef.current = true
      setStatus('up')
    } else if (connectionState === 'reconnecting') {
      if (hasConnectedRef.current) setStatus('reconnecting')
    } else if (connectionState === 'disconnected') {
      if (hasConnectedRef.current) setStatus('down')
    }
    // 'connecting' on first load → stay 'unknown'
  }, [connectionState])

  return status
}
