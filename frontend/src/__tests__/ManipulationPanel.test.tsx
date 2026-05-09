import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, it, expect, vi, beforeEach } from 'vitest'
import ManipulationPanel from '../components/manipulation/ManipulationPanel'

const noop = vi.fn()

const defaultManip = {
  rules: [], rulesLoading: false, rulesError: null,
  addRule: noop, editRule: noop, removeRule: noop,
  breakpoints: [], breakpointsLoading: false, breakpointsError: null,
  addBreakpoint: noop, editBreakpoint: noop, removeBreakpoint: noop,
  replays: [], replaysLoading: false, replaysError: null,
  replay: noop, removeReplay: noop,
  fuzzerJobs: [], selectedFuzzerResults: [], fuzzerLoading: false, fuzzerError: null,
  fuzz: noop, viewFuzzerResults: noop, cancelFuzzer: noop, removeFuzzer: noop,
}

vi.mock('../hooks/useManipulation', () => ({ useManipulation: () => defaultManip }))
vi.mock('../hooks/useCaptures', () => ({ useCaptures: () => ({ captures: [], loading: false, error: null }) }))
vi.mock('../components/manipulation/RulesEditor', () => ({ default: () => <div>RulesEditor</div> }))
vi.mock('../components/manipulation/BreakpointsEditor', () => ({ default: () => <div>BreakpointsEditor</div> }))
vi.mock('../components/manipulation/ReplayPanel', () => ({ default: () => <div>ReplayPanel</div> }))
vi.mock('../components/manipulation/FuzzerPanel', () => ({ default: () => <div>FuzzerPanel</div> }))
vi.mock('../components/contentrules/ContentRulesPanel', () => ({ default: () => <div>ContentRulesPanel</div> }))
vi.mock('../components/apispec/AssetLibrary', () => ({ default: () => <div>AssetLibrary</div> }))
vi.mock('../components/apispec/ApiSpecPanel', () => ({ default: () => <div>ApiSpecPanel</div> }))
vi.mock('../components/grpc/GrpcSchemasPanel', () => ({ default: () => <div>GrpcSchemasPanel</div> }))

describe('ManipulationPanel', () => {
  beforeEach(() => vi.clearAllMocks())

  it('renders all eight tab buttons', () => {
    render(<ManipulationPanel />)
    expect(screen.getByRole('button', { name: 'Traffic Rules' })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Breakpoints' })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Replay' })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Fuzzer' })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Content Rules' })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Assets' })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'API Spec' })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'gRPC Schemas' })).toBeInTheDocument()
  })

  it('shows Traffic Rules tab content by default', () => {
    render(<ManipulationPanel />)
    expect(screen.getByText('RulesEditor')).toBeInTheDocument()
  })

  it('switches to Breakpoints tab on click', async () => {
    render(<ManipulationPanel />)
    await userEvent.click(screen.getByRole('button', { name: 'Breakpoints' }))
    expect(screen.getByText('BreakpointsEditor')).toBeInTheDocument()
  })

  it('switches to Fuzzer tab on click', async () => {
    render(<ManipulationPanel />)
    await userEvent.click(screen.getByRole('button', { name: 'Fuzzer' }))
    expect(screen.getByText('FuzzerPanel')).toBeInTheDocument()
  })

  it('switches to Content Rules tab on click', async () => {
    render(<ManipulationPanel />)
    await userEvent.click(screen.getByRole('button', { name: 'Content Rules' }))
    expect(screen.getByText('ContentRulesPanel')).toBeInTheDocument()
  })

  it('switches to Assets tab on click', async () => {
    render(<ManipulationPanel />)
    await userEvent.click(screen.getByRole('button', { name: 'Assets' }))
    expect(screen.getByText('AssetLibrary')).toBeInTheDocument()
  })

  it('switches to API Spec tab on click', async () => {
    render(<ManipulationPanel />)
    await userEvent.click(screen.getByRole('button', { name: 'API Spec' }))
    expect(screen.getByText('ApiSpecPanel')).toBeInTheDocument()
  })

  it('switches to gRPC Schemas tab on click', async () => {
    render(<ManipulationPanel />)
    await userEvent.click(screen.getByRole('button', { name: 'gRPC Schemas' }))
    expect(screen.getByText('GrpcSchemasPanel')).toBeInTheDocument()
  })

  it('marks the active tab with the active CSS class', async () => {
    render(<ManipulationPanel />)
    const trafficBtn = screen.getByRole('button', { name: 'Traffic Rules' })
    expect(trafficBtn.className).toContain('manip-tab--active')

    await userEvent.click(screen.getByRole('button', { name: 'Breakpoints' }))
    const bpBtn = screen.getByRole('button', { name: 'Breakpoints' })
    expect(bpBtn.className).toContain('manip-tab--active')
    expect(trafficBtn.className).not.toContain('manip-tab--active')
  })
})
