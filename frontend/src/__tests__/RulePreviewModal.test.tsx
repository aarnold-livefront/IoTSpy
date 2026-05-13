import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, it, expect, vi, beforeEach } from 'vitest'
import RulePreviewModal from '../components/apispec/RulePreviewModal'
import type { PreviewRuleResult } from '../types/api'

const onClose = vi.fn()

const makeResult = (overrides: Partial<PreviewRuleResult> = {}): PreviewRuleResult => ({
  statusCode: 200,
  matched: true,
  modified: true,
  bodyLength: 42,
  contentType: 'application/json',
  responseBodyText: '{"ok":true}',
  responseBodyBase64: undefined,
  responseHeaders: { 'Content-Type': 'application/json' },
  warnings: [],
  wasStreamed: false,
  ...overrides,
})

vi.mock('../api/captures', () => ({
  listCaptures: vi.fn().mockResolvedValue({ items: [] }),
}))

describe('RulePreviewModal', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    // reset focus
    document.body.focus()
  })

  it('renders with correct dialog role and title', () => {
    render(
      <RulePreviewModal specId="" ruleId="r1" ruleName="Block ads" onClose={onClose} />
    )
    expect(screen.getByRole('dialog')).toBeInTheDocument()
    expect(screen.getByText('Preview rule: Block ads')).toBeInTheDocument()
  })

  it('calls onClose when Close button is clicked', async () => {
    render(
      <RulePreviewModal specId="" ruleId="r1" ruleName="My Rule" onClose={onClose} />
    )
    await userEvent.click(screen.getByRole('button', { name: /close/i }))
    expect(onClose).toHaveBeenCalledOnce()
  })

  it('calls onClose when Escape is pressed', async () => {
    render(
      <RulePreviewModal specId="" ruleId="r1" ruleName="My Rule" onClose={onClose} />
    )
    await userEvent.keyboard('{Escape}')
    expect(onClose).toHaveBeenCalledOnce()
  })

  it('calls onClose when overlay backdrop is clicked', async () => {
    const { container } = render(
      <RulePreviewModal specId="" ruleId="r1" ruleName="My Rule" onClose={onClose} />
    )
    const overlay = container.querySelector('.modal-overlay')!
    await userEvent.click(overlay)
    expect(onClose).toHaveBeenCalledOnce()
  })

  it('renders synthetic and capture mode toggle buttons', () => {
    render(
      <RulePreviewModal specId="" ruleId="r1" ruleName="My Rule" onClose={onClose} />
    )
    expect(screen.getByRole('button', { name: /synthetic payload/i })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /from capture/i })).toBeInTheDocument()
  })

  it('shows synthetic form fields by default', () => {
    render(
      <RulePreviewModal specId="" ruleId="r1" ruleName="My Rule" onClose={onClose} />
    )
    expect(screen.getByDisplayValue('example.com')).toBeInTheDocument()
    expect(screen.getByDisplayValue('/')).toBeInTheDocument()
  })

  it('shows capture list when From capture mode is selected', async () => {
    render(
      <RulePreviewModal specId="" ruleId="r1" ruleName="My Rule" onClose={onClose} />
    )
    await userEvent.click(screen.getByRole('button', { name: /from capture/i }))
    await waitFor(() => {
      expect(screen.getByRole('listbox', { name: /select a capture/i })).toBeInTheDocument()
    })
  })

  it('calls overridePreview and shows result on Run preview', async () => {
    const overridePreview = vi.fn().mockResolvedValue(makeResult())
    render(
      <RulePreviewModal
        specId=""
        ruleId="r1"
        ruleName="My Rule"
        onClose={onClose}
        overridePreview={overridePreview}
      />
    )
    await userEvent.click(screen.getByRole('button', { name: /run preview/i }))
    await waitFor(() => {
      expect(overridePreview).toHaveBeenCalledOnce()
      expect(screen.getByText(/matched/i)).toBeInTheDocument()
    })
  })

  it('shows result metadata when preview succeeds', async () => {
    const overridePreview = vi.fn().mockResolvedValue(makeResult())
    render(
      <RulePreviewModal
        specId=""
        ruleId="r1"
        ruleName="My Rule"
        onClose={onClose}
        overridePreview={overridePreview}
      />
    )
    await userEvent.click(screen.getByRole('button', { name: /run preview/i }))
    await waitFor(() => {
      const meta = document.querySelector('.preview-modal__result-meta')!
      expect(meta).toHaveTextContent('42')
      expect(meta).toHaveTextContent('application/json')
    })
  })

  it('shows error message when preview fails', async () => {
    const overridePreview = vi.fn().mockRejectedValue(new Error('server unavailable'))
    render(
      <RulePreviewModal
        specId=""
        ruleId="r1"
        ruleName="My Rule"
        onClose={onClose}
        overridePreview={overridePreview}
      />
    )
    await userEvent.click(screen.getByRole('button', { name: /run preview/i }))
    await waitFor(() => {
      expect(screen.getByText(/server unavailable/i)).toBeInTheDocument()
    })
  })

  it('renders response body text when present', async () => {
    const overridePreview = vi.fn().mockResolvedValue(makeResult({ responseBodyText: '{"ok":true}' }))
    render(
      <RulePreviewModal
        specId=""
        ruleId="r1"
        ruleName="My Rule"
        onClose={onClose}
        overridePreview={overridePreview}
      />
    )
    await userEvent.click(screen.getByRole('button', { name: /run preview/i }))
    await waitFor(() => {
      expect(screen.getByText('{"ok":true}')).toBeInTheDocument()
    })
  })
})
