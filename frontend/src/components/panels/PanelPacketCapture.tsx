import { useState, useEffect, useRef } from 'react'
import { usePacketCapture } from '../../hooks/usePacketCapture'
import { usePacketAnalysis } from '../../hooks/usePacketAnalysis'
import PacketListFilterable from '../../components/packet-capture/PacketListFilterable'
import PacketInspector from '../../components/packet-capture/PacketInspector'
import ProtocolDistributionView from '../../components/packet-capture/ProtocolDistributionView'
import PatternExplorer from '../../components/packet-capture/PatternExplorer'
import SuspiciousActivityPanel from '../../components/packet-capture/SuspiciousActivityPanel'
import { getToken } from '../../api/client'
import type { CapturedPacket, PcapImportResult } from '../../types/api'
import '../../styles/panel-packet-capture.css'

type AnalysisTab = 'packets' | 'protocols' | 'patterns' | 'suspicious'

export default function PanelPacketCapture() {
  const {
    devices, packets, isCapturing, isImporting, importProgress,
    startCapture, stopCapture, clearPackets, importPcapFile, error
  } = usePacketCapture()
  const analysis = usePacketAnalysis()
  // Destructure stable useCallback references so the effect below doesn't re-run
  // every render (the `analysis` object literal is unstable; in React 19 strict
  // mode that produces an infinite render loop).
  const { loadProtocols, loadPatterns, loadSuspicious } = analysis
  const [selectedDevice, setSelectedDevice] = useState<string | null>(null)
  const [selectedPacket, setSelectedPacket] = useState<CapturedPacket | null>(null)
  const [activeTab, setActiveTab] = useState<AnalysisTab>('packets')
  const [isDragOver, setIsDragOver] = useState(false)
  const [importResult, setImportResult] = useState<PcapImportResult | null>(null)
  const [exportError, setExportError] = useState<string | null>(null)
  const fileInputRef = useRef<HTMLInputElement>(null)

  useEffect(() => {
    if (activeTab === 'protocols') loadProtocols()
    else if (activeTab === 'patterns') loadPatterns()
    else if (activeTab === 'suspicious') loadSuspicious()
  }, [activeTab, loadProtocols, loadPatterns, loadSuspicious])

  const handleFileDrop = async (file: File) => {
    setImportResult(null)
    try {
      const result = await importPcapFile(file)
      setImportResult(result)
    } catch {
      // error already set in hook
    }
  }

  const handleDrop = (e: React.DragEvent) => {
    e.preventDefault()
    setIsDragOver(false)
    const file = e.dataTransfer.files[0]
    if (file) handleFileDrop(file)
  }

  const handleFileInput = (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0]
    if (file) handleFileDrop(file)
    if (fileInputRef.current) fileInputRef.current.value = ''
  }

  const handleExport = async () => {
    setExportError(null)
    try {
      const token = getToken()
      const res = await fetch('/api/packet-capture/export/pcap', {
        headers: token ? { Authorization: `Bearer ${token}` } : {},
      })
      if (!res.ok) {
        setExportError(`Export failed (HTTP ${res.status}). Check server logs.`)
        return
      }
      const blob = await res.blob()
      const url = URL.createObjectURL(blob)
      const a = document.createElement('a')
      a.href = url
      a.download = 'capture.pcap'
      a.click()
      URL.revokeObjectURL(url)
    } catch (e) {
      setExportError(e instanceof Error ? e.message : 'Export failed.')
    }
  }

  const tabs: { key: AnalysisTab; label: string }[] = [
    { key: 'packets', label: 'Packets' },
    { key: 'protocols', label: 'Protocols' },
    { key: 'patterns', label: 'Patterns' },
    { key: 'suspicious', label: 'Suspicious' },
  ]

  const dropzoneClass = [
    'ppc-dropzone',
    isDragOver ? 'ppc-dropzone--active' : '',
    isImporting ? 'ppc-dropzone--importing' : '',
  ].filter(Boolean).join(' ')

  return (
    <div className="ppc-root">
      <div className="ppc-sidebar">
        <h3>Network Capture</h3>

        {error && <div className="ppc-error">{error}</div>}
        {analysis.error && <div className="ppc-error">{analysis.error}</div>}
        {exportError && <div className="ppc-error" role="alert">{exportError}</div>}

        <div>
          <label className="ppc-label">Network Interface:</label>
          <select
            value={selectedDevice || ''}
            onChange={(e) => setSelectedDevice(e.target.value)}
            disabled={isCapturing}
            className="ppc-select"
          >
            <option value="">Select a device...</option>
            {devices.map((device) => (
              <option key={device.id} value={device.id}>
                {device.displayName} ({device.name})
              </option>
            ))}
          </select>
        </div>

        <div className="ppc-capture-controls">
          {!isCapturing && selectedDevice && (
            <button
              onClick={() => startCapture(selectedDevice)}
              className="ppc-btn-start"
            >
              Start Capture
            </button>
          )}
          {isCapturing && (
            <button onClick={stopCapture} className="ppc-btn-stop">
              Stop Capture
            </button>
          )}
        </div>

        {activeTab !== 'packets' && (
          <button
            onClick={() => {
              if (activeTab === 'protocols') analysis.loadProtocols()
              else if (activeTab === 'patterns') analysis.loadPatterns()
              else if (activeTab === 'suspicious') analysis.loadSuspicious()
            }}
            disabled={analysis.loading}
            className="ppc-btn-refresh"
          >
            {analysis.loading ? 'Loading...' : 'Refresh Analysis'}
          </button>
        )}

        <div>
          <div
            onDrop={handleDrop}
            onDragOver={(e) => { e.preventDefault(); setIsDragOver(true) }}
            onDragLeave={() => setIsDragOver(false)}
            onClick={() => fileInputRef.current?.click()}
            className={dropzoneClass}
          >
            <input
              ref={fileInputRef}
              type="file"
              accept=".pcap,.pcapng,.cap"
              onChange={handleFileInput}
              style={{ display: 'none' }}
              disabled={isImporting}
            />
            {isImporting ? (
              <span>Importing{importProgress && importProgress.total > 0 ? ` ${importProgress.percent}%` : '…'}</span>
            ) : (
              <span>Drop .pcap/.pcapng here<br />or click to browse</span>
            )}
          </div>

          {isImporting && importProgress && importProgress.total > 0 && (
            <div className="ppc-progress-track">
              <div
                className="ppc-progress-fill"
                style={{ width: `${importProgress.percent}%` }}
              />
            </div>
          )}

          {importResult && !isImporting && (
            <div className="ppc-import-summary">
              Imported {importResult.packetsImported} packets
              {importResult.tcpSessionsReconstructed > 0 && `, ${importResult.tcpSessionsReconstructed} HTTP sessions reconstructed`}
              {importResult.packetsSkipped > 0 && `, ${importResult.packetsSkipped} skipped`}
            </div>
          )}
        </div>

        <div className="ppc-sidebar-bottom">
          <button
            onClick={handleExport}
            disabled={!packets.length}
            className="ppc-btn-secondary"
          >
            Export PCAP
          </button>
          <button
            onClick={clearPackets}
            disabled={!packets.length || isCapturing}
            className="ppc-btn-secondary"
          >
            Clear Packets ({packets.length})
          </button>
        </div>
      </div>

      <div className="ppc-main">
        <div className="ppc-tabs">
          {tabs.map(tab => (
            <button
              key={tab.key}
              onClick={() => setActiveTab(tab.key)}
              className={`ppc-tab${activeTab === tab.key ? ' ppc-tab--active' : ''}`}
            >
              {tab.label}
              {tab.key === 'suspicious' && analysis.suspicious.length > 0 && (
                <span className="ppc-tab-badge">{analysis.suspicious.length}</span>
              )}
            </button>
          ))}
        </div>

        {activeTab === 'packets' && (
          <PacketListFilterable
            packets={packets}
            isCapturing={isCapturing}
            selectedPacket={selectedPacket}
            onSelect={setSelectedPacket}
            freezeFrame={selectedPacket !== null}
          />
        )}
        {activeTab === 'protocols' && (
          <div className="ppc-tab-content">
            <ProtocolDistributionView distribution={analysis.protocolDistribution} loading={analysis.loading} />
          </div>
        )}
        {activeTab === 'patterns' && (
          <div className="ppc-tab-content">
            <PatternExplorer patterns={analysis.patterns} loading={analysis.loading} />
          </div>
        )}
        {activeTab === 'suspicious' && (
          <div className="ppc-tab-content">
            <SuspiciousActivityPanel activities={analysis.suspicious} loading={analysis.loading} />
          </div>
        )}
      </div>

      {selectedPacket && activeTab === 'packets' && (
        <div className="ppc-inspector">
          <PacketInspector packet={selectedPacket} onClose={() => setSelectedPacket(null)} />
        </div>
      )}
    </div>
  )
}
