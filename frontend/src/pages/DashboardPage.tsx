import { useEffect, useState } from 'react'
import AppShell from '../components/layout/AppShell'
import Header from '../components/layout/Header'
import SplitPane from '../components/layout/SplitPane'
import CaptureList from '../components/captures/CaptureList'
import CaptureDetail from '../components/captures/CaptureDetail'
import TimelineSwimlaneView from '../components/timeline/TimelineSwimlaneView'
import PanelPacketCapture from '../components/panels/PanelPacketCapture'
import ManipulationPanel from '../components/manipulation/ManipulationPanel'
import SessionsPanel from '../components/sessions/SessionsPanel'
import ErrorBoundary from '../components/common/ErrorBoundary'
import { useProxy } from '../hooks/useProxy'
import { useCaptures } from '../hooks/useCaptures'
import { useDevices } from '../hooks/useDevices'
import { useTrafficStream } from '../hooks/useTrafficStream'
import { useTheme } from '../hooks/useTheme'
import type { CaptureFilters } from '../types/api'

type ViewMode = 'list' | 'timeline' | 'packet-capture' | 'manipulation' | 'sessions'

function useIsMobile() {
  const [isMobile, setIsMobile] = useState(() => window.matchMedia('(max-width: 768px)').matches)
  useEffect(() => {
    const mq = window.matchMedia('(max-width: 768px)')
    const handler = (e: MediaQueryListEvent) => setIsMobile(e.matches)
    mq.addEventListener('change', handler)
    return () => mq.removeEventListener('change', handler)
  }, [])
  return isMobile
}

export default function DashboardPage() {
  const [selectedId, setSelectedId] = useState<string | null>(null)
  const isMobile = useIsMobile()
  const [filters, setFilters] = useState<CaptureFilters>({ page: 1, pageSize: 50 })
  const [viewMode, setViewMode] = useState<ViewMode>('list')
  const { theme, toggle: toggleTheme } = useTheme()

  const proxy = useProxy()
  const { devices } = useDevices()
  const {
    captures,
    total,
    loading,
    loadingMore,
    error,
    hasMore,
    loadMore,
    prependCapture,
  } = useCaptures(filters)

  const { connectionState } = useTrafficStream({ onCapture: prependCapture })

  const proxyStatus = proxy.status
  const isRunning = proxyStatus?.isRunning ?? false
  const port = proxyStatus?.port ?? proxyStatus?.settings?.proxyPort ?? 8888

  return (
    <AppShell
      header={
        <Header
          isRunning={isRunning}
          port={port}
          settings={proxyStatus?.settings ?? null}
          signalRConnected={connectionState === 'connected'}
          loading={proxy.loading}
          theme={theme}
          onStart={proxy.start}
          onStop={proxy.stop}
          onSaveSettings={proxy.saveSettings}
          onToggleTheme={toggleTheme}
        />
      }
    >
      {/* View mode toggle */}
      <div className="view-toggle">
        {(['list', 'timeline', 'packet-capture', 'manipulation', 'sessions'] as const).map(mode => (
          <button
            key={mode}
            className={`view-toggle__btn${viewMode === mode ? ' view-toggle__btn--active' : ''}`}
            onClick={() => setViewMode(mode)}
          >
            {mode === 'list' ? 'List' :
             mode === 'timeline' ? 'Timeline' :
             mode === 'packet-capture' ? 'Packet Capture' :
             mode === 'manipulation' ? 'Manipulation' : 'Sessions'}
          </button>
        ))}
      </div>

      {viewMode === 'list' && isMobile ? (
        <ErrorBoundary>
          {selectedId ? (
            <CaptureDetail captureId={selectedId} onBack={() => setSelectedId(null)} />
          ) : (
            <CaptureList
              captures={captures}
              total={total}
              loading={loading}
              loadingMore={loadingMore}
              error={error}
              hasMore={hasMore}
              filters={filters}
              devices={devices}
              selectedId={selectedId}
              onSelect={setSelectedId}
              onFiltersChange={setFilters}
              onLoadMore={loadMore}
            />
          )}
        </ErrorBoundary>
      ) : viewMode === 'list' && selectedId ? (
        <ErrorBoundary>
          <SplitPane
            left={
              <CaptureList
                captures={captures}
                total={total}
                loading={loading}
                loadingMore={loadingMore}
                error={error}
                hasMore={hasMore}
                filters={filters}
                devices={devices}
                selectedId={selectedId}
                onSelect={setSelectedId}
                onFiltersChange={setFilters}
                onLoadMore={loadMore}
              />
            }
            right={<CaptureDetail captureId={selectedId} />}
            initialLeftPercent={40}
            onCollapse={() => setSelectedId(null)}
            minLeftPx={260}
            minRightPx={260}
          />
        </ErrorBoundary>
      ) : viewMode === 'list' ? (
        <ErrorBoundary>
          <CaptureList
            captures={captures}
            total={total}
            loading={loading}
            loadingMore={loadingMore}
            error={error}
            hasMore={hasMore}
            filters={filters}
            devices={devices}
            selectedId={selectedId}
            onSelect={setSelectedId}
            onFiltersChange={setFilters}
            onLoadMore={loadMore}
          />
        </ErrorBoundary>
      ) : null}

      {viewMode === 'timeline' && (
        <ErrorBoundary>
          <SplitPane
            left={
              <TimelineSwimlaneView
                captures={captures}
                devices={devices}
                selectedId={selectedId}
                onSelect={setSelectedId}
              />
            }
            right={<CaptureDetail captureId={selectedId} />}
            initialLeftPercent={65}
            minLeftPx={400}
            minRightPx={260}
          />
        </ErrorBoundary>
      )}

      {viewMode === 'packet-capture' && (
        <ErrorBoundary>
          <PanelPacketCapture />
        </ErrorBoundary>
      )}

      {viewMode === 'manipulation' && (
        <ErrorBoundary>
          <ManipulationPanel />
        </ErrorBoundary>
      )}

      {viewMode === 'sessions' && (
        <ErrorBoundary>
          <SessionsPanel />
        </ErrorBoundary>
      )}
    </AppShell>
  )
}
