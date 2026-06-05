import React from 'react'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, it, expect, vi, beforeEach } from 'vitest'
import CaptureList from '../components/captures/CaptureList'
import type { CaptureFilters, CapturedRequestSummary } from '../types/api'

vi.mock('react-window', () => ({
  // children is a React component (memo-wrapped), so use createElement not direct call
  FixedSizeList: ({ children, itemCount, itemData }: any) => (
    <div data-testid="virtual-list">
      {Array.from({ length: Math.min(itemCount, 5) }, (_, i) =>
        React.createElement(children, { key: i, index: i, style: {}, data: itemData })
      )}
    </div>
  ),
}))
// AutoSizer is a named export; CaptureList calls it via renderProp not children
vi.mock('react-virtualized-auto-sizer', () => ({
  AutoSizer: ({ renderProp, children }: any) => {
    const fn = renderProp ?? children
    return fn ? fn({ width: 800, height: 600 }) : null
  },
}))
vi.mock('../components/captures/CaptureRow', () => ({
  default: ({ capture }: any) => <div data-testid="capture-row">{capture.host}</div>,
}))
// CaptureFilterBar passes onChange (not onFiltersChange) to the parent
vi.mock('../components/captures/CaptureFilterBar', () => ({
  default: ({ onChange }: any) => (
    <div>
      <button onClick={() => onChange({})}>Reset filters</button>
    </div>
  ),
}))
vi.mock('../api/captures', () => ({ exportCaptures: vi.fn() }))

const noop = vi.fn()

const makeSummary = (i: number): CapturedRequestSummary => ({
  id: `cap-${i}`,
  host: `host-${i}.example.com`,
  method: 'GET',
  scheme: 'https',
  port: 443,
  path: `/path/${i}`,
  query: '',
  requestHeaders: '',
  requestBodySize: 100,
  statusCode: 200,
  statusMessage: 'OK',
  responseHeaders: '',
  responseBodySize: 200,
  isTls: true,
  tlsVersion: 'TLS 1.3',
  tlsCipherSuite: '',
  protocol: 'Https',
  timestamp: new Date().toISOString(),
  durationMs: 42,
  clientIp: '192.168.1.1',
  isModified: false,
  notes: '',
})

const defaultFilters: CaptureFilters = {}

const defaultFreezeProps = {
  frozen: false,
  pendingCount: 0,
  onFreeze: noop,
  onResume: noop,
}

describe('CaptureList', () => {
  beforeEach(() => vi.clearAllMocks())

  it('renders virtual list when captures present', () => {
    const captures = [makeSummary(1), makeSummary(2)]
    render(
      <CaptureList
        captures={captures}
        total={2}
        loading={false}
        loadingMore={false}
        error={null}
        hasMore={false}
        filters={defaultFilters}
        devices={[]}
        selectedId={null}
        onSelect={noop}
        onFiltersChange={noop}
        onLoadMore={noop}
        {...defaultFreezeProps}
      />
    )
    expect(screen.getByTestId('virtual-list')).toBeInTheDocument()
    expect(screen.getAllByTestId('capture-row')).toHaveLength(2)
    expect(screen.getByText('host-1.example.com')).toBeInTheDocument()
  })

  it('renders empty list without crashing', () => {
    render(
      <CaptureList
        captures={[]}
        total={0}
        loading={false}
        loadingMore={false}
        error={null}
        hasMore={false}
        filters={defaultFilters}
        devices={[]}
        selectedId={null}
        onSelect={noop}
        onFiltersChange={noop}
        onLoadMore={noop}
        {...defaultFreezeProps}
      />
    )
    expect(screen.queryAllByTestId('capture-row')).toHaveLength(0)
  })

  it('shows error banner when error prop is set', () => {
    render(
      <CaptureList
        captures={[]}
        total={0}
        loading={false}
        loadingMore={false}
        error="Network failure"
        hasMore={false}
        filters={defaultFilters}
        devices={[]}
        selectedId={null}
        onSelect={noop}
        onFiltersChange={noop}
        onLoadMore={noop}
        {...defaultFreezeProps}
      />
    )
    expect(screen.getByText(/Network failure/i)).toBeInTheDocument()
  })

  it('calls onFiltersChange when filter bar emits change', async () => {
    render(
      <CaptureList
        captures={[]}
        total={0}
        loading={false}
        loadingMore={false}
        error={null}
        hasMore={false}
        filters={defaultFilters}
        devices={[]}
        selectedId={null}
        onSelect={noop}
        onFiltersChange={noop}
        onLoadMore={noop}
        {...defaultFreezeProps}
      />
    )
    await userEvent.click(screen.getByRole('button', { name: /Reset filters/i }))
    expect(noop).toHaveBeenCalled()
  })
})
