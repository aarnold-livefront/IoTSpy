import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, it, expect, vi, beforeEach } from 'vitest'
import { MemoryRouter } from 'react-router-dom'
import Header from '../components/layout/Header'

const noop = vi.fn()
const mockLogout = vi.fn()

vi.mock('../hooks/useAuth', () => ({
  useCurrentUser: vi.fn(),
  useLogout: () => mockLogout,
}))
vi.mock('../components/proxy/ProxyStatusBadge', () => ({
  default: () => <div data-testid="proxy-status-badge" />,
}))
vi.mock('../components/proxy/SettingsModal', () => ({
  default: ({ onClose }: { onClose: () => void }) => (
    <div data-testid="settings-modal">
      <button onClick={onClose}>Close</button>
    </div>
  ),
}))

import { useCurrentUser } from '../hooks/useAuth'

const defaultProps = {
  isRunning: false,
  port: 8080,
  settings: null,
  signalRConnected: false,
  loading: false,
  theme: 'dark' as const,
  onStart: noop,
  onStop: noop,
  onSaveSettings: vi.fn(async () => null),
  onToggleTheme: noop,
}

function renderHeader(props = {}) {
  return render(
    <MemoryRouter>
      <Header {...defaultProps} {...props} />
    </MemoryRouter>,
  )
}

describe('Header — hamburger menu', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    vi.mocked(useCurrentUser).mockReturnValue(null)
  })

  it('renders a hamburger button', () => {
    renderHeader()
    expect(screen.getByRole('button', { name: 'More options' })).toBeInTheDocument()
  })

  it('menu is hidden by default', () => {
    renderHeader()
    expect(screen.queryByRole('menu')).not.toBeInTheDocument()
  })

  it('opens the menu on hamburger click', async () => {
    renderHeader()
    await userEvent.click(screen.getByRole('button', { name: 'More options' }))
    expect(screen.getByRole('menu')).toBeInTheDocument()
  })

  it('closes the menu on second hamburger click', async () => {
    renderHeader()
    const btn = screen.getByRole('button', { name: 'More options' })
    await userEvent.click(btn)
    await userEvent.click(btn)
    expect(screen.queryByRole('menu')).not.toBeInTheDocument()
  })

  it('menu contains Theme, Settings, and Sign out items', async () => {
    renderHeader()
    await userEvent.click(screen.getByRole('button', { name: 'More options' }))
    expect(screen.getByRole('menuitem', { name: /light mode|dark mode/i })).toBeInTheDocument()
    expect(screen.getByRole('menuitem', { name: /settings/i })).toBeInTheDocument()
    expect(screen.getByRole('menuitem', { name: /sign out/i })).toBeInTheDocument()
  })

  it('does not show Admin menu item when user is not admin', async () => {
    vi.mocked(useCurrentUser).mockReturnValue({ id: '1', username: 'op', displayName: 'Op', role: 'operator' })
    renderHeader()
    await userEvent.click(screen.getByRole('button', { name: 'More options' }))
    expect(screen.queryByRole('menuitem', { name: /admin/i })).not.toBeInTheDocument()
  })

  it('shows Admin menu item when user is admin', async () => {
    vi.mocked(useCurrentUser).mockReturnValue({ id: '1', username: 'a', displayName: 'Admin', role: 'admin' })
    renderHeader()
    await userEvent.click(screen.getByRole('button', { name: 'More options' }))
    expect(screen.getByRole('menuitem', { name: /admin/i })).toBeInTheDocument()
  })

  it('calls logout and closes menu on Sign out click', async () => {
    renderHeader()
    await userEvent.click(screen.getByRole('button', { name: 'More options' }))
    await userEvent.click(screen.getByRole('menuitem', { name: /sign out/i }))
    expect(mockLogout).toHaveBeenCalledOnce()
    await waitFor(() => expect(screen.queryByRole('menu')).not.toBeInTheDocument())
  })

  it('opens settings modal and closes menu on Settings click', async () => {
    renderHeader({ settings: { mode: 'Intercept', port: 8080, upstreamProxy: null } })
    await userEvent.click(screen.getByRole('button', { name: 'More options' }))
    await userEvent.click(screen.getByRole('menuitem', { name: /settings/i }))
    expect(screen.getByTestId('settings-modal')).toBeInTheDocument()
    await waitFor(() => expect(screen.queryByRole('menu')).not.toBeInTheDocument())
  })
})
