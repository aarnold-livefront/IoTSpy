import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, it, expect, vi, beforeEach } from 'vitest'
import PanelPacketCapture from '../components/panels/PanelPacketCapture'

const noop = vi.fn()

const defaultCapture = {
  devices: [
    { id: 'dev-1', name: 'eth0', displayName: 'Ethernet Interface', description: 'Ethernet' },
    { id: 'dev-2', name: 'lo', displayName: 'Loopback', description: 'Loopback' },
  ],
  packets: [],
  isCapturing: false,
  isImporting: false,
  importProgress: null,
  startCapture: noop,
  stopCapture: noop,
  clearPackets: noop,
  importPcapFile: noop,
  error: null,
}

const defaultAnalysis = {
  protocolDistribution: null,
  patterns: [],
  suspicious: [],
  freezeFrame: null,
  isFrozen: false,
  loading: false,
  error: null,
  loadProtocols: noop,
  loadPatterns: noop,
  loadSuspicious: noop,
  createFreezeFrame: noop,
  loadFreezeFrame: noop,
  freezeAnalysis: noop,
  unfreezeAnalysis: noop,
  checkFreezeStatus: noop,
}

vi.mock('../hooks/usePacketCapture', () => ({ usePacketCapture: () => defaultCapture }))
vi.mock('../hooks/usePacketAnalysis', () => ({ usePacketAnalysis: () => defaultAnalysis }))
vi.mock('../api/client', () => ({ getToken: () => null }))
vi.mock('../components/packet-capture/PacketListFilterable', () => ({ default: () => <div>PacketList</div> }))
vi.mock('../components/packet-capture/PacketInspector', () => ({ default: () => <div>PacketInspector</div> }))
vi.mock('../components/packet-capture/ProtocolDistributionView', () => ({ default: () => <div>ProtocolDistribution</div> }))
vi.mock('../components/packet-capture/PatternExplorer', () => ({ default: () => <div>PatternExplorer</div> }))
vi.mock('../components/packet-capture/SuspiciousActivityPanel', () => ({ default: () => <div>SuspiciousActivity</div> }))

describe('PanelPacketCapture', () => {
  beforeEach(() => vi.clearAllMocks())

  it('renders all four analysis tab buttons', () => {
    render(<PanelPacketCapture />)
    expect(screen.getByRole('button', { name: 'Packets' })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Protocols' })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Patterns' })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Suspicious' })).toBeInTheDocument()
  })

  it('shows the packet list by default', () => {
    render(<PanelPacketCapture />)
    expect(screen.getByText('PacketList')).toBeInTheDocument()
  })

  it('switches to Protocols tab on click', async () => {
    render(<PanelPacketCapture />)
    await userEvent.click(screen.getByRole('button', { name: 'Protocols' }))
    expect(screen.getByText('ProtocolDistribution')).toBeInTheDocument()
  })

  it('switches to Patterns tab on click', async () => {
    render(<PanelPacketCapture />)
    await userEvent.click(screen.getByRole('button', { name: 'Patterns' }))
    expect(screen.getByText('PatternExplorer')).toBeInTheDocument()
  })

  it('switches to Suspicious tab on click', async () => {
    render(<PanelPacketCapture />)
    await userEvent.click(screen.getByRole('button', { name: 'Suspicious' }))
    expect(screen.getByText('SuspiciousActivity')).toBeInTheDocument()
  })

  it('shows Start Capture button when a device is selected', async () => {
    render(<PanelPacketCapture />)
    await userEvent.selectOptions(screen.getByRole('combobox'), 'dev-1')
    expect(screen.getByRole('button', { name: /start capture/i })).toBeInTheDocument()
  })

  it('shows Stop Capture button when capturing is active', () => {
    vi.mocked(defaultCapture as Record<string, unknown>).isCapturing = true
    render(<PanelPacketCapture />)
    expect(screen.getByRole('button', { name: /stop capture/i })).toBeInTheDocument()
    vi.mocked(defaultCapture as Record<string, unknown>).isCapturing = false
  })

  it('renders device selector with a default placeholder option', () => {
    render(<PanelPacketCapture />)
    expect(screen.getByRole('option', { name: /select a device/i })).toBeInTheDocument()
  })

  it('renders device names in the selector', () => {
    render(<PanelPacketCapture />)
    expect(screen.getByRole('option', { name: /Ethernet Interface/i })).toBeInTheDocument()
    expect(screen.getByRole('option', { name: /Loopback/i })).toBeInTheDocument()
  })

  it('displays an error message when capture errors', () => {
    vi.mocked(defaultCapture as Record<string, unknown>).error = 'Permission denied'
    render(<PanelPacketCapture />)
    expect(screen.getByText(/Permission denied/i)).toBeInTheDocument()
    vi.mocked(defaultCapture as Record<string, unknown>).error = null
  })
})
