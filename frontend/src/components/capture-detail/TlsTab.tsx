import type { CapturedRequest } from '../../types/api'
import '../../styles/capture-detail.css'

interface Props {
  capture: CapturedRequest
}

export default function TlsTab({ capture }: Props) {
  const { isTls, tlsVersion, tlsCipherSuite, protocol } = capture

  if (!isTls) {
    return (
      <div className="detail-tab-content">
        <p className="empty-note">This request was not intercepted over TLS.</p>
      </div>
    )
  }

  return (
    <div className="detail-tab-content">
      <div className="tls-grid">
        <span className="tls-grid__key">TLS</span>
        <span className="tls-grid__val">Yes</span>

        <span className="tls-grid__key">Version</span>
        <span className="tls-grid__val">{tlsVersion || '—'}</span>

        <span className="tls-grid__key">Cipher Suite</span>
        <span className="tls-grid__val">{tlsCipherSuite || '—'}</span>

        <span className="tls-grid__key">Protocol</span>
        <span className="tls-grid__val">{protocol}</span>
      </div>
    </div>
  )
}
