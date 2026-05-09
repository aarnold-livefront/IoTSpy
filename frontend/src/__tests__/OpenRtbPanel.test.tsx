import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, it, expect, vi, beforeEach } from 'vitest'
import OpenRtbPanel from '../components/openrtb/OpenRtbPanel'

const noop = vi.fn()

vi.mock('../hooks/useOpenRtb', () => ({
  useOpenRtb: () => ({
    events: [], eventsTotal: 0, eventsLoading: false, eventsError: null, refreshEvents: noop,
    policies: [], policiesLoading: false, policiesError: null,
    addPolicy: noop, editPolicy: noop, removePolicy: noop, resetPolicies: noop,
    auditLogs: [], auditTotal: 0, auditStats: null, auditLoading: false, auditError: null,
    refreshAuditLog: noop,
  }),
}))
vi.mock('../components/openrtb/OpenRtbTrafficList', () => ({
  default: () => <div>OpenRtbTrafficList</div>,
}))
vi.mock('../components/openrtb/OpenRtbInspector', () => ({
  default: () => <div>OpenRtbInspector</div>,
}))
vi.mock('../components/openrtb/PiiPolicyEditor', () => ({
  default: () => <div>PiiPolicyEditor</div>,
}))
vi.mock('../components/openrtb/PiiAuditLog', () => ({
  default: () => <div>PiiAuditLog</div>,
}))

describe('OpenRtbPanel', () => {
  beforeEach(() => vi.clearAllMocks())

  it('renders three tab buttons', () => {
    render(<OpenRtbPanel />)
    expect(screen.getByRole('button', { name: 'Traffic' })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'PII Policies' })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Audit Log' })).toBeInTheDocument()
  })

  it('shows Traffic tab content by default', () => {
    render(<OpenRtbPanel />)
    expect(screen.getByText('OpenRtbTrafficList')).toBeInTheDocument()
  })

  it('switches to PII Policies tab', async () => {
    render(<OpenRtbPanel />)
    await userEvent.click(screen.getByRole('button', { name: 'PII Policies' }))
    expect(screen.getByText('PiiPolicyEditor')).toBeInTheDocument()
  })

  it('switches to Audit Log tab', async () => {
    render(<OpenRtbPanel />)
    await userEvent.click(screen.getByRole('button', { name: 'Audit Log' }))
    expect(screen.getByText('PiiAuditLog')).toBeInTheDocument()
  })

  it('marks active tab with CSS class', async () => {
    render(<OpenRtbPanel />)
    const trafficBtn = screen.getByRole('button', { name: 'Traffic' })
    expect(trafficBtn.className).toContain('manip-tab--active')

    await userEvent.click(screen.getByRole('button', { name: 'PII Policies' }))
    expect(screen.getByRole('button', { name: 'PII Policies' }).className).toContain('manip-tab--active')
    expect(screen.getByRole('button', { name: 'Traffic' }).className).not.toContain('manip-tab--active')
  })
})
