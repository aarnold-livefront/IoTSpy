import { render, screen } from '@testing-library/react'
import { describe, it, expect } from 'vitest'
import LoadingSpinner from '../components/common/LoadingSpinner'

describe('LoadingSpinner', () => {
  it('renders without fullPage prop', () => {
    const { container } = render(<LoadingSpinner />)
    const spinner = container.firstChild as HTMLElement
    expect(spinner).toHaveClass('loading-spinner')
    expect(spinner).not.toHaveClass('loading-spinner--full-page')
  })

  it('adds full-page class when fullPage=true', () => {
    const { container } = render(<LoadingSpinner fullPage />)
    const spinner = container.firstChild as HTMLElement
    expect(spinner).toHaveClass('loading-spinner--full-page')
  })

  it('renders inner spinner element', () => {
    const { container } = render(<LoadingSpinner />)
    expect(container.querySelector('.spinner')).toBeInTheDocument()
  })
})
