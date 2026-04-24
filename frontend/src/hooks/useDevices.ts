import { useCallback } from 'react'
import { useQuery } from '@tanstack/react-query'
import { listDevices } from '../api/devices'

export function useDevices() {
  const { data: devices = [], isLoading: loading, error: queryError, refetch } = useQuery({
    queryKey: ['devices'],
    queryFn: listDevices,
    staleTime: 30_000,
  })

  const refresh = useCallback(() => { void refetch() }, [refetch])

  return {
    devices,
    loading,
    error: queryError instanceof Error ? queryError.message : null,
    refresh,
  }
}
