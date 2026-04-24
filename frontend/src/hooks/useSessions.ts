import { useCallback, useEffect, useRef, useState } from 'react'
import * as signalR from '@microsoft/signalr'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { getToken } from '../api/client'
import {
  listSessions,
  getSession,
  getSessionCaptures,
  getAnnotations,
  getActivity,
} from '../api/sessions'
import type {
  InvestigationSession,
  SessionCapture,
  CaptureAnnotation,
  SessionActivity,
  PresenceEntry,
} from '../types/sessions'

export function useSessions() {
  const [includeInactive, setIncludeInactive] = useState(false)

  const { data: sessions = [], isLoading: loading, error: queryError, refetch } = useQuery({
    queryKey: ['sessions', includeInactive],
    queryFn: () => listSessions(includeInactive),
  })

  const reload = useCallback(
    (incl = false) => {
      setIncludeInactive(incl)
      void refetch()
    },
    [refetch],
  )

  return {
    sessions,
    loading,
    error: queryError instanceof Error ? queryError.message : null,
    reload,
  }
}

export function useSessionDetail(sessionId: string | null) {
  const queryClient = useQueryClient()
  const [session, setSession] = useState<InvestigationSession | null>(null)
  const [captures, setCaptures] = useState<SessionCapture[]>([])
  const [annotations, setAnnotations] = useState<CaptureAnnotation[]>([])
  const [activity, setActivity] = useState<SessionActivity[]>([])
  const [presence, setPresence] = useState<PresenceEntry[]>([])
  const [loading, setLoading] = useState(false)

  const connRef = useRef<signalR.HubConnection | null>(null)

  // Fetch all session detail data when sessionId is set
  const { data: detailData } = useQuery({
    queryKey: ['session-detail', sessionId],
    queryFn: () =>
      Promise.all([
        getSession(sessionId!),
        getSessionCaptures(sessionId!),
        getAnnotations(sessionId!),
        getActivity(sessionId!),
      ]),
    enabled: !!sessionId,
    staleTime: 0,
  })

  // Sync query data into local state (which SignalR can also update)
  useEffect(() => {
    if (detailData) {
      const [s, caps, anns, acts] = detailData
      setSession(s)
      setCaptures(caps)
      setAnnotations(anns)
      setActivity(acts)
    }
  }, [detailData])

  // Clear state when no session selected
  useEffect(() => {
    if (!sessionId) {
      setSession(null)
      setCaptures([])
      setAnnotations([])
      setActivity([])
      setPresence([])
      return
    }

    setLoading(true)

    // Connect SignalR
    const token = getToken()
    const conn = new signalR.HubConnectionBuilder()
      .withUrl('/hubs/collaboration', token ? { accessTokenFactory: () => token } : {})
      .withAutomaticReconnect()
      .build()

    conn.on('AnnotationAdded', (ann: CaptureAnnotation) => {
      setAnnotations(prev => [ann, ...prev])
    })
    conn.on('AnnotationUpdated', (ann: CaptureAnnotation) => {
      setAnnotations(prev => prev.map(a => a.id === ann.id ? ann : a))
    })
    conn.on('AnnotationDeleted', (data: { id: string }) => {
      setAnnotations(prev => prev.filter(a => a.id !== data.id))
    })
    conn.on('SessionActivity', (act: SessionActivity) => {
      setActivity(prev => [act, ...prev.slice(0, 99)])
    })
    conn.on('PresenceUpdated', (list: PresenceEntry[]) => {
      setPresence(list)
    })

    conn.start()
      .then(() => conn.invoke('JoinSession', sessionId))
      .catch(() => { /* SignalR unavailable */ })
      .finally(() => setLoading(false))

    connRef.current = conn

    return () => {
      conn.invoke('LeaveSession', sessionId).catch(() => {})
      void conn.stop()
      connRef.current = null
    }
  }, [sessionId])

  const reload = useCallback(() => {
    if (sessionId) {
      void queryClient.invalidateQueries({ queryKey: ['session-detail', sessionId] })
    }
  }, [queryClient, sessionId])

  return {
    session,
    captures,
    annotations,
    activity,
    presence,
    loading,
    reload,
    setAnnotations,
    setActivity,
  }
}
