import { useCallback, useEffect, useRef, useState } from 'react'
import * as signalR from '@microsoft/signalr'
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
  const [sessions, setSessions] = useState<InvestigationSession[]>([])
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const load = useCallback(async (includeInactive = false) => {
    setLoading(true)
    setError(null)
    try {
      setSessions(await listSessions(includeInactive))
    } catch {
      setError('Failed to load sessions')
    } finally {
      setLoading(false)
    }
  }, [])

  useEffect(() => { void load() }, [load])

  return { sessions, loading, error, reload: load }
}

export function useSessionDetail(sessionId: string | null) {
  const [session, setSession] = useState<InvestigationSession | null>(null)
  const [captures, setCaptures] = useState<SessionCapture[]>([])
  const [annotations, setAnnotations] = useState<CaptureAnnotation[]>([])
  const [activity, setActivity] = useState<SessionActivity[]>([])
  const [presence, setPresence] = useState<PresenceEntry[]>([])
  const [loading, setLoading] = useState(false)

  const connRef = useRef<signalR.HubConnection | null>(null)

  const loadData = useCallback(async (id: string) => {
    setLoading(true)
    try {
      const [s, caps, anns, acts] = await Promise.all([
        getSession(id),
        getSessionCaptures(id),
        getAnnotations(id),
        getActivity(id),
      ])
      setSession(s)
      setCaptures(caps)
      setAnnotations(anns)
      setActivity(acts)
    } catch {
      // ignore; caller can handle
    } finally {
      setLoading(false)
    }
  }, [])

  useEffect(() => {
    if (!sessionId) {
      setSession(null)
      setCaptures([])
      setAnnotations([])
      setActivity([])
      setPresence([])
      return
    }

    void loadData(sessionId)

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

    connRef.current = conn

    return () => {
      conn.invoke('LeaveSession', sessionId).catch(() => {})
      void conn.stop()
      connRef.current = null
    }
  }, [sessionId, loadData])

  return {
    session,
    captures,
    annotations,
    activity,
    presence,
    loading,
    reload: sessionId ? () => loadData(sessionId) : () => {},
    setAnnotations,
    setActivity,
  }
}
