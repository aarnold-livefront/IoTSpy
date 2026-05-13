import { useEffect, useRef, useState } from 'react'
import { previewRule } from '../../api/apispec'
import { listCaptures } from '../../api/captures'
import { useFocusTrap } from '../../hooks/useFocusTrap'
import type { CapturedRequestSummary, PreviewRuleRequest, PreviewRuleResult } from '../../types/api'
import '../../styles/modal.css'

interface Props {
  specId: string
  ruleId: string
  ruleName: string
  onClose: () => void
  overridePreview?: (id: string, req: PreviewRuleRequest) => Promise<PreviewRuleResult>
}

type Mode = 'synthetic' | 'capture'

export default function RulePreviewModal({ specId, ruleId, ruleName, onClose, overridePreview }: Props) {
  const doPreview = overridePreview ?? ((id, req) => previewRule(specId, id, req))
  const [mode, setMode] = useState<Mode>('synthetic')

  const [syntheticContentType, setSyntheticContentType] = useState('application/json')
  const [syntheticBody, setSyntheticBody] = useState('{"example":"data"}')
  const [syntheticHost, setSyntheticHost] = useState('example.com')
  const [syntheticPath, setSyntheticPath] = useState('/')

  const [captures, setCaptures] = useState<CapturedRequestSummary[]>([])
  const [captureSearch, setCaptureSearch] = useState('')
  const [selectedCaptureId, setSelectedCaptureId] = useState<string | null>(null)
  const [capturesLoading, setCapturesLoading] = useState(false)

  const [result, setResult] = useState<PreviewRuleResult | null>(null)
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const dialogRef = useRef<HTMLDivElement>(null)
  useFocusTrap(dialogRef)

  useEffect(() => {
    const handle = (e: KeyboardEvent) => { if (e.key === 'Escape') onClose() }
    document.addEventListener('keydown', handle)
    return () => document.removeEventListener('keydown', handle)
  }, [onClose])

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
        setResult(await doPreview(ruleId, { captureId: selectedCaptureId }))
      } else {
        setResult(await doPreview(ruleId, {
          synthetic: {
            method: 'GET',
            host: syntheticHost,
            path: syntheticPath,
            statusCode: 200,
            responseHeaders: { 'Content-Type': syntheticContentType },
            responseBody: syntheticBody,
          },
        }))
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
      className="modal-overlay"
      onClick={onClose}
      role="dialog"
      aria-modal="true"
      aria-labelledby="rule-preview-title"
    >
      <div
        ref={dialogRef}
        className="modal preview-modal"
        onClick={(e) => e.stopPropagation()}
      >
        <div className="modal__header">
          <span className="modal__title" id="rule-preview-title">Preview rule: {ruleName}</span>
          <button className="btn btn--secondary" onClick={onClose}>Close</button>
        </div>

        <div className="modal__body">
          {/* Mode toggle */}
          <div className="preview-modal__mode-toggle" role="group" aria-label="Preview mode">
            {(['synthetic', 'capture'] as const).map((m) => (
              <button
                key={m}
                onClick={() => { setMode(m); setResult(null); setError(null) }}
                className={`preview-modal__mode-btn${mode === m ? ' preview-modal__mode-btn--active' : ''}`}
              >
                {m === 'synthetic' ? 'Synthetic payload' : 'From capture'}
              </button>
            ))}
          </div>

          {mode === 'synthetic' && (
            <div className="preview-modal__form-grid">
              <label>
                <span className="preview-modal__form-hint">Host</span>
                <input value={syntheticHost} onChange={(e) => setSyntheticHost(e.target.value)} style={{ width: '100%' }} />
              </label>
              <label>
                <span className="preview-modal__form-hint">Path</span>
                <input value={syntheticPath} onChange={(e) => setSyntheticPath(e.target.value)} style={{ width: '100%' }} />
              </label>
              <label className="preview-modal__form-grid--full">
                <span className="preview-modal__form-hint">Response Content-Type</span>
                <input value={syntheticContentType} onChange={(e) => setSyntheticContentType(e.target.value)} style={{ width: '100%' }} />
              </label>
              <label className="preview-modal__form-grid--full">
                <span className="preview-modal__form-hint">Response body</span>
                <textarea
                  value={syntheticBody}
                  onChange={(e) => setSyntheticBody(e.target.value)}
                  rows={4}
                  style={{ width: '100%', fontFamily: 'var(--font-mono)', fontSize: 'var(--font-size-sm)' }}
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
                className="preview-modal__capture-search"
                style={{ width: '100%', marginBottom: 'var(--space-2)' }}
              />
              {capturesLoading && <div className="preview-modal__loading">Loading captures...</div>}
              <div
                role="listbox"
                aria-label="Select a capture"
                className="preview-modal__capture-list"
              >
                {filteredCaptures.length === 0 && !capturesLoading && (
                  <div className="preview-modal__capture-empty">No captures found.</div>
                )}
                {filteredCaptures.map((c) => (
                  <div
                    key={c.id}
                    role="option"
                    aria-selected={selectedCaptureId === c.id}
                    tabIndex={0}
                    onClick={() => setSelectedCaptureId(c.id)}
                    onKeyDown={(e) => {
                      if (e.key === 'Enter' || e.key === ' ') { e.preventDefault(); setSelectedCaptureId(c.id) }
                    }}
                    className={`preview-modal__capture-item${selectedCaptureId === c.id ? ' preview-modal__capture-item--selected' : ''}`}
                  >
                    <span className="preview-modal__method-badge">{c.method}</span>
                    <span className="preview-modal__capture-host">{c.host}</span>
                    <span className="preview-modal__capture-path">{c.path}</span>
                    {c.statusCode != null && (
                      <span className={`preview-modal__capture-status${c.statusCode >= 400 ? ' preview-modal__capture-status--err' : ' preview-modal__capture-status--ok'}`}>
                        {c.statusCode}
                      </span>
                    )}
                  </div>
                ))}
              </div>
              {selectedCapture && (
                <div className="preview-modal__capture-summary">
                  Selected: <strong>{selectedCapture.method} {selectedCapture.host}{selectedCapture.path}</strong>
                  {' '}· status {selectedCapture.statusCode} · {selectedCapture.responseBodySize} B response
                </div>
              )}
            </div>
          )}

          <div className="preview-modal__actions">
            <button className="btn btn--primary" onClick={() => void handleRun()} disabled={loading}>
              {loading ? 'Running...' : 'Run preview'}
            </button>
          </div>

          {error && <div className="modal-error">{error}</div>}

          {result && (
            <div>
              <div className="preview-modal__result-meta">
                <strong>Status:</strong> {result.statusCode} ·{' '}
                <strong>Matched:</strong> {result.matched ? '✔' : '—'} ·{' '}
                <strong>Modified:</strong> {result.modified ? '✔' : '—'} ·{' '}
                <strong>Bytes:</strong> {result.bodyLength} ·{' '}
                <strong>CT:</strong> {result.contentType || '(none)'}
                {result.wasStreamed && ' · streamed'}
              </div>

              {result.warnings.length > 0 && (
                <div className="preview-modal__warnings">
                  <strong>Warnings:</strong>
                  <ul>{result.warnings.map((w, i) => <li key={i}>{w}</li>)}</ul>
                </div>
              )}

              <details open>
                <summary className="preview-modal__summary-detail">
                  Response headers ({Object.keys(result.responseHeaders).length})
                </summary>
                <pre className="preview-modal__pre">
                  {Object.entries(result.responseHeaders).map(([k, v]) => `${k}: ${v}`).join('\n')}
                </pre>
              </details>

              <details open style={{ marginTop: 'var(--space-2)' }}>
                <summary className="preview-modal__summary-detail">Response body</summary>
                {isImagePreview ? (
                  <img
                    src={`data:${result.contentType};base64,${result.responseBodyBase64}`}
                    alt="preview"
                    style={{ maxWidth: '100%', marginTop: 'var(--space-2)', borderRadius: 'var(--radius-sm)' }}
                  />
                ) : result.responseBodyText ? (
                  <pre className="preview-modal__pre preview-modal__pre--body">{result.responseBodyText}</pre>
                ) : result.responseBodyBase64 ? (
                  <div className="preview-modal__binary-note">Binary body ({result.bodyLength} bytes, base64 elided)</div>
                ) : (
                  <div className="preview-modal__binary-note">Empty body</div>
                )}
              </details>
            </div>
          )}
        </div>
      </div>
    </div>
  )
}
