import { Component, type ReactNode } from 'react'

interface Props {
  children: ReactNode
  fallback?: ReactNode
}

interface State {
  error: Error | null
}

export default class ErrorBoundary extends Component<Props, State> {
  state: State = { error: null }

  static getDerivedStateFromError(error: Error): State {
    return { error }
  }

  render() {
    if (this.state.error) {
      return this.props.fallback ?? (
        <div style={{ padding: '2rem', color: 'var(--color-status-5xx)', fontFamily: 'var(--font-mono)', fontSize: 'var(--font-size-sm)' }}>
          <strong>Render error:</strong> {this.state.error.message}
        </div>
      )
    }
    return this.props.children
  }
}
