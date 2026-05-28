import { useCallback, useEffect, useState } from 'react'
import { getTriageQueue, reviewInsight, getAnalyticsStats } from '../../api/analytics'
import type { AnalyticsStats, TrafficInsight } from '../../types/analytics'
import { parseTags, parseConfidence } from '../../types/analytics'
import RiskTagBadge from './RiskTagBadge'
import './InsightTriagePanel.css'

export default function InsightTriagePanel() {
  const [items, setItems] = useState<TrafficInsight[]>([])
  const [stats, setStats] = useState<AnalyticsStats | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [unreviewedOnly, setUnreviewedOnly] = useState(true)
  const [page, setPage] = useState(1)
  const [total, setTotal] = useState(0)
  const [reviewing, setReviewing] = useState<string | null>(null)

  const PAGE_SIZE = 50

  const load = useCallback(async () => {
    setLoading(true)
    setError(null)
    try {
      const [triageRes, statsRes] = await Promise.all([
        getTriageQueue(page, PAGE_SIZE, unreviewedOnly),
        getAnalyticsStats()
      ])
      setItems(triageRes.items)
      setTotal(triageRes.total)
      setStats(statsRes)
    } catch (e) {
      setError('Failed to load analytics data.')
    } finally {
      setLoading(false)
    }
  }, [page, unreviewedOnly])

  useEffect(() => { load() }, [load])

  const handleReview = async (id: string, dismissed: boolean) => {
    setReviewing(id)
    try {
      await reviewInsight(id, dismissed)
      await load()
    } finally {
      setReviewing(null)
    }
  }

  return (
    <div className="insight-triage">
      <div className="insight-triage__header">
        <div className="insight-triage__title">
          <h2>ML Risk Triage</h2>
          {stats && (
            <span className="insight-triage__badge">{stats.unreviewed} unreviewed</span>
          )}
        </div>
        <label className="insight-triage__filter">
          <input
            type="checkbox"
            checked={unreviewedOnly}
            onChange={e => { setUnreviewedOnly(e.target.checked); setPage(1) }}
          />
          Unreviewed only
        </label>
      </div>

      {error && <div className="insight-triage__error">{error}</div>}

      {loading ? (
        <div className="insight-triage__loading">Loading insights…</div>
      ) : items.length === 0 ? (
        <div className="insight-triage__empty">
          {unreviewedOnly ? 'No unreviewed findings — all clear.' : 'No insights recorded yet.'}
        </div>
      ) : (
        <>
          <table className="insight-triage__table">
            <thead>
              <tr>
                <th>Risk</th>
                <th>Tags</th>
                <th>Host / Path</th>
                <th>Source</th>
                <th>When</th>
                <th>Actions</th>
              </tr>
            </thead>
            <tbody>
              {items.map(insight => {
                const tags = parseTags(insight)
                const conf = parseConfidence(insight)
                return (
                  <tr key={insight.id} className={insight.isDismissed ? 'insight-triage__row--dismissed' : ''}>
                    <td>
                      <div className="insight-triage__score">
                        <div
                          className="insight-triage__score-bar"
                          style={{ width: `${Math.round(insight.riskScore * 100)}%` }}
                        />
                        <span>{Math.round(insight.riskScore * 100)}%</span>
                      </div>
                    </td>
                    <td>
                      <div className="insight-triage__tags">
                        {tags.map(tag => (
                          <RiskTagBadge key={tag} tag={tag} confidence={conf[tag]} />
                        ))}
                      </div>
                    </td>
                    <td className="insight-triage__capture-id">
                      <code>{insight.captureId.slice(0, 8)}…</code>
                    </td>
                    <td>
                      <span className={`insight-triage__source insight-triage__source--${insight.source}`}>
                        {insight.source}
                      </span>
                    </td>
                    <td className="insight-triage__date">
                      {new Date(insight.createdAt).toLocaleString()}
                    </td>
                    <td>
                      {!insight.isReviewed && (
                        <div className="insight-triage__actions">
                          <button
                            className="insight-triage__btn insight-triage__btn--confirm"
                            disabled={reviewing === insight.id}
                            onClick={() => handleReview(insight.id, false)}
                          >
                            Confirm
                          </button>
                          <button
                            className="insight-triage__btn insight-triage__btn--dismiss"
                            disabled={reviewing === insight.id}
                            onClick={() => handleReview(insight.id, true)}
                          >
                            Dismiss
                          </button>
                        </div>
                      )}
                      {insight.isReviewed && (
                        <span className="insight-triage__reviewed">
                          {insight.isDismissed ? 'Dismissed' : 'Confirmed'}
                        </span>
                      )}
                    </td>
                  </tr>
                )
              })}
            </tbody>
          </table>

          {total > PAGE_SIZE && (
            <div className="insight-triage__pagination">
              <button disabled={page === 1} onClick={() => setPage(p => p - 1)}>Prev</button>
              <span>Page {page} of {Math.ceil(total / PAGE_SIZE)}</span>
              <button disabled={page >= Math.ceil(total / PAGE_SIZE)} onClick={() => setPage(p => p + 1)}>Next</button>
            </div>
          )}
        </>
      )}
    </div>
  )
}
