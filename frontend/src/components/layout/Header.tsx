import { useState } from 'react'
import ProxyStatusBadge from '../proxy/ProxyStatusBadge'
import SettingsModal from '../proxy/SettingsModal'
import { useLogout } from '../../hooks/useAuth'
import type { ProxySettings, ProxySettingsUpdate } from '../../types/api'
import '../../styles/header.css'

interface Props {
  isRunning: boolean
  port: number
  settings: ProxySettings | null
  signalRConnected: boolean
  loading: boolean
  onStart: () => void
  onStop: () => void
  onSaveSettings: (update: ProxySettingsUpdate) => Promise<ProxySettings | null>
}

export default function Header({
  isRunning,
  port,
  settings,
  signalRConnected,
  loading,
  onStart,
  onStop,
  onSaveSettings,
}: Props) {
  const logout = useLogout()
  const [settingsOpen, setSettingsOpen] = useState(false)

  return (
    <>
      <header className="header">
        <a className="header__logo" href="/">
          <div className="header__logo-icon">I</div>
          IoTSpy
        </a>

        <ProxyStatusBadge
          isRunning={isRunning}
          port={port}
          signalRConnected={signalRConnected}
        />

        <div className="header__spacer" />

        <div className="header__actions">
          {isRunning ? (
            <button
              className="header__btn header__btn--stop"
              onClick={onStop}
              disabled={loading}
              title="Stop proxy"
            >
              ■ Stop
            </button>
          ) : (
            <button
              className="header__btn header__btn--start"
              onClick={onStart}
              disabled={loading}
              title="Start proxy"
            >
              ▶ Start
            </button>
          )}

          <a
            className="header__btn"
            href="/api/certificates/root-ca/download"
            download
            title="Download root CA certificate"
          >
            ⬇ CA
          </a>

          <button
            className="header__btn header__btn--icon"
            onClick={() => setSettingsOpen(true)}
            title="Proxy settings"
            aria-label="Proxy settings"
          >
            ⚙
          </button>

          <button
            className="header__btn"
            onClick={logout}
            title="Sign out"
          >
            Sign out
          </button>
        </div>
      </header>

      {settingsOpen && settings && (
        <SettingsModal
          settings={settings}
          onSave={onSaveSettings}
          onClose={() => setSettingsOpen(false)}
        />
      )}
    </>
  )
}
