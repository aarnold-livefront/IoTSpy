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
          <div className="header__logo-icon">
            {/* Radar/signal icon — fitting for IoT interception */}
            <svg width="16" height="16" viewBox="0 0 16 16" fill="none" aria-hidden="true">
              <circle cx="8" cy="11" r="2" fill="currentColor"/>
              <path d="M5 8.5A4.24 4.24 0 0 1 8 7.5a4.24 4.24 0 0 1 3 1" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round"/>
              <path d="M2 5.5A8.5 8.5 0 0 1 8 4a8.5 8.5 0 0 1 6 1.5" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" opacity="0.6"/>
            </svg>
          </div>
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
            Root CA
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
            className="header__btn header__btn--icon"
            onClick={logout}
            title="Sign out"
            aria-label="Sign out"
          >
            <svg width="16" height="16" viewBox="0 0 16 16" fill="none" aria-hidden="true">
              <path d="M6 2H3a1 1 0 0 0-1 1v10a1 1 0 0 0 1 1h3" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round"/>
              <path d="M10.5 10.5 13 8l-2.5-2.5M13 8H6" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round"/>
            </svg>
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
