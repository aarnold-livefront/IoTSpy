import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, it, expect, vi, beforeEach } from 'vitest'
import ScannerPanel from '../components/scanner/ScannerPanel'

const noop = vi.fn()

vi.mock('../hooks/useScanner', () => ({
  useScanner: () => ({
    jobs: [], selectedJob: null, loading: false, error: null,
    scan: noop, selectJob: noop, cancel: noop, remove: noop,
  }),
}))
vi.mock('../hooks/useDevices', () => ({
  useDevices: () => ({
    devices: [
      { id: 'dev-1', label: 'Camera A', hostname: 'camera.local', ipAddress: '192.168.1.10' },
      { id: 'dev-2', label: '', hostname: '', ipAddress: '192.168.1.20' },
    ],
    loading: false, error: null,
    addDevice: noop, editDevice: noop, removeDevice: noop, refresh: noop,
  }),
}))
vi.mock('../components/scanner/ScanJobList', () => ({ default: () => <div>ScanJobList</div> }))
vi.mock('../components/scanner/ScanFindingsView', () => ({ default: () => <div>ScanFindingsView</div> }))
vi.mock('../components/scanner/ScheduledScansPanel', () => ({
  ScheduledScansPanel: () => <div>ScheduledScansPanel</div>,
}))

describe('ScannerPanel', () => {
  beforeEach(() => vi.clearAllMocks())

  it('renders the scanner form heading', () => {
    render(<ScannerPanel />)
    expect(screen.getByText('Security Scanner')).toBeInTheDocument()
  })

  it('renders device selector with options', () => {
    render(<ScannerPanel />)
    expect(screen.getByRole('combobox')).toBeInTheDocument()
    expect(screen.getByText('Camera A (192.168.1.10)')).toBeInTheDocument()
    expect(screen.getByText('192.168.1.20 (192.168.1.20)')).toBeInTheDocument()
  })

  it('Start Scan button is disabled when no device selected', () => {
    render(<ScannerPanel />)
    expect(screen.getByRole('button', { name: /Start Scan/i })).toBeDisabled()
  })

  it('Start Scan button enables after device selection', async () => {
    render(<ScannerPanel />)
    await userEvent.selectOptions(screen.getByRole('combobox'), 'dev-1')
    expect(screen.getByRole('button', { name: /Start Scan/i })).not.toBeDisabled()
  })

  it('renders scan option checkboxes', () => {
    render(<ScannerPanel />)
    expect(screen.getByLabelText(/Fingerprinting/i)).toBeInTheDocument()
    expect(screen.getByLabelText(/Credential Test/i)).toBeInTheDocument()
    expect(screen.getByLabelText(/CVE Lookup/i)).toBeInTheDocument()
    expect(screen.getByLabelText(/Config Audit/i)).toBeInTheDocument()
  })

  it('renders ScanJobList child component', () => {
    render(<ScannerPanel />)
    expect(screen.getByText('ScanJobList')).toBeInTheDocument()
  })

  it('calls scan hook with correct params on submit', async () => {
    render(<ScannerPanel />)
    await userEvent.selectOptions(screen.getByRole('combobox'), 'dev-1')
    await userEvent.click(screen.getByRole('button', { name: /Start Scan/i }))
    expect(noop).toHaveBeenCalledWith(
      expect.objectContaining({ deviceId: 'dev-1' })
    )
  })

  it('renders Active Scans and Scheduled Scans tabs', () => {
    render(<ScannerPanel />)
    expect(screen.getByRole('button', { name: /Active Scans/i })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /Scheduled Scans/i })).toBeInTheDocument()
  })

  it('switches to Scheduled Scans tab', async () => {
    render(<ScannerPanel />)
    await userEvent.click(screen.getByRole('button', { name: /Scheduled Scans/i }))
    expect(screen.getByText('ScheduledScansPanel')).toBeInTheDocument()
  })
})
