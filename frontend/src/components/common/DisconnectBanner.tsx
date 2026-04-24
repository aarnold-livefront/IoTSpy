import type { BackendStatus } from '../../hooks/useBackendHealth'
import '../../styles/disconnect-banner.css'

interface Props {
  status: BackendStatus
}

export default function DisconnectBanner({ status }: Props) {
  if (status === 'up' || status === 'unknown') return null

  return (
    <div className={`disconnect-banner disconnect-banner--${status}`} role="alert">
      <svg className="disconnect-banner__icon" width="16" height="16" viewBox="0 0 16 16" fill="none" aria-hidden="true">
        <circle cx="8" cy="8" r="7" stroke="currentColor" strokeWidth="1.5"/>
        <path d="M8 4.5v4" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round"/>
        <circle cx="8" cy="11" r="0.75" fill="currentColor"/>
      </svg>
      {status === 'reconnecting'
        ? 'Reconnecting to backend…'
        : 'Backend is unavailable — check that the IoTSpy API is running.'}
    </div>
  )
}
