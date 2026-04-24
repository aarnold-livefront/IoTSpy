import { useState, useCallback } from 'react'
import { useQueryClient } from '@tanstack/react-query'
import type {
  FreezeFrameDto,
  ProtocolDistributionDto,
  CommunicationPatternDto,
  SuspiciousActivityDto,
} from '../types/api'
import {
  getProtocolDistribution,
  getCommunicationPatterns,
  getSuspiciousActivity,
  createFreezeFrame,
  getFreezeFrame,
  freezeAnalysis,
  unfreezeAnalysis,
  getFreezeStatus,
} from '../api/packetCapture'

export function usePacketAnalysis() {
  const queryClient = useQueryClient()
  const [protocolDistribution, setProtocolDistribution] = useState<ProtocolDistributionDto | null>(null)
  const [patterns, setPatterns] = useState<CommunicationPatternDto[]>([])
  const [suspicious, setSuspicious] = useState<SuspiciousActivityDto[]>([])
  const [freezeFrame, setFreezeFrame] = useState<FreezeFrameDto | null>(null)
  const [isFrozen, setIsFrozen] = useState(false)
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const loadProtocols = useCallback(async () => {
    try {
      setLoading(true)
      setError(null)
      const data = await queryClient.fetchQuery({
        queryKey: ['packet-analysis-protocols'],
        queryFn: getProtocolDistribution,
        staleTime: 10_000,
      })
      setProtocolDistribution(data)
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to load protocol distribution')
    } finally {
      setLoading(false)
    }
  }, [queryClient])

  const loadPatterns = useCallback(async (topN = 10) => {
    try {
      setLoading(true)
      setError(null)
      const data = await queryClient.fetchQuery({
        queryKey: ['packet-analysis-patterns', topN],
        queryFn: () => getCommunicationPatterns(topN),
        staleTime: 10_000,
      })
      setPatterns(data)
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to load communication patterns')
    } finally {
      setLoading(false)
    }
  }, [queryClient])

  const loadSuspicious = useCallback(async () => {
    try {
      setLoading(true)
      setError(null)
      const data = await queryClient.fetchQuery({
        queryKey: ['packet-analysis-suspicious'],
        queryFn: getSuspiciousActivity,
        staleTime: 10_000,
      })
      setSuspicious(data)
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to load suspicious activity')
    } finally {
      setLoading(false)
    }
  }, [queryClient])

  const loadAll = useCallback(async () => {
    setLoading(true)
    setError(null)
    try {
      const [proto, pat, sus] = await Promise.all([
        queryClient.fetchQuery({
          queryKey: ['packet-analysis-protocols'],
          queryFn: getProtocolDistribution,
          staleTime: 10_000,
        }),
        queryClient.fetchQuery({
          queryKey: ['packet-analysis-patterns', 10],
          queryFn: () => getCommunicationPatterns(),
          staleTime: 10_000,
        }),
        queryClient.fetchQuery({
          queryKey: ['packet-analysis-suspicious'],
          queryFn: getSuspiciousActivity,
          staleTime: 10_000,
        }),
      ])
      setProtocolDistribution(proto)
      setPatterns(pat)
      setSuspicious(sus)
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to load analysis data')
    } finally {
      setLoading(false)
    }
  }, [queryClient])

  const loadFreezeFrame = useCallback(async (packetId: string) => {
    try {
      setError(null)
      let frame: FreezeFrameDto
      try {
        frame = await getFreezeFrame(packetId)
      } catch {
        frame = await createFreezeFrame(packetId)
      }
      setFreezeFrame(frame)
      return frame
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to load freeze frame')
      return null
    }
  }, [])

  const toggleFreeze = useCallback(async () => {
    try {
      if (isFrozen) {
        await unfreezeAnalysis()
        setIsFrozen(false)
      } else {
        await freezeAnalysis()
        setIsFrozen(true)
      }
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to toggle freeze')
    }
  }, [isFrozen])

  const checkFreezeStatus = useCallback(async () => {
    try {
      const status = await getFreezeStatus()
      setIsFrozen(status.isFrozen)
    } catch {
      // Best-effort status check
    }
  }, [])

  return {
    protocolDistribution,
    patterns,
    suspicious,
    freezeFrame,
    isFrozen,
    loading,
    error,
    loadProtocols,
    loadPatterns,
    loadSuspicious,
    loadAll,
    loadFreezeFrame,
    toggleFreeze,
    checkFreezeStatus,
    clearFreezeFrame: () => setFreezeFrame(null),
  }
}
