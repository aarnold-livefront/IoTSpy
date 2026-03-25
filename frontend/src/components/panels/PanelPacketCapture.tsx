import { useState, useEffect } from 'react'
import { usePacketCapture } from '../../hooks/usePacketCapture'
import { usePacketAnalysis } from '../../hooks/usePacketAnalysis'
import PacketListFilterable from '../../components/packet-capture/PacketListFilterable'
import PacketInspector from '../../components/packet-capture/PacketInspector'
import ProtocolDistributionView from '../../components/packet-capture/ProtocolDistributionView'
import PatternExplorer from '../../components/packet-capture/PatternExplorer'
import SuspiciousActivityPanel from '../../components/packet-capture/SuspiciousActivityPanel'
import type { CapturedPacket } from '../../types/api'

type AnalysisTab = 'packets' | 'protocols' | 'patterns' | 'suspicious'

export default function PanelPacketCapture() {
  const { devices, packets, isCapturing, startCapture, stopCapture, clearPackets, error } = usePacketCapture()
  const analysis = usePacketAnalysis()
  const [selectedDevice, setSelectedDevice] = useState<string | null>(null)
  const [selectedPacket, setSelectedPacket] = useState<CapturedPacket | null>(null)
  const [activeTab, setActiveTab] = useState<AnalysisTab>('packets')

  // Load analysis data when switching to an analysis tab
  useEffect(() => {
    if (activeTab === 'protocols') analysis.loadProtocols()
    else if (activeTab === 'patterns') analysis.loadPatterns()
    else if (activeTab === 'suspicious') analysis.loadSuspicious()
  }, [activeTab, analysis])

  const tabs: { key: AnalysisTab; label: string }[] = [
    { key: 'packets', label: 'Packets' },
    { key: 'protocols', label: 'Protocols' },
    { key: 'patterns', label: 'Patterns' },
    { key: 'suspicious', label: 'Suspicious' },
  ]

  return (
    <div style={{ padding: '16px', height: '100%', display: 'flex', gap: '16px' }}>
      {/* Left panel - Device selection and capture controls */}
      <div style={{ width: '280px', display: 'flex', flexDirection: 'column', gap: '12px' }}>
        <h3 style={{ margin: 0, fontSize: 'var(--font-size-md)' }}>Network Capture</h3>

        {error && (
          <div style={{ padding: '8px', background: '#fee', borderRadius: '4px', color: '#c00' }}>
            {error}
          </div>
        )}

        {analysis.error && (
          <div style={{ padding: '8px', background: '#fee', borderRadius: '4px', color: '#c00' }}>
            {analysis.error}
          </div>
        )}

        <div>
          <label style={{ display: 'block', marginBottom: '4px', fontSize: 'var(--font-size-sm)' }}>
            Network Interface:
          </label>
          <select
            value={selectedDevice || ''}
            onChange={(e) => setSelectedDevice(e.target.value)}
            disabled={isCapturing}
            style={{ width: '100%', padding: '8px', borderRadius: 'var(--radius-sm)', border: '1px solid var(--color-border)' }}
          >
            <option value="">Select a device...</option>
            {devices.map((device) => (
              <option key={device.id} value={device.id}>
                {device.displayName} ({device.name})
              </option>
            ))}
          </select>
        </div>

        <div style={{ display: 'flex', gap: '8px' }}>
          {!isCapturing && selectedDevice && (
            <button
              onClick={() => startCapture(selectedDevice)}
              style={{
                flex: 1,
                padding: '10px',
                background: 'var(--color-primary)',
                color: '#fff',
                border: 'none',
                borderRadius: 'var(--radius-sm)',
                cursor: 'pointer',
              }}
            >
              Start Capture
            </button>
          )}

          {isCapturing && (
            <button
              onClick={stopCapture}
              style={{
                flex: 1,
                padding: '10px',
                background: '#d32f2f',
                color: '#fff',
                border: 'none',
                borderRadius: 'var(--radius-sm)',
                cursor: 'pointer',
              }}
            >
              Stop Capture
            </button>
          )}
        </div>

        {/* Analysis refresh button */}
        {activeTab !== 'packets' && (
          <button
            onClick={() => {
              if (activeTab === 'protocols') analysis.loadProtocols()
              else if (activeTab === 'patterns') analysis.loadPatterns()
              else if (activeTab === 'suspicious') analysis.loadSuspicious()
            }}
            disabled={analysis.loading}
            style={{
              padding: '8px',
              background: 'var(--color-surface-2)',
              color: 'var(--color-text)',
              border: '1px solid var(--color-border)',
              borderRadius: 'var(--radius-sm)',
              cursor: analysis.loading ? 'not-allowed' : 'pointer',
            }}
          >
            {analysis.loading ? 'Loading...' : 'Refresh Analysis'}
          </button>
        )}

        <div style={{ marginTop: 'auto' }}>
          <button
            onClick={clearPackets}
            disabled={!packets.length || isCapturing}
            style={{
              width: '100%',
              padding: '8px',
              background: isCapturing ? '#ccc' : 'var(--color-surface-2)',
              color: 'var(--color-text)',
              border: '1px solid var(--color-border)',
              borderRadius: 'var(--radius-sm)',
              cursor: packets.length && !isCapturing ? 'pointer' : 'not-allowed',
            }}
          >
            Clear Packets ({packets.length})
          </button>
        </div>
      </div>

      {/* Middle panel - Tabs + content */}
      <div style={{ flex: 1, display: 'flex', flexDirection: 'column' }}>
        {/* Tab bar */}
        <div style={{
          display: 'flex',
          gap: '2px',
          borderBottom: '2px solid var(--color-border)',
          marginBottom: '0',
        }}>
          {tabs.map(tab => (
            <button
              key={tab.key}
              onClick={() => setActiveTab(tab.key)}
              style={{
                padding: '8px 16px',
                background: activeTab === tab.key ? 'var(--color-primary)' : 'transparent',
                color: activeTab === tab.key ? '#fff' : 'var(--color-text)',
                border: 'none',
                borderRadius: '4px 4px 0 0',
                cursor: 'pointer',
                fontWeight: activeTab === tab.key ? 'bold' : 'normal',
              }}
            >
              {tab.label}
              {tab.key === 'suspicious' && analysis.suspicious.length > 0 && (
                <span style={{
                  marginLeft: '6px',
                  padding: '1px 6px',
                  background: '#d32f2f',
                  color: '#fff',
                  borderRadius: '10px',
                  fontSize: 'var(--font-size-xs)',
                }}>
                  {analysis.suspicious.length}
                </span>
              )}
            </button>
          ))}
        </div>

        {/* Tab content */}
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
          <div style={{ flex: 1, overflowY: 'auto' }}>
            <ProtocolDistributionView distribution={analysis.protocolDistribution} loading={analysis.loading} />
          </div>
        )}

        {activeTab === 'patterns' && (
          <div style={{ flex: 1, overflowY: 'auto' }}>
            <PatternExplorer patterns={analysis.patterns} loading={analysis.loading} />
          </div>
        )}

        {activeTab === 'suspicious' && (
          <div style={{ flex: 1, overflowY: 'auto' }}>
            <SuspiciousActivityPanel activities={analysis.suspicious} loading={analysis.loading} />
          </div>
        )}
      </div>

      {/* Right panel - Packet inspector */}
      {selectedPacket && activeTab === 'packets' && (
        <div style={{ width: '400px' }}>
          <PacketInspector packet={selectedPacket} onClose={() => setSelectedPacket(null)} />
        </div>
      )}
    </div>
  )
}
