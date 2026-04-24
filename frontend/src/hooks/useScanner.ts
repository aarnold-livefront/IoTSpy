import { useCallback, useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import {
  listScanJobs,
  startScan,
  getScanJob,
  cancelScanJob,
  deleteScanJob,
} from '../api/scanner'
import type { ScanJob, StartScanRequest } from '../types/api'

const QUERY_KEY = ['scan-jobs']

export function useScanner() {
  const queryClient = useQueryClient()
  const [selectedJob, setSelectedJob] = useState<ScanJob | null>(null)
  const [error, setError] = useState<string | null>(null)

  const { data: jobs = [], isLoading: loading } = useQuery({
    queryKey: QUERY_KEY,
    queryFn: listScanJobs,
    staleTime: 0,
    refetchInterval: (query) => {
      const data = query.state.data as ScanJob[] | undefined
      const hasRunning = Array.isArray(data) &&
        data.some((j) => j.status === 'Running' || j.status === 'Pending')
      return hasRunning ? 3000 : false
    },
  })

  const refresh = useCallback(() => {
    void queryClient.invalidateQueries({ queryKey: QUERY_KEY })
  }, [queryClient])

  const scanMutation = useMutation({
    mutationFn: (req: StartScanRequest) => startScan(req),
    onSuccess: (job) => {
      queryClient.setQueryData<ScanJob[]>(QUERY_KEY, (prev = []) => [job, ...prev])
    },
  })

  const scan = useCallback(
    async (request: StartScanRequest) => {
      setError(null)
      try {
        return await scanMutation.mutateAsync(request)
      } catch (err) {
        setError(err instanceof Error ? err.message : 'Failed to start scan')
        return null
      }
    },
    [scanMutation],
  )

  const selectJob = useCallback(async (id: string) => {
    setError(null)
    try {
      const job = await getScanJob(id)
      setSelectedJob(job)
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to fetch scan job')
    }
  }, [])

  const cancelMutation = useMutation({
    mutationFn: (id: string) => cancelScanJob(id),
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: QUERY_KEY }),
  })

  const cancel = useCallback(
    async (id: string) => {
      setError(null)
      try {
        await cancelMutation.mutateAsync(id)
      } catch (err) {
        setError(err instanceof Error ? err.message : 'Failed to cancel scan')
      }
    },
    [cancelMutation],
  )

  const removeMutation = useMutation({
    mutationFn: (id: string) => deleteScanJob(id),
    onSuccess: (_data, id) => {
      queryClient.setQueryData<ScanJob[]>(QUERY_KEY, (prev = []) =>
        prev.filter((j) => j.id !== id),
      )
      setSelectedJob((prev) => (prev?.id === id ? null : prev))
    },
  })

  const remove = useCallback(
    async (id: string) => {
      setError(null)
      try {
        await removeMutation.mutateAsync(id)
      } catch (err) {
        setError(err instanceof Error ? err.message : 'Failed to delete scan job')
      }
    },
    [removeMutation],
  )

  return { jobs, selectedJob, loading, error, refresh, scan, selectJob, cancel, remove }
}
