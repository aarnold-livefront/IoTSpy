import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, it, expect, vi, beforeEach } from 'vitest'
import DashboardLayoutsMenu from '../components/dashboard/DashboardLayoutsMenu'

const mockSave = vi.fn()
const mockSetDefault = vi.fn()
const mockRemove = vi.fn()

const mockLayouts = [
  { id: 'l1', userId: 'u1', name: 'Default View', isDefault: true, layoutJson: '{"viewMode":"list"}', filtersJson: '{}' },
  { id: 'l2', userId: 'u1', name: 'Scanner Focus', isDefault: false, layoutJson: '{"viewMode":"scanner"}', filtersJson: '{}' },
]

vi.mock('../hooks/useDashboardLayouts', () => ({
  useDashboardLayouts: () => ({
    layouts: mockLayouts,
    loading: false,
    saving: false,
    error: null,
    save: mockSave,
    setDefault: mockSetDefault,
    remove: mockRemove,
  }),
}))

describe('DashboardLayoutsMenu', () => {
  beforeEach(() => vi.clearAllMocks())

  const setup = () => {
    const onApply = vi.fn()
    render(
      <DashboardLayoutsMenu
        currentLayoutJson='{"viewMode":"list"}'
        currentFiltersJson="{}"
        onApply={onApply}
      />,
    )
    return { onApply }
  }

  it('renders trigger with saved layout count', () => {
    setup()
    expect(screen.getByRole('button', { name: /Layouts/i })).toBeInTheDocument()
    expect(screen.getByText('2')).toBeInTheDocument()
  })

  it('opens the panel and lists saved layouts', async () => {
    setup()
    await userEvent.click(screen.getByRole('button', { name: /Layouts/i }))
    expect(screen.getByText('Default View')).toBeInTheDocument()
    expect(screen.getByText('Scanner Focus')).toBeInTheDocument()
  })

  it('calls onApply with the layout JSON when a saved layout is clicked', async () => {
    const { onApply } = setup()
    await userEvent.click(screen.getByRole('button', { name: /Layouts/i }))
    await userEvent.click(screen.getByRole('button', { name: 'Scanner Focus' }))
    expect(onApply).toHaveBeenCalledWith('{"viewMode":"scanner"}', '{}')
  })

  it('shows validation error when saving without a name', async () => {
    setup()
    await userEvent.click(screen.getByRole('button', { name: /Layouts/i }))
    await userEvent.click(screen.getByRole('button', { name: /Save Layout/i }))
    expect(screen.getByRole('alert')).toHaveTextContent(/Layout name is required/i)
    expect(mockSave).not.toHaveBeenCalled()
  })

  it('calls save with current layout/filters JSON and name', async () => {
    setup()
    await userEvent.click(screen.getByRole('button', { name: /Layouts/i }))
    await userEvent.type(screen.getByLabelText(/Layout name/i), 'My Preset')
    await userEvent.click(screen.getByRole('button', { name: /Save Layout/i }))
    expect(mockSave).toHaveBeenCalledWith('My Preset', '{"viewMode":"list"}', '{}', false)
  })

  it('calls setDefault when the star icon is clicked on a non-default layout', async () => {
    setup()
    await userEvent.click(screen.getByRole('button', { name: /Layouts/i }))
    await userEvent.click(screen.getByRole('button', { name: /Set Scanner Focus as default/i }))
    expect(mockSetDefault).toHaveBeenCalledWith('l2')
  })

  it('calls remove when the delete icon is clicked', async () => {
    setup()
    await userEvent.click(screen.getByRole('button', { name: /Layouts/i }))
    await userEvent.click(screen.getByRole('button', { name: /Delete Scanner Focus/i }))
    expect(mockRemove).toHaveBeenCalledWith('l2')
  })
})
