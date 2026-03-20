import { render, screen } from '@testing-library/react'
import { describe, it, expect } from 'vitest'
import HeadersViewer from '../components/common/HeadersViewer'

describe('HeadersViewer', () => {
  it('renders header names and values in a table', () => {
    const headers = JSON.stringify({ 'Content-Type': 'application/json', 'X-Custom': 'value' })
    render(<HeadersViewer headersJson={headers} />)

    expect(screen.getByText('Content-Type')).toBeInTheDocument()
    expect(screen.getByText('application/json')).toBeInTheDocument()
    expect(screen.getByText('X-Custom')).toBeInTheDocument()
    expect(screen.getByText('value')).toBeInTheDocument()
  })

  it('shows "No headers" when object is empty', () => {
    render(<HeadersViewer headersJson="{}" />)
    expect(screen.getByText('No headers')).toBeInTheDocument()
  })

  it('renders raw text when JSON is invalid', () => {
    const invalid = 'not-valid-json'
    render(<HeadersViewer headersJson={invalid} />)
    expect(screen.getByText(invalid)).toBeInTheDocument()
  })

  it('joins array header values with comma', () => {
    const headers = JSON.stringify({ 'Accept': ['text/html', 'application/json'] })
    render(<HeadersViewer headersJson={headers} />)
    expect(screen.getByText('text/html, application/json')).toBeInTheDocument()
  })

  it('renders table with Name and Value column headers', () => {
    const headers = JSON.stringify({ 'Authorization': 'Bearer token' })
    render(<HeadersViewer headersJson={headers} />)
    expect(screen.getByText('Name')).toBeInTheDocument()
    expect(screen.getByText('Value')).toBeInTheDocument()
  })
})
