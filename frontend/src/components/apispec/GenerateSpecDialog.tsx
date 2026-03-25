import { useState } from 'react'
import type { GenerateSpecRequest } from '../../types/api'

interface Props {
  onGenerate: (req: GenerateSpecRequest) => Promise<void>
  onCancel: () => void
}

export default function GenerateSpecDialog({ onGenerate, onCancel }: Props) {
  const [host, setHost] = useState('')
  const [name, setName] = useState('')
  const [pathPattern, setPathPattern] = useState('')
  const [method, setMethod] = useState('')
  const [useLlm, setUseLlm] = useState(false)
  const [loading, setLoading] = useState(false)

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault()
    if (!host.trim()) return

    setLoading(true)
    try {
      await onGenerate({
        host: host.trim(),
        name: name.trim() || undefined,
        pathPattern: pathPattern.trim() || undefined,
        method: method.trim() || undefined,
        useLlmAnalysis: useLlm,
      })
    } finally {
      setLoading(false)
    }
  }

  return (
    <div
      style={{
        padding: 16,
        background: '#1a1a2e',
        borderRadius: 8,
        border: '1px solid #4a4a6a',
        marginBottom: 16,
      }}
    >
      <h4 style={{ margin: '0 0 12px' }}>Generate API Spec from Traffic</h4>
      <form onSubmit={(e) => void handleSubmit(e)}>
        <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 8 }}>
          <label>
            <span style={{ fontSize: 12, color: '#aaa' }}>Host (required)</span>
            <input
              type="text"
              value={host}
              onChange={(e) => setHost(e.target.value)}
              placeholder="api.example.com"
              required
              style={{ width: '100%' }}
            />
          </label>
          <label>
            <span style={{ fontSize: 12, color: '#aaa' }}>Spec Name</span>
            <input
              type="text"
              value={name}
              onChange={(e) => setName(e.target.value)}
              placeholder="My API Spec"
              style={{ width: '100%' }}
            />
          </label>
          <label>
            <span style={{ fontSize: 12, color: '#aaa' }}>Path Pattern (regex)</span>
            <input
              type="text"
              value={pathPattern}
              onChange={(e) => setPathPattern(e.target.value)}
              placeholder="/api/.*"
              style={{ width: '100%' }}
            />
          </label>
          <label>
            <span style={{ fontSize: 12, color: '#aaa' }}>Method Filter</span>
            <select value={method} onChange={(e) => setMethod(e.target.value)} style={{ width: '100%' }}>
              <option value="">All Methods</option>
              <option value="GET">GET</option>
              <option value="POST">POST</option>
              <option value="PUT">PUT</option>
              <option value="PATCH">PATCH</option>
              <option value="DELETE">DELETE</option>
            </select>
          </label>
        </div>
        <label style={{ display: 'flex', alignItems: 'center', gap: 6, marginTop: 8 }}>
          <input type="checkbox" checked={useLlm} onChange={(e) => setUseLlm(e.target.checked)} />
          <span style={{ fontSize: 13 }}>Enhance with LLM analysis</span>
        </label>
        <div style={{ display: 'flex', gap: 8, marginTop: 12 }}>
          <button type="submit" className="btn btn--primary" disabled={loading || !host.trim()}>
            {loading ? 'Generating...' : 'Generate'}
          </button>
          <button type="button" className="btn btn--secondary" onClick={onCancel}>
            Cancel
          </button>
        </div>
      </form>
    </div>
  )
}
