import { useState } from 'react'
import AppShell from '../components/layout/AppShell'
import Header from '../components/layout/Header'
import SplitPane from '../components/layout/SplitPane'
import CaptureList from '../components/captures/CaptureList'
import CaptureDetail from '../components/captures/CaptureDetail'
import TimelineSwimlaneView from '../components/timeline/TimelineSwimlaneView'
import PanelPacketCapture from '../components/panels/PanelPacketCapture'
import { useProxy } from '../hooks/useProxy'
import { useCaptures } from '../hooks/useCaptures'
import { useDevices } from '../hooks/useDevices'
import { useTrafficStream } from '../hooks/useTrafficStream'
import { useTheme } from '../hooks/useTheme'
import type { CaptureFilters } from '../types/api'

type ViewMode = 'list' | 'timeline' | 'packet-capture'

export default function DashboardPage() {
  const [selectedId, setSelectedId] = useState<string | null>(null)
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
      <div className="view-toggle" style={{
        display: 'flex',
        gap: '4px',
        padding: '4px 12px',
        background: 'var(--color-surface)',
        borderBottom: '1px solid var(--color-border)',
      }}>
        {(['list', 'timeline', 'packet-capture'] as const).map(mode => (
          <button
            key={mode}
            onClick={() => setViewMode(mode)}
            style={{
              background: viewMode === mode ? 'var(--color-primary)' : 'var(--color-surface-2)',
              color: viewMode === mode ? '#fff' : 'var(--color-text-muted)',
              border: '1px solid var(--color-border)',
              borderRadius: 'var(--radius-sm)',
              padding: '2px 12px',
              fontSize: 'var(--font-size-sm)',
              cursor: 'pointer',
            }}
          >
            {mode === 'list' ? 'List' : mode === 'timeline' ? 'Timeline' : 'Packet Capture'}
          </button>
        ))}
      </div>

      {viewMode === 'list' && (
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
          minLeftPx={260}
          minRightPx={260}
        />
      )}

      {viewMode === 'timeline' && (
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
      )}

      {viewMode === 'packet-capture' && (
        <PanelPacketCapture />
      )}
    </AppShell>
  )
}
