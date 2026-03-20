import { render, screen } from '@testing-library/react'
import { describe, it, expect } from 'vitest'
import ErrorBanner from '../components/common/ErrorBanner'

describe('ErrorBanner', () => {
  it('renders the error message', () => {
    render(<ErrorBanner message="Something went wrong" />)
    expect(screen.getByText('Something went wrong')).toBeInTheDocument()
  })

  it('has role="alert" for accessibility', () => {
    render(<ErrorBanner message="Error" />)
    expect(screen.getByRole('alert')).toBeInTheDocument()
  })

  it('renders different messages correctly', () => {
    const { rerender } = render(<ErrorBanner message="First error" />)
    expect(screen.getByText('First error')).toBeInTheDocument()

    rerender(<ErrorBanner message="Second error" />)
    expect(screen.getByText('Second error')).toBeInTheDocument()
  })
})
