import { useCallback, useEffect, useRef, useState } from 'react'
import {
  listScanJobs,
  startScan,
  getScanJob,
  cancelScanJob,
  deleteScanJob,
} from '../api/scanner'
import type { ScanJob, StartScanRequest } from '../types/api'

export function useScanner() {
  const [jobs, setJobs] = useState<ScanJob[]>([])
  const [selectedJob, setSelectedJob] = useState<ScanJob | null>(null)
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const pollRef = useRef<ReturnType<typeof setInterval> | null>(null)

  const refresh = useCallback(async () => {
    try {
      const data = await listScanJobs()
      setJobs(data)
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to fetch scan jobs')
    }
  }, [])

  useEffect(() => {
    setLoading(true)
    void refresh().finally(() => setLoading(false))
  }, [refresh])

  // Poll for running jobs
  useEffect(() => {
    const hasRunning = jobs.some((j) => j.status === 'Running' || j.status === 'Pending')
    if (hasRunning) {
      pollRef.current = setInterval(() => void refresh(), 3000)
    } else if (pollRef.current) {
      clearInterval(pollRef.current)
      pollRef.current = null
    }
    return () => {
      if (pollRef.current) clearInterval(pollRef.current)
    }
  }, [jobs, refresh])

  const scan = useCallback(async (request: StartScanRequest) => {
    setError(null)
    try {
      const job = await startScan(request)
      setJobs((prev) => [job, ...prev])
      return job
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to start scan')
      return null
    }
  }, [])

  const selectJob = useCallback(async (id: string) => {
    setError(null)
    try {
      const job = await getScanJob(id)
      setSelectedJob(job)
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to fetch scan job')
    }
  }, [])

  const cancel = useCallback(async (id: string) => {
    setError(null)
    try {
      await cancelScanJob(id)
      void refresh()
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to cancel scan')
    }
  }, [refresh])

  const remove = useCallback(async (id: string) => {
    setError(null)
    try {
      await deleteScanJob(id)
      setJobs((prev) => prev.filter((j) => j.id !== id))
      if (selectedJob?.id === id) setSelectedJob(null)
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to delete scan job')
    }
  }, [selectedJob])

  return {
    jobs,
    selectedJob,
    loading,
    error,
    refresh,
    scan,
    selectJob,
    cancel,
    remove,
  }
}
