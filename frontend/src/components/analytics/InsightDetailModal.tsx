import { useEffect, useState } from 'react'
import { getCapture } from '../../api/captures'
import { reviewInsight } from '../../api/analytics'
import type { TrafficInsight } from '../../types/analytics'
import { parseTags, parseConfidence } from '../../types/analytics'
import type { CapturedRequest } from '../../types/api'
import RiskTagBadge from './RiskTagBadge'
import './InsightDetailModal.css'

const PREVIEW_LIMIT = 600

interface Props {
  insight: TrafficInsight
  onClose: () => void
  onReviewed: () => void
  onOpenCapture?: (captureId: string) => void
}

function statusClass(code: number): string {
  if (code < 300) return 'ok'
  if (code < 400) return 'redirect'
  if (code < 500) return 'client-error'
  return 'server-error'
}

function BodyPreview({ label, body, size }: { label: string; body: string; size: number }) {
  const [expanded, setExpanded] = useState(false)

  if (!body && size === 0) return null

  const truncated = body && body.length > PREVIEW_LIMIT && !expanded
  const displayText = body
    ? (truncated ? body.slice(0, PREVIEW_LIMIT) : body)
    : null

  return (
    <div className="insight-modal__body-preview">
      <div className="insight-modal__body-preview-label">
        {label}
        {body && size > 0 && (
          <span className="insight-modal__body-size">
            {size < 1024 ? `${size}b` : `${(size / 1024).toFixed(1)}k`}
          </span>
        )}
      </div>
      {displayText ? (
        <>
          <pre className="insight-modal__body-pre">{displayText}{truncated ? '…' : ''}</pre>
          {body.length > PREVIEW_LIMIT && (
            <button
              className="insight-modal__body-toggle"
              onClick={() => setExpanded(e => !e)}
            >
              {expanded ? 'Show less' : `Show all ${body.length.toLocaleString()} chars`}
            </button>
          )}
        </>
      ) : (
        <span className="insight-modal__body-empty">
          {size > 0
            ? `${size.toLocaleString()} bytes (body not stored)`
            : 'Empty'}
        </span>
      )}
    </div>
  )
}

export default function InsightDetailModal({ insight, onClose, onReviewed, onOpenCapture }: Props) {
  const [capture, setCapture] = useState<CapturedRequest | null>(null)
  const [captureLoading, setCaptureLoading] = useState(true)
  const [note, setNote] = useState(insight.reviewNote ?? '')
  const [submitting, setSubmitting] = useState(false)

  const tags = parseTags(insight)
  const conf = parseConfidence(insight)

  useEffect(() => {
    setCaptureLoading(true)
    getCapture(insight.captureId)
      .then(setCapture)
      .catch(() => setCapture(null))
      .finally(() => setCaptureLoading(false))
  }, [insight.captureId])

  const handleReview = async (dismissed: boolean) => {
    setSubmitting(true)
    try {
      await reviewInsight(insight.id, dismissed, note || undefined)
      onReviewed()
    } finally {
      setSubmitting(false)
    }
  }

  const handleOpenCapture = () => {
    onOpenCapture?.(insight.captureId)
    onClose()
  }

  const handleOverlayClick = (e: React.MouseEvent<HTMLDivElement>) => {
    if (e.target === e.currentTarget) onClose()
  }

  return (
    <div className="insight-modal-overlay" onClick={handleOverlayClick}>
      <div className="insight-modal" role="dialog" aria-modal="true" aria-label="Insight details">
        <div className="insight-modal__header">
          <h3>Insight Details</h3>
          <button className="insight-modal__close" onClick={onClose} aria-label="Close">&#x2715;</button>
        </div>

        <div className="insight-modal__body">
          {/* Risk score */}
          <div>
            <p className="insight-modal__section-label">Risk Score</p>
            <div className="insight-modal__score-row">
              <div className="insight-modal__score-bar-track">
                <div
                  className="insight-modal__score-bar-fill"
                  style={{ width: `${Math.round(insight.riskScore * 100)}%` }}
                />
              </div>
              <span className="insight-modal__score-value">
                {Math.round(insight.riskScore * 100)}%
              </span>
            </div>
          </div>

          {/* Tags + confidence */}
          {tags.length > 0 && (
            <div>
              <p className="insight-modal__section-label">Risk Signals</p>
              <table className="insight-modal__tags-table">
                <thead>
                  <tr>
                    <th>Tag</th>
                    <th>Confidence</th>
                  </tr>
                </thead>
                <tbody>
                  {tags.map(tag => {
                    const pct = conf[tag] != null ? Math.round(conf[tag] * 100) : null
                    return (
                      <tr key={tag}>
                        <td><RiskTagBadge tag={tag} confidence={conf[tag]} /></td>
                        <td>
                          {pct != null ? (
                            <>
                              <span className="insight-modal__conf-bar-track">
                                <span
                                  className="insight-modal__conf-bar-fill"
                                  style={{ width: `${pct}%` }}
                                />
                              </span>
                              {pct}%
                            </>
                          ) : (
                            <span style={{ color: 'var(--color-text-muted, #888)' }}>—</span>
                          )}
                        </td>
                      </tr>
                    )
                  })}
                </tbody>
              </table>
            </div>
          )}

          {/* Capture summary + payload preview */}
          <div>
            <div className="insight-modal__capture-header">
              <p className="insight-modal__section-label" style={{ margin: 0 }}>Associated Capture</p>
              {capture && onOpenCapture && (
                <button
                  className="insight-modal__open-capture-link"
                  onClick={handleOpenCapture}
                >
                  Open full capture &rarr;
                </button>
              )}
            </div>

            {captureLoading ? (
              <span className="insight-modal__capture-loading">Loading capture…</span>
            ) : capture ? (
              <>
                <div className="insight-modal__capture-grid">
                  <span className="insight-modal__capture-key">Method</span>
                  <span className="insight-modal__method">{capture.method}</span>

                  <span className="insight-modal__capture-key">Host</span>
                  <span className="insight-modal__capture-val">
                    {capture.host}{capture.port !== 80 && capture.port !== 443 ? `:${capture.port}` : ''}
                  </span>

                  <span className="insight-modal__capture-key">Path</span>
                  <span className="insight-modal__capture-val">
                    {capture.path || '/'}{capture.query ? `?${capture.query}` : ''}
                  </span>

                  <span className="insight-modal__capture-key">Status</span>
                  <span className={`insight-modal__status insight-modal__status--${statusClass(capture.statusCode)}`}>
                    {capture.statusCode} {capture.statusMessage}
                  </span>

                  <span className="insight-modal__capture-key">Protocol</span>
                  <span className="insight-modal__capture-val">
                    {capture.isTls ? 'HTTPS' : 'HTTP'}{capture.tlsVersion ? ` / ${capture.tlsVersion}` : ''}
                  </span>
                </div>

                <BodyPreview
                  label="Request Body"
                  body={capture.requestBody}
                  size={capture.requestBodySize}
                />
                <BodyPreview
                  label="Response Body"
                  body={capture.responseBody}
                  size={capture.responseBodySize}
                />
              </>
            ) : (
              <span className="insight-modal__capture-loading">
                Capture not found (ID: {insight.captureId})
              </span>
            )}
          </div>

          {/* Meta */}
          <div className="insight-modal__meta">
            <span>Source: <strong>{insight.source}</strong></span>
            <span>Model: <strong>{insight.modelVersion}</strong></span>
            <span>Detected: <strong>{new Date(insight.createdAt).toLocaleString()}</strong></span>
            {insight.reviewedAt && (
              <span>Reviewed: <strong>{new Date(insight.reviewedAt).toLocaleString()}</strong></span>
            )}
          </div>

          {/* Review note */}
          <div>
            <p className="insight-modal__section-label">
              {insight.isReviewed ? 'Review Note' : 'Add Note (optional)'}
            </p>
            <textarea
              className="insight-modal__note-input"
              value={note}
              onChange={e => setNote(e.target.value)}
              placeholder="Describe why this is or isn't a real finding…"
              readOnly={insight.isReviewed}
            />
          </div>
        </div>

        <div className="insight-modal__footer">
          {insight.isReviewed && (
            <span className="insight-modal__already-reviewed">
              {insight.isDismissed ? 'Dismissed' : 'Confirmed'}
            </span>
          )}
          <button
            className="insight-modal__footer-btn insight-modal__footer-btn--cancel"
            onClick={onClose}
            disabled={submitting}
          >
            Close
          </button>
          {!insight.isReviewed && (
            <>
              <button
                className="insight-modal__footer-btn insight-modal__footer-btn--dismiss"
                onClick={() => handleReview(true)}
                disabled={submitting}
              >
                Dismiss
              </button>
              <button
                className="insight-modal__footer-btn insight-modal__footer-btn--confirm"
                onClick={() => handleReview(false)}
                disabled={submitting}
              >
                Confirm
              </button>
            </>
          )}
        </div>
      </div>
    </div>
  )
}
