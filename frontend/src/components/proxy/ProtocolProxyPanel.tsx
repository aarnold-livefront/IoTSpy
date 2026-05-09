import { useState } from 'react'
import { useProtocolProxy } from '../../hooks/useProtocolProxy'
import type { MqttBrokerSettings, CoapProxySettings } from '../../types/api'
import '../../styles/protocol-proxy.css'

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
    <span className={`protocol-proxy-status protocol-proxy-status--${running ? 'running' : 'stopped'}`}>
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
    <div className="protocol-proxy">
      <h2 className="protocol-proxy__title">Protocol Proxies</h2>
      <p className="protocol-proxy__subtitle">
        Standalone protocol-level proxies that intercept and forward MQTT and CoAP traffic independently of the main HTTP proxy.
      </p>

      {error && <div className="protocol-proxy__error" role="alert">{error}</div>}

      <div className="protocol-proxy-grid">

        {/* ── MQTT ─────────────────────────────────────────── */}
        <div className="protocol-proxy-card">
          <div className="protocol-proxy-card__header">
            <h3>MQTT Broker Proxy</h3>
            {mqttLoading
              ? <span className="protocol-proxy-card__loading">Loading…</span>
              : <StatusBadge running={mqttRunning} />}
          </div>

          {mqttRunning && mqttStatus && (
            <div className="protocol-proxy-card__stat">
              Active connections: <strong>{mqttStatus.activeConnections}</strong>
            </div>
          )}

          {!mqttRunning && (
            <div className="protocol-proxy-form-grid">
              <label>
                <span>Listen Port</span>
                <input type="number" value={mqtt.listenPort} min={1} max={65535}
                  onChange={e => setMqtt(p => ({ ...p, listenPort: Number(e.target.value) }))} />
              </label>
              <label>
                <span>Listen Address</span>
                <input value={mqtt.listenAddress}
                  onChange={e => setMqtt(p => ({ ...p, listenAddress: e.target.value }))} />
              </label>
              <label>
                <span>Upstream Host</span>
                <input value={mqtt.upstreamHost ?? ''}
                  onChange={e => setMqtt(p => ({ ...p, upstreamHost: e.target.value }))}
                  placeholder="broker.example.com" />
              </label>
              <label>
                <span>Upstream Port</span>
                <input type="number" value={mqtt.upstreamPort} min={1} max={65535}
                  onChange={e => setMqtt(p => ({ ...p, upstreamPort: Number(e.target.value) }))} />
              </label>
              <label className="protocol-proxy-form-grid__checkbox">
                <input type="checkbox" checked={mqtt.logPayloads}
                  onChange={e => setMqtt(p => ({ ...p, logPayloads: e.target.checked }))} />
                <span>Log payloads</span>
              </label>
              <div className="protocol-proxy-form-grid__row-full">
                <span>Topic Filters (optional)</span>
                <div className="protocol-proxy-topic-input">
                  <input value={topicInput} onChange={e => setTopicInput(e.target.value)}
                    onKeyDown={e => e.key === 'Enter' && addTopic()}
                    placeholder="e.g. sensors/#" />
                  <button className="admin-btn" onClick={addTopic}>Add</button>
                </div>
                {mqtt.topicFilters.length > 0 && (
                  <div className="protocol-proxy-topic-chips">
                    {mqtt.topicFilters.map(t => (
                      <span key={t} className="protocol-proxy-topic-chip">
                        {t}
                        <button
                          onClick={() => removeTopic(t)}
                          aria-label={`Remove topic filter ${t}`}
                          className="protocol-proxy-topic-chip__remove"
                        >×</button>
                      </span>
                    ))}
                  </div>
                )}
              </div>
            </div>
          )}

          <button
            className={`admin-btn ${mqttRunning ? 'admin-btn--danger' : 'admin-btn--primary'} protocol-proxy-card__action-btn`}
            disabled={busy || mqttLoading || (!mqttRunning && !mqtt.upstreamHost?.trim())}
            onClick={() => mqttRunning ? void stopMqtt() : void startMqtt(mqtt)}
          >
            {mqttRunning ? 'Stop MQTT Proxy' : 'Start MQTT Proxy'}
          </button>
          {!mqttRunning && !mqtt.upstreamHost?.trim() && (
            <div className="protocol-proxy-card__hint">
              Upstream host is required to start.
            </div>
          )}
        </div>

        {/* ── CoAP ─────────────────────────────────────────── */}
        <div className="protocol-proxy-card">
          <div className="protocol-proxy-card__header">
            <h3>CoAP Proxy</h3>
            {coapLoading
              ? <span className="protocol-proxy-card__loading">Loading…</span>
              : <StatusBadge running={coapRunning} />}
          </div>

          {coapRunning && coapStatus && (
            <div className="protocol-proxy-card__stat">
              Messages proxied: <strong>{coapStatus.messagesProxied}</strong>
            </div>
          )}

          {!coapRunning && (
            <div className="protocol-proxy-form-grid">
              <label>
                <span>Listen Port</span>
                <input type="number" value={coap.listenPort} min={1} max={65535}
                  onChange={e => setCoap(p => ({ ...p, listenPort: Number(e.target.value) }))} />
              </label>
              <label>
                <span>Listen Address</span>
                <input value={coap.listenAddress}
                  onChange={e => setCoap(p => ({ ...p, listenAddress: e.target.value }))} />
              </label>
              <label>
                <span>Upstream Host</span>
                <input value={coap.upstreamHost ?? ''}
                  onChange={e => setCoap(p => ({ ...p, upstreamHost: e.target.value }))}
                  placeholder="coap.example.com" />
              </label>
              <label>
                <span>Upstream Port</span>
                <input type="number" value={coap.upstreamPort} min={1} max={65535}
                  onChange={e => setCoap(p => ({ ...p, upstreamPort: Number(e.target.value) }))} />
              </label>
              <label className="protocol-proxy-form-grid__checkbox">
                <input type="checkbox" checked={coap.logPayloads}
                  onChange={e => setCoap(p => ({ ...p, logPayloads: e.target.checked }))} />
                <span>Log payloads</span>
              </label>
            </div>
          )}

          <button
            className={`admin-btn ${coapRunning ? 'admin-btn--danger' : 'admin-btn--primary'} protocol-proxy-card__action-btn`}
            disabled={busy || coapLoading || (!coapRunning && !coap.upstreamHost?.trim())}
            onClick={() => coapRunning ? void stopCoap() : void startCoap(coap)}
          >
            {coapRunning ? 'Stop CoAP Proxy' : 'Start CoAP Proxy'}
          </button>
          {!coapRunning && !coap.upstreamHost?.trim() && (
            <div className="protocol-proxy-card__hint">
              Upstream host is required to start.
            </div>
          )}
        </div>

      </div>
    </div>
  )
}
