import { useEffect, useState } from 'react'
import { previewRule } from '../../api/apispec'
import { listCaptures } from '../../api/captures'
import type { CapturedRequestSummary, PreviewRuleRequest, PreviewRuleResult } from '../../types/api'

interface Props {
  specId: string
  ruleId: string
  ruleName: string
  onClose: () => void
  /** Override the preview function (e.g. for standalone content rules). */
  overridePreview?: (id: string, req: PreviewRuleRequest) => Promise<PreviewRuleResult>
}

type Mode = 'synthetic' | 'capture'

export default function RulePreviewModal({ specId, ruleId, ruleName, onClose, overridePreview }: Props) {
  const doPreview = overridePreview ?? ((id, req) => previewRule(specId, id, req))
  const [mode, setMode] = useState<Mode>('synthetic')

  // synthetic mode
  const [syntheticContentType, setSyntheticContentType] = useState('application/json')
  const [syntheticBody, setSyntheticBody] = useState('{"example":"data"}')
  const [syntheticHost, setSyntheticHost] = useState('example.com')
  const [syntheticPath, setSyntheticPath] = useState('/')

  // capture mode
  const [captures, setCaptures] = useState<CapturedRequestSummary[]>([])
  const [captureSearch, setCaptureSearch] = useState('')
  const [selectedCaptureId, setSelectedCaptureId] = useState<string | null>(null)
  const [capturesLoading, setCapturesLoading] = useState(false)

  const [result, setResult] = useState<PreviewRuleResult | null>(null)
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    if (mode !== 'capture') return
    setCapturesLoading(true)
    listCaptures({ pageSize: 200 })
      .then((r) => setCaptures(r.items))
      .catch((e) => setError((e as Error).message))
      .finally(() => setCapturesLoading(false))
  }, [mode])

  const filteredCaptures = captures.filter((c) => {
    const q = captureSearch.toLowerCase()
    if (!q) return true
    return (
      c.host?.toLowerCase().includes(q) ||
      c.path?.toLowerCase().includes(q) ||
      c.method?.toLowerCase().includes(q)
    )
  })

  const selectedCapture = captures.find((c) => c.id === selectedCaptureId) ?? null

  const handleRun = async () => {
    setLoading(true)
    setError(null)
    setResult(null)
    try {
      if (mode === 'capture') {
        if (!selectedCaptureId) { setError('Select a capture first.'); setLoading(false); return }
        const r = await doPreview(ruleId, { captureId: selectedCaptureId })
        setResult(r)
      } else {
        const r = await doPreview(ruleId, {
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
      }
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
          width: 'min(760px, 96vw)',
          maxHeight: '92vh',
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

        {/* Mode toggle */}
        <div style={{ display: 'flex', gap: 4, marginBottom: 16 }}>
          {(['synthetic', 'capture'] as const).map((m) => (
            <button
              key={m}
              onClick={() => { setMode(m); setResult(null); setError(null) }}
              style={{
                padding: '4px 14px',
                fontSize: 12,
                borderRadius: 4,
                border: '1px solid #4a4a6a',
                background: mode === m ? '#4a4a6a' : 'transparent',
                color: mode === m ? '#fff' : '#aaa',
                cursor: 'pointer',
              }}
            >
              {m === 'synthetic' ? 'Synthetic payload' : 'From capture'}
            </button>
          ))}
        </div>

        {mode === 'synthetic' && (
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
        )}

        {mode === 'capture' && (
          <div>
            <input
              value={captureSearch}
              onChange={(e) => setCaptureSearch(e.target.value)}
              placeholder="Filter by host, path, or method..."
              style={{ width: '100%', marginBottom: 8 }}
            />
            {capturesLoading && <div style={{ fontSize: 12, color: '#888' }}>Loading captures...</div>}
            <div
              style={{
                maxHeight: 200,
                overflowY: 'auto',
                border: '1px solid #4a4a6a',
                borderRadius: 4,
                fontSize: 12,
              }}
            >
              {filteredCaptures.length === 0 && !capturesLoading && (
                <div style={{ padding: 12, color: '#666' }}>No captures found.</div>
              )}
              {filteredCaptures.map((c) => (
                <div
                  key={c.id}
                  onClick={() => setSelectedCaptureId(c.id)}
                  style={{
                    padding: '6px 10px',
                    cursor: 'pointer',
                    background: selectedCaptureId === c.id ? '#2a2a4e' : 'transparent',
                    borderBottom: '1px solid #2a2a3e',
                    display: 'flex',
                    gap: 8,
                    alignItems: 'center',
                  }}
                >
                  <span
                    style={{
                      fontFamily: 'monospace',
                      fontSize: 10,
                      padding: '1px 5px',
                      borderRadius: 3,
                      background: '#3a3a5a',
                      color: '#c084fc',
                    }}
                  >
                    {c.method}
                  </span>
                  <span style={{ color: '#ccc' }}>{c.host}</span>
                  <span style={{ color: '#888', overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>
                    {c.path}
                  </span>
                  {c.statusCode != null && (
                    <span style={{ marginLeft: 'auto', color: c.statusCode >= 400 ? '#fca5a5' : '#86efac', fontFamily: 'monospace', fontSize: 11 }}>
                      {c.statusCode}
                    </span>
                  )}
                </div>
              ))}
            </div>
            {selectedCapture && (
              <div style={{ marginTop: 8, padding: 8, background: '#0f0f1a', borderRadius: 4, fontSize: 11, color: '#aaa' }}>
                Selected: <strong style={{ color: '#ccc' }}>{selectedCapture.method} {selectedCapture.host}{selectedCapture.path}</strong>
                {' '}· status {selectedCapture.statusCode} · {selectedCapture.responseBodySize} B response
              </div>
            )}
          </div>
        )}

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
              <strong>Matched:</strong> {result.matched ? '✔' : '—'} ·{' '}
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
