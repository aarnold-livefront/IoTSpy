import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, it, expect, vi, beforeEach } from 'vitest'
import SessionsPanel from '../components/sessions/SessionsPanel'

const noop = vi.fn()

const mockSession = {
  id: 'sess-1',
  name: 'Test Session',
  description: 'desc',
  createdAt: '2026-01-01T00:00:00Z',
  isActive: true,
  shareToken: null,
}

vi.mock('../hooks/useSessions', () => ({
  useSessions: () => ({
    sessions: [mockSession],
    loading: false,
    error: null,
    reload: noop,
  }),
  useSessionDetail: () => ({
    session: null,
    captures: [],
    annotations: [],
    activity: [],
    presence: [],
    setAnnotations: noop,
    reload: noop,
  }),
}))

vi.mock('../api/sessions', () => ({
  createSession: vi.fn(async () => mockSession),
  deleteSession: vi.fn(async () => undefined),
  generateShareToken: vi.fn(async () => ({ url: 'http://share.url/token' })),
  revokeShareToken: vi.fn(async () => undefined),
  exportSession: vi.fn(async () => ({})),
  addCaptureToSession: vi.fn(async () => undefined),
}))

vi.mock('../api/captures', () => ({
  listCaptures: vi.fn(async () => ({ items: [], total: 0 })),
}))

vi.mock('../components/sessions/PresenceIndicator', () => ({ default: () => null }))
vi.mock('../components/sessions/AnnotationPanel', () => ({ default: () => <div>AnnotationPanel</div> }))
vi.mock('../components/common/ConfirmDialog', () => ({
  default: ({ onConfirm, message }: { onConfirm: () => void; message: string }) => (
    <div>
      <span>{message}</span>
      <button onClick={onConfirm}>Confirm</button>
    </div>
  ),
}))

describe('SessionsPanel', () => {
  beforeEach(() => vi.clearAllMocks())

  it('renders the session list with session names', () => {
    render(<SessionsPanel />)
    expect(screen.getByText('Test Session')).toBeInTheDocument()
  })

  it('shows the + New button', () => {
    render(<SessionsPanel />)
    expect(screen.getByRole('button', { name: '+ New' })).toBeInTheDocument()
  })

  it('opens the create form when + New is clicked', async () => {
    render(<SessionsPanel />)
    await userEvent.click(screen.getByRole('button', { name: '+ New' }))
    expect(screen.getByPlaceholderText('Session name')).toBeInTheDocument()
  })

  it('calls createSession when the form is submitted with a name', async () => {
    const { createSession } = await import('../api/sessions')
    render(<SessionsPanel />)

    await userEvent.click(screen.getByRole('button', { name: '+ New' }))
    await userEvent.type(screen.getByPlaceholderText('Session name'), 'My New Session')
    await userEvent.click(screen.getByRole('button', { name: /^Create$/ }))

    await waitFor(() => expect(createSession).toHaveBeenCalledWith('My New Session', undefined))
  })

  it('does not submit the create form when name is empty', async () => {
    const { createSession } = await import('../api/sessions')
    render(<SessionsPanel />)

    await userEvent.click(screen.getByRole('button', { name: '+ New' }))
    // Create button is disabled when name is empty — clicking it does nothing
    const createBtn = screen.getByRole('button', { name: /^Create$/ })
    expect(createBtn).toBeDisabled()
    expect(createSession).not.toHaveBeenCalled()
  })
})
