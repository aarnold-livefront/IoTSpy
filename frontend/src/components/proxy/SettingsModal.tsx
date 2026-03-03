import { useEffect, useRef, useState } from 'react'
import type { ProxySettings, ProxySettingsUpdate } from '../../types/api'
import '../../styles/modal.css'

interface Props {
  settings: ProxySettings
  onSave: (update: ProxySettingsUpdate) => Promise<ProxySettings | null>
  onClose: () => void
}

export default function SettingsModal({ settings, onSave, onClose }: Props) {
  const [proxyPort, setProxyPort] = useState(String(settings.proxyPort))
  const [listenAddress, setListenAddress] = useState(settings.listenAddress)
  const [captureTls, setCaptureTls] = useState(settings.captureTls)
  const [captureRequestBodies, setCaptureRequestBodies] = useState(settings.captureRequestBodies)
  const [captureResponseBodies, setCaptureResponseBodies] = useState(settings.captureResponseBodies)
  const [maxBodySizeKb, setMaxBodySizeKb] = useState(String(settings.maxBodySizeKb))
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState<string | null>(null)

  // Close on Escape
  useEffect(() => {
    function onKey(e: KeyboardEvent) {
      if (e.key === 'Escape') onClose()
    }
    window.addEventListener('keydown', onKey)
    return () => window.removeEventListener('keydown', onKey)
  }, [onClose])

  // Trap focus
  const modalRef = useRef<HTMLDivElement>(null)
  useEffect(() => {
    modalRef.current?.focus()
  }, [])

  async function handleSave() {
    const port = Number(proxyPort)
    const bodyKb = Number(maxBodySizeKb)
    if (!port || port < 1 || port > 65535) {
      setError('Proxy port must be between 1 and 65535.')
      return
    }
    if (!bodyKb || bodyKb < 1) {
      setError('Max body size must be at least 1 KB.')
      return
    }
    setSaving(true)
    setError(null)
    const result = await onSave({
      proxyPort: port,
      listenAddress,
      captureTls,
      captureRequestBodies,
      captureResponseBodies,
      maxBodySizeKb: bodyKb,
    })
    setSaving(false)
    if (result) {
      onClose()
    } else {
      setError('Failed to save settings. Check the console for details.')
    }
  }

  return (
    <div className="modal-overlay" onClick={(e) => e.target === e.currentTarget && onClose()}>
      <div
        className="modal"
        role="dialog"
        aria-modal="true"
        aria-labelledby="settings-modal-title"
        ref={modalRef}
        tabIndex={-1}
      >
        <div className="modal__header">
          <h2 className="modal__title" id="settings-modal-title">Proxy Settings</h2>
          <button className="modal__close" onClick={onClose} aria-label="Close">✕</button>
        </div>

        <div className="modal__body">
          {error && <div className="modal-error">{error}</div>}

          <div className="settings-section-title">Network</div>

          <div className="settings-row">
            <div className="settings-field">
              <label className="settings-label" htmlFor="proxy-port">Proxy Port</label>
              <input
                id="proxy-port"
                className="settings-input"
                type="number"
                min={1}
                max={65535}
                value={proxyPort}
                onChange={(e) => setProxyPort(e.target.value)}
              />
            </div>
            <div className="settings-field">
              <label className="settings-label" htmlFor="listen-addr">Listen Address</label>
              <input
                id="listen-addr"
                className="settings-input"
                type="text"
                value={listenAddress}
                onChange={(e) => setListenAddress(e.target.value)}
                placeholder="127.0.0.1"
              />
            </div>
          </div>

          <div className="settings-section-title">Capture</div>

          <label className="settings-checkbox-row">
            <input
              type="checkbox"
              checked={captureTls}
              onChange={(e) => setCaptureTls(e.target.checked)}
            />
            Intercept TLS (HTTPS MITM)
          </label>

          <label className="settings-checkbox-row">
            <input
              type="checkbox"
              checked={captureRequestBodies}
              onChange={(e) => setCaptureRequestBodies(e.target.checked)}
            />
            Capture request bodies
          </label>

          <label className="settings-checkbox-row">
            <input
              type="checkbox"
              checked={captureResponseBodies}
              onChange={(e) => setCaptureResponseBodies(e.target.checked)}
            />
            Capture response bodies
          </label>

          <div className="settings-field" style={{ maxWidth: 180 }}>
            <label className="settings-label" htmlFor="max-body">Max body size (KB)</label>
            <input
              id="max-body"
              className="settings-input"
              type="number"
              min={1}
              value={maxBodySizeKb}
              onChange={(e) => setMaxBodySizeKb(e.target.value)}
            />
          </div>

          <div className="settings-section-title">Mode</div>

          <div className="settings-field">
            <label className="settings-label" htmlFor="proxy-mode">Proxy Mode</label>
            <select
              id="proxy-mode"
              className="settings-select"
              value="ExplicitProxy"
              disabled
              title="ARP Spoof and Gateway Redirect modes are available in Phase 2"
            >
              <option value="ExplicitProxy">Explicit Proxy (Phase 1)</option>
              <option value="ArpSpoof" disabled>ARP Spoof (Phase 2)</option>
              <option value="GatewayRedirect" disabled>Gateway Redirect (Phase 2)</option>
            </select>
          </div>
        </div>

        <div className="modal__footer">
          <button className="btn-modal-cancel" onClick={onClose} disabled={saving}>
            Cancel
          </button>
          <button className="btn-modal-save" onClick={handleSave} disabled={saving}>
            {saving ? 'Saving…' : 'Save'}
          </button>
        </div>
      </div>
    </div>
  )
}
