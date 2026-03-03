import { useState } from 'react'
import AppShell from '../components/layout/AppShell'
import Header from '../components/layout/Header'
import SplitPane from '../components/layout/SplitPane'
import CaptureList from '../components/captures/CaptureList'
import CaptureDetail from '../components/captures/CaptureDetail'
import { useProxy } from '../hooks/useProxy'
import { useCaptures } from '../hooks/useCaptures'
import { useDevices } from '../hooks/useDevices'
import { useTrafficStream } from '../hooks/useTrafficStream'
import type { CaptureFilters } from '../types/api'

export default function DashboardPage() {
  const [selectedId, setSelectedId] = useState<string | null>(null)
  const [filters, setFilters] = useState<CaptureFilters>({ page: 1, pageSize: 50 })

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
          onStart={proxy.start}
          onStop={proxy.stop}
          onSaveSettings={proxy.saveSettings}
        />
      }
    >
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
    </AppShell>
  )
}
