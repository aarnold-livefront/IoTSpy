import { useState } from 'react'
import { previewRule } from '../../api/apispec'
import type { PreviewRuleResult } from '../../types/api'

interface Props {
  specId: string
  ruleId: string
  ruleName: string
  onClose: () => void
}

export default function RulePreviewModal({ specId, ruleId, ruleName, onClose }: Props) {
  const [syntheticContentType, setSyntheticContentType] = useState('application/json')
  const [syntheticBody, setSyntheticBody] = useState('{"example":"data"}')
  const [syntheticHost, setSyntheticHost] = useState('example.com')
  const [syntheticPath, setSyntheticPath] = useState('/')
  const [result, setResult] = useState<PreviewRuleResult | null>(null)
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const handleRun = async () => {
    setLoading(true)
    setError(null)
    try {
      const r = await previewRule(specId, ruleId, {
        synthetic: {
          method: 'GET',
          host: syntheticHost,
          path: syntheticPath,
          statusCode: 200,
          responseHeaders: { 'Content-Type': syntheticContentType },
          responseBody: syntheticBody,
        },
      })
      setResult(r)
    } catch (e) {
      setError((e as Error).message)
    } finally {
      setLoading(false)
    }
  }

  const isImagePreview = result?.contentType?.startsWith('image/') && result.responseBodyBase64

  return (
    <div
      onClick={onClose}
      style={{
        position: 'fixed',
        inset: 0,
        background: 'rgba(0,0,0,0.6)',
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        zIndex: 1000,
      }}
    >
      <div
        onClick={(e) => e.stopPropagation()}
        style={{
          width: 'min(720px, 94vw)',
          maxHeight: '90vh',
          overflow: 'auto',
          background: '#1a1a2e',
          border: '1px solid #4a4a6a',
          borderRadius: 8,
          padding: 20,
        }}
      >
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 12 }}>
          <h3 style={{ margin: 0 }}>Preview rule: {ruleName}</h3>
          <button className="btn btn--secondary" onClick={onClose}>Close</button>
        </div>

        <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 8 }}>
          <label>
            <span style={{ fontSize: 11, color: '#aaa' }}>Host</span>
            <input value={syntheticHost} onChange={(e) => setSyntheticHost(e.target.value)} style={{ width: '100%' }} />
          </label>
          <label>
            <span style={{ fontSize: 11, color: '#aaa' }}>Path</span>
            <input value={syntheticPath} onChange={(e) => setSyntheticPath(e.target.value)} style={{ width: '100%' }} />
          </label>
          <label style={{ gridColumn: '1 / -1' }}>
            <span style={{ fontSize: 11, color: '#aaa' }}>Response Content-Type</span>
            <input
              value={syntheticContentType}
              onChange={(e) => setSyntheticContentType(e.target.value)}
              style={{ width: '100%' }}
            />
          </label>
          <label style={{ gridColumn: '1 / -1' }}>
            <span style={{ fontSize: 11, color: '#aaa' }}>Response body</span>
            <textarea
              value={syntheticBody}
              onChange={(e) => setSyntheticBody(e.target.value)}
              rows={4}
              style={{ width: '100%', fontFamily: 'monospace', fontSize: 12 }}
            />
          </label>
        </div>

        <div style={{ marginTop: 12 }}>
          <button className="btn btn--primary" onClick={() => void handleRun()} disabled={loading}>
            {loading ? 'Running...' : 'Run preview'}
          </button>
        </div>

        {error && (
          <div style={{ marginTop: 12, padding: 8, background: '#3a1a1a', color: '#fca5a5', borderRadius: 4 }}>
            {error}
          </div>
        )}

        {result && (
          <div style={{ marginTop: 16 }}>
            <div style={{ fontSize: 13, marginBottom: 6 }}>
              <strong>Status:</strong> {result.statusCode} ·{' '}
              <strong>Modified:</strong> {result.modified ? '✔' : '—'} ·{' '}
              <strong>Bytes:</strong> {result.bodyLength} ·{' '}
              <strong>CT:</strong> {result.contentType || '(none)'}
              {result.wasStreamed && ' · streamed'}
            </div>

            {result.warnings.length > 0 && (
              <div style={{ padding: 8, background: '#3a2a1a', borderRadius: 4, fontSize: 12, marginBottom: 8 }}>
                <strong>Warnings:</strong>
                <ul style={{ margin: '4px 0 0 16px' }}>
                  {result.warnings.map((w, i) => <li key={i}>{w}</li>)}
                </ul>
              </div>
            )}

            <details open>
              <summary style={{ cursor: 'pointer', fontSize: 12, color: '#aaa' }}>
                Response headers ({Object.keys(result.responseHeaders).length})
              </summary>
              <pre style={{
                fontSize: 11,
                margin: '4px 0 0 0',
                padding: 8,
                background: '#0f0f1a',
                borderRadius: 4,
                maxHeight: 120,
                overflow: 'auto',
              }}>
                {Object.entries(result.responseHeaders).map(([k, v]) => `${k}: ${v}`).join('\n')}
              </pre>
            </details>

            <details open style={{ marginTop: 8 }}>
              <summary style={{ cursor: 'pointer', fontSize: 12, color: '#aaa' }}>Response body</summary>
              {isImagePreview ? (
                <img
                  src={`data:${result.contentType};base64,${result.responseBodyBase64}`}
                  alt="preview"
                  style={{ maxWidth: '100%', marginTop: 8, borderRadius: 4 }}
                />
              ) : result.responseBodyText ? (
                <pre style={{
                  fontSize: 11,
                  margin: '4px 0 0 0',
                  padding: 8,
                  background: '#0f0f1a',
                  borderRadius: 4,
                  maxHeight: 240,
                  overflow: 'auto',
                  whiteSpace: 'pre-wrap',
                }}>
                  {result.responseBodyText}
                </pre>
              ) : result.responseBodyBase64 ? (
                <div style={{ fontSize: 11, color: '#777', padding: 8 }}>
                  Binary body ({result.bodyLength} bytes, base64 elided)
                </div>
              ) : (
                <div style={{ fontSize: 11, color: '#777', padding: 8 }}>Empty body</div>
              )}
            </details>
          </div>
        )}
      </div>
    </div>
  )
}
