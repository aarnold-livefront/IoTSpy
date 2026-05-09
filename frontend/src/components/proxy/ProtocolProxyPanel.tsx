import { useState } from 'react'
import { useProtocolProxy } from '../../hooks/useProtocolProxy'
import type { MqttBrokerSettings, CoapProxySettings } from '../../types/api'

const DEFAULT_MQTT: MqttBrokerSettings = {
  enabled: true,
  listenPort: 1883,
  listenAddress: '0.0.0.0',
  upstreamHost: '',
  upstreamPort: 1883,
  logPayloads: true,
  topicFilters: [],
}

const DEFAULT_COAP: CoapProxySettings = {
  enabled: true,
  listenPort: 5683,
  listenAddress: '0.0.0.0',
  upstreamHost: '',
  upstreamPort: 5683,
  logPayloads: true,
}

function StatusBadge({ running }: { running: boolean }) {
  return (
    <span style={{ fontSize: 12, color: running ? 'var(--color-success)' : 'var(--color-text-muted)' }}>
      {running ? '● Running' : '○ Stopped'}
    </span>
  )
}

export default function ProtocolProxyPanel() {
  const {
    mqttStatus, mqttLoading,
    coapStatus, coapLoading,
    busy, error,
    startMqtt, stopMqtt,
    startCoap, stopCoap,
  } = useProtocolProxy()

  const [mqtt, setMqtt] = useState<MqttBrokerSettings>(DEFAULT_MQTT)
  const [coap, setCoap] = useState<CoapProxySettings>(DEFAULT_COAP)
  const [topicInput, setTopicInput] = useState('')

  const mqttRunning = mqttStatus?.isRunning ?? false
  const coapRunning = coapStatus?.isRunning ?? false

  const addTopic = () => {
    const t = topicInput.trim()
    if (t && !mqtt.topicFilters.includes(t)) {
      setMqtt(prev => ({ ...prev, topicFilters: [...prev.topicFilters, t] }))
    }
    setTopicInput('')
  }

  const removeTopic = (t: string) =>
    setMqtt(prev => ({ ...prev, topicFilters: prev.topicFilters.filter(x => x !== t) }))

  return (
    <div style={{ padding: 20, maxWidth: 960 }}>
      <h2 style={{ marginTop: 0, marginBottom: 4 }}>Protocol Proxies</h2>
      <p style={{ margin: '0 0 24px', fontSize: 13, color: 'var(--color-text-muted)' }}>
        Standalone protocol-level proxies that intercept and forward MQTT and CoAP traffic independently of the main HTTP proxy.
      </p>

      {error && (
        <div style={{ padding: 10, background: 'var(--color-error-bg)', color: 'var(--color-error)', borderRadius: 4, marginBottom: 16, fontSize: 13 }}>
          {error}
        </div>
      )}

      <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 20 }}>

        {/* ── MQTT ─────────────────────────────────────────── */}
        <div style={{ border: '1px solid var(--color-border)', borderRadius: 8, padding: 16 }}>
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 12 }}>
            <h3 style={{ margin: 0 }}>MQTT Broker Proxy</h3>
            {mqttLoading ? <span style={{ fontSize: 12, color: 'var(--color-text-muted)' }}>Loading…</span>
              : <StatusBadge running={mqttRunning} />}
          </div>

          {mqttRunning && mqttStatus && (
            <div style={{ marginBottom: 12, fontSize: 13, color: 'var(--color-text-muted)' }}>
              Active connections: <strong style={{ color: 'var(--color-text)' }}>{mqttStatus.activeConnections}</strong>
            </div>
          )}

          {!mqttRunning && (
            <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 8, marginBottom: 12 }}>
              <label>
                <span style={{ fontSize: 11, color: 'var(--color-text-muted)' }}>Listen Port</span>
                <input type="number" value={mqtt.listenPort} min={1} max={65535}
                  onChange={e => setMqtt(p => ({ ...p, listenPort: Number(e.target.value) }))}
                  style={{ width: '100%' }} />
              </label>
              <label>
                <span style={{ fontSize: 11, color: 'var(--color-text-muted)' }}>Listen Address</span>
                <input value={mqtt.listenAddress}
                  onChange={e => setMqtt(p => ({ ...p, listenAddress: e.target.value }))}
                  style={{ width: '100%' }} />
              </label>
              <label>
                <span style={{ fontSize: 11, color: 'var(--color-text-muted)' }}>Upstream Host</span>
                <input value={mqtt.upstreamHost ?? ''}
                  onChange={e => setMqtt(p => ({ ...p, upstreamHost: e.target.value }))}
                  placeholder="broker.example.com"
                  style={{ width: '100%' }} />
              </label>
              <label>
                <span style={{ fontSize: 11, color: 'var(--color-text-muted)' }}>Upstream Port</span>
                <input type="number" value={mqtt.upstreamPort} min={1} max={65535}
                  onChange={e => setMqtt(p => ({ ...p, upstreamPort: Number(e.target.value) }))}
                  style={{ width: '100%' }} />
              </label>
              <label style={{ gridColumn: '1 / -1', display: 'flex', alignItems: 'center', gap: 6 }}>
                <input type="checkbox" checked={mqtt.logPayloads}
                  onChange={e => setMqtt(p => ({ ...p, logPayloads: e.target.checked }))} />
                <span style={{ fontSize: 12 }}>Log payloads</span>
              </label>
              <div style={{ gridColumn: '1 / -1' }}>
                <span style={{ fontSize: 11, color: 'var(--color-text-muted)' }}>Topic Filters (optional)</span>
                <div style={{ display: 'flex', gap: 4, marginTop: 4 }}>
                  <input value={topicInput} onChange={e => setTopicInput(e.target.value)}
                    onKeyDown={e => e.key === 'Enter' && addTopic()}
                    placeholder="e.g. sensors/#"
                    style={{ flex: 1 }} />
                  <button className="btn btn--secondary" onClick={addTopic} style={{ fontSize: 11 }}>Add</button>
                </div>
                {mqtt.topicFilters.length > 0 && (
                  <div style={{ marginTop: 6, display: 'flex', flexWrap: 'wrap', gap: 4 }}>
                    {mqtt.topicFilters.map(t => (
                      <span key={t} style={{ fontSize: 11, background: 'var(--color-bg-alt)', padding: '2px 6px', borderRadius: 3, display: 'flex', alignItems: 'center', gap: 4 }}>
                        {t}
                        <button onClick={() => removeTopic(t)} style={{ border: 'none', background: 'none', cursor: 'pointer', color: 'var(--color-text-muted)', padding: 0, lineHeight: 1 }}>×</button>
                      </span>
                    ))}
                  </div>
                )}
              </div>
            </div>
          )}

          <button
            className={`btn ${mqttRunning ? 'btn--danger' : 'btn--primary'}`}
            disabled={busy || mqttLoading || (!mqttRunning && !mqtt.upstreamHost?.trim())}
            onClick={() => mqttRunning ? void stopMqtt() : void startMqtt(mqtt)}
            style={{ width: '100%' }}
          >
            {mqttRunning ? 'Stop MQTT Proxy' : 'Start MQTT Proxy'}
          </button>
          {!mqttRunning && !mqtt.upstreamHost?.trim() && (
            <div style={{ fontSize: 11, color: 'var(--color-text-muted)', marginTop: 4 }}>
              Upstream host is required to start.
            </div>
          )}
        </div>

        {/* ── CoAP ─────────────────────────────────────────── */}
        <div style={{ border: '1px solid var(--color-border)', borderRadius: 8, padding: 16 }}>
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 12 }}>
            <h3 style={{ margin: 0 }}>CoAP Proxy</h3>
            {coapLoading ? <span style={{ fontSize: 12, color: 'var(--color-text-muted)' }}>Loading…</span>
              : <StatusBadge running={coapRunning} />}
          </div>

          {coapRunning && coapStatus && (
            <div style={{ marginBottom: 12, fontSize: 13, color: 'var(--color-text-muted)' }}>
              Messages proxied: <strong style={{ color: 'var(--color-text)' }}>{coapStatus.messagesProxied}</strong>
            </div>
          )}

          {!coapRunning && (
            <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 8, marginBottom: 12 }}>
              <label>
                <span style={{ fontSize: 11, color: 'var(--color-text-muted)' }}>Listen Port</span>
                <input type="number" value={coap.listenPort} min={1} max={65535}
                  onChange={e => setCoap(p => ({ ...p, listenPort: Number(e.target.value) }))}
                  style={{ width: '100%' }} />
              </label>
              <label>
                <span style={{ fontSize: 11, color: 'var(--color-text-muted)' }}>Listen Address</span>
                <input value={coap.listenAddress}
                  onChange={e => setCoap(p => ({ ...p, listenAddress: e.target.value }))}
                  style={{ width: '100%' }} />
              </label>
              <label>
                <span style={{ fontSize: 11, color: 'var(--color-text-muted)' }}>Upstream Host</span>
                <input value={coap.upstreamHost ?? ''}
                  onChange={e => setCoap(p => ({ ...p, upstreamHost: e.target.value }))}
                  placeholder="coap.example.com"
                  style={{ width: '100%' }} />
              </label>
              <label>
                <span style={{ fontSize: 11, color: 'var(--color-text-muted)' }}>Upstream Port</span>
                <input type="number" value={coap.upstreamPort} min={1} max={65535}
                  onChange={e => setCoap(p => ({ ...p, upstreamPort: Number(e.target.value) }))}
                  style={{ width: '100%' }} />
              </label>
              <label style={{ gridColumn: '1 / -1', display: 'flex', alignItems: 'center', gap: 6 }}>
                <input type="checkbox" checked={coap.logPayloads}
                  onChange={e => setCoap(p => ({ ...p, logPayloads: e.target.checked }))} />
                <span style={{ fontSize: 12 }}>Log payloads</span>
              </label>
            </div>
          )}

          <button
            className={`btn ${coapRunning ? 'btn--danger' : 'btn--primary'}`}
            disabled={busy || coapLoading || (!coapRunning && !coap.upstreamHost?.trim())}
            onClick={() => coapRunning ? void stopCoap() : void startCoap(coap)}
            style={{ width: '100%' }}
          >
            {coapRunning ? 'Stop CoAP Proxy' : 'Start CoAP Proxy'}
          </button>
          {!coapRunning && !coap.upstreamHost?.trim() && (
            <div style={{ fontSize: 11, color: 'var(--color-text-muted)', marginTop: 4 }}>
              Upstream host is required to start.
            </div>
          )}
        </div>

      </div>
    </div>
  )
}
