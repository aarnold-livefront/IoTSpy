import { useState } from 'react'
import type {
  FuzzerJob,
  FuzzerResult,
  StartFuzzerRequest,
  FuzzerStrategy,
  CapturedRequestSummary,
} from '../../types/api'
import '../../styles/manipulation.css'

interface Props {
  jobs: FuzzerJob[]
  selectedResults: FuzzerResult[]
  loading: boolean
  error: string | null
  captures: CapturedRequestSummary[]
  onStart: (req: StartFuzzerRequest) => Promise<FuzzerJob | null>
  onViewResults: (id: string) => void
  onCancel: (id: string) => void
  onDelete: (id: string) => void
}

const STRATEGIES: FuzzerStrategy[] = ['Random', 'Boundary', 'BitFlip']

const statusClass: Record<string, string> = {
  Pending: 'fuzzer-status--pending',
  Running: 'fuzzer-status--running',
  Completed: 'fuzzer-status--completed',
  Failed: 'fuzzer-status--failed',
  Cancelled: 'fuzzer-status--cancelled',
}

export default function FuzzerPanel({
  jobs,
  selectedResults,
  loading,
  error,
  captures,
  onStart,
  onViewResults,
  onCancel,
  onDelete,
}: Props) {
  const [captureId, setCaptureId] = useState('')
  const [strategy, setStrategy] = useState<FuzzerStrategy>('Random')
  const [mutationCount, setMutationCount] = useState(50)
  const [concurrent, setConcurrent] = useState(5)
  const [starting, setStarting] = useState(false)
  const [viewingJobId, setViewingJobId] = useState<string | null>(null)

  const handleStart = async () => {
    if (!captureId) return
    setStarting(true)
    const req: StartFuzzerRequest = {
      captureId,
      strategy,
      mutationCount,
      concurrentRequests: concurrent,
    }
    await onStart(req)
    setStarting(false)
  }

  const handleViewResults = (id: string) => {
    setViewingJobId(id)
    onViewResults(id)
  }

  return (
    <div className="manip-section">
      <div className="manip-section__header">
        <span className="manip-section__title">
          Mutation Fuzzer {loading && <span className="manip-spinner" />}
        </span>
      </div>

      {error && <div className="manip-error">{error}</div>}

      {/* Fuzzer config form */}
      <div className="manip-form">
        <div className="manip-form__row">
          <label className="manip-form__label">
            Source Capture
            <select
              className="manip-form__select"
              value={captureId}
              onChange={(e) => setCaptureId(e.target.value)}
            >
              <option value="">Select a captured request...</option>
              {captures.map((c) => (
                <option key={c.id} value={c.id}>
                  {c.method} {c.host}{c.path} ({c.statusCode})
                </option>
              ))}
            </select>
          </label>
          <label className="manip-form__label">
            Strategy
            <select
              className="manip-form__select"
              value={strategy}
              onChange={(e) => setStrategy(e.target.value as FuzzerStrategy)}
            >
              {STRATEGIES.map((s) => <option key={s} value={s}>{s}</option>)}
            </select>
          </label>
          <label className="manip-form__label">
            Mutations
            <input
              className="manip-form__input manip-form__input--narrow"
              type="number"
              value={mutationCount}
              onChange={(e) => setMutationCount(Number(e.target.value))}
              min={1}
              max={10000}
            />
          </label>
          <label className="manip-form__label">
            Concurrent
            <input
              className="manip-form__input manip-form__input--narrow"
              type="number"
              value={concurrent}
              onChange={(e) => setConcurrent(Number(e.target.value))}
              min={1}
              max={50}
            />
          </label>
          <button
            className="manip-btn manip-btn--primary"
            onClick={handleStart}
            disabled={!captureId || starting}
          >
            {starting ? 'Starting...' : 'Start Fuzzer'}
          </button>
        </div>
      </div>

      {/* Fuzzer jobs list */}
      <div className="manip-table">
        <div className="manip-section__subtitle">Fuzzer Jobs</div>
        {jobs.length === 0 ? (
          <div className="manip-empty">No fuzzer jobs yet.</div>
        ) : (
          jobs.map((job) => (
            <div
              key={job.id}
              className={`manip-row manip-row--clickable ${viewingJobId === job.id ? 'manip-row--selected' : ''}`}
              onClick={() => handleViewResults(job.id)}
            >
              <div className="manip-row__main">
                <span className={`fuzzer-status ${statusClass[job.status] ?? ''}`}>
                  {job.status}
                </span>
                <span className={`manip-badge manip-badge--${job.strategy.toLowerCase()}`}>
                  {job.strategy}
                </span>
                <span className="manip-row__meta">
                  {job.completedRequests}/{job.mutationCount} mutations
                </span>
                {job.anomaliesFound > 0 && (
                  <span className="fuzzer-anomaly-count">
                    {job.anomaliesFound} anomal{job.anomaliesFound === 1 ? 'y' : 'ies'}
                  </span>
                )}
              </div>
              <div className="manip-row__actions">
                {(job.status === 'Running' || job.status === 'Pending') && (
                  <button
                    className="manip-btn manip-btn--small manip-btn--danger"
                    onClick={(e) => { e.stopPropagation(); onCancel(job.id) }}
                  >
                    Cancel
                  </button>
                )}
                {(job.status === 'Completed' || job.status === 'Failed' || job.status === 'Cancelled') && (
                  <button
                    className="manip-btn manip-btn--small manip-btn--ghost"
                    onClick={(e) => { e.stopPropagation(); onDelete(job.id) }}
                  >
                    Delete
                  </button>
                )}
              </div>
            </div>
          ))
        )}
      </div>

      {/* Fuzzer results table */}
      {viewingJobId && selectedResults.length > 0 && (
        <div className="fuzzer-results">
          <div className="manip-section__subtitle">
            Results ({selectedResults.length})
          </div>
          <div className="fuzzer-results__table">
            <div className="fuzzer-results__header">
              <span>#</span>
              <span>Status</span>
              <span>Duration</span>
              <span>Anomaly</span>
              <span>Reason</span>
            </div>
            {selectedResults.map((r) => (
              <div
                key={r.id}
                className={`fuzzer-results__row ${r.isAnomaly ? 'fuzzer-results__row--anomaly' : ''}`}
              >
                <span className="fuzzer-results__index">{r.mutationIndex}</span>
                <span className={`replay-status replay-status--${Math.floor(r.responseStatusCode / 100)}xx`}>
                  {r.responseStatusCode}
                </span>
                <span className="fuzzer-results__duration">{r.durationMs}ms</span>
                <span className={`fuzzer-results__anomaly ${r.isAnomaly ? 'fuzzer-results__anomaly--yes' : ''}`}>
                  {r.isAnomaly ? 'YES' : '-'}
                </span>
                <span className="fuzzer-results__reason">
                  {r.anomalyReason ?? '-'}
                </span>
              </div>
            ))}
          </div>
        </div>
      )}
    </div>
  )
}
