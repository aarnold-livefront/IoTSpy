import { useState } from 'react'
import ProxyStatusBadge from '../proxy/ProxyStatusBadge'
import SettingsModal from '../proxy/SettingsModal'
import { useLogout } from '../../hooks/useAuth'
import type { ProxySettings, ProxySettingsUpdate } from '../../types/api'
import type { Theme } from '../../hooks/useTheme'
import '../../styles/header.css'

interface Props {
  isRunning: boolean
  port: number
  settings: ProxySettings | null
  signalRConnected: boolean
  loading: boolean
  theme: Theme
  onStart: () => void
  onStop: () => void
  onSaveSettings: (update: ProxySettingsUpdate) => Promise<ProxySettings | null>
  onToggleTheme: () => void
}

export default function Header({
  isRunning,
  port,
  settings,
  signalRConnected,
  loading,
  theme,
  onStart,
  onStop,
  onSaveSettings,
  onToggleTheme,
}: Props) {
  const logout = useLogout()
  const [settingsOpen, setSettingsOpen] = useState(false)

  return (
    <>
      <header className="header">
        <a className="header__logo" href="/">
          <div className="header__logo-icon">I</div>
          <span className="header__logo-text">IoTSpy</span>
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
              Stop
            </button>
          ) : (
            <button
              className="header__btn header__btn--start"
              onClick={onStart}
              disabled={loading}
              title="Start proxy"
            >
              Start
            </button>
          )}

          <a
            className="header__btn"
            href="/api/certificates/root-ca/download"
            download
            title="Download root CA certificate"
          >
            CA
          </a>

          <button
            className="header__btn header__btn--icon"
            onClick={onToggleTheme}
            title={`Switch to ${theme === 'dark' ? 'light' : 'dark'} mode`}
            aria-label="Toggle theme"
          >
            {theme === 'dark' ? '\u2600' : '\u263E'}
          </button>

          <button
            className="header__btn header__btn--icon"
            onClick={() => setSettingsOpen(true)}
            title="Proxy settings"
            aria-label="Proxy settings"
          >
            &#x2699;
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
