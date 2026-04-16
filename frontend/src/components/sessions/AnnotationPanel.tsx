import { useState } from 'react'
import type { CaptureAnnotation } from '../../types/sessions'
import { addAnnotation, deleteAnnotation } from '../../api/sessions'

interface AnnotationPanelProps {
  sessionId: string
  captureId: string
  annotations: CaptureAnnotation[]
  readOnly?: boolean
  onAdded?: (ann: CaptureAnnotation) => void
  onDeleted?: (id: string) => void
}

export default function AnnotationPanel({
  sessionId,
  captureId,
  annotations,
  readOnly = false,
  onAdded,
  onDeleted,
}: AnnotationPanelProps) {
  const [note, setNote] = useState('')
  const [tags, setTags] = useState('')
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const relevant = annotations.filter(a => a.captureId === captureId)

  const handleAdd = async () => {
    if (!note.trim()) return
    setBusy(true)
    setError(null)
    try {
      const ann = await addAnnotation(sessionId, captureId, note.trim(), tags.trim() || undefined)
      setNote('')
      setTags('')
      onAdded?.(ann)
    } catch {
      setError('Failed to save annotation')
    } finally {
      setBusy(false)
    }
  }

  const handleDelete = async (id: string) => {
    try {
      await deleteAnnotation(sessionId, id)
      onDeleted?.(id)
    } catch {
      // silently fail
    }
  }

  return (
    <div className="annotation-panel">
      <h4 className="annotation-panel__title">Annotations</h4>

      {relevant.length === 0 && (
        <p className="annotation-panel__empty">No annotations yet.</p>
      )}

      {relevant.map((ann) => (
        <div key={ann.id} className="annotation-panel__item">
          <div className="annotation-panel__item-header">
            <span className="annotation-panel__author">{ann.username}</span>
            <span className="annotation-panel__time">
              {new Date(ann.createdAt).toLocaleTimeString()}
            </span>
            {!readOnly && (
              <button
                className="annotation-panel__delete"
                onClick={() => handleDelete(ann.id)}
                title="Delete annotation"
              >
                ×
              </button>
            )}
          </div>
          <p className="annotation-panel__note">{ann.note}</p>
          {ann.tags && (
            <div className="annotation-panel__tags">
              {ann.tags.split(',').map(t => (
                <span key={t} className="badge badge--default">{t.trim()}</span>
              ))}
            </div>
          )}
        </div>
      ))}

      {!readOnly && (
        <div className="annotation-panel__form">
          <textarea
            className="annotation-panel__textarea"
            placeholder="Add a note…"
            value={note}
            onChange={e => setNote(e.target.value)}
            rows={2}
          />
          <input
            className="annotation-panel__tags-input"
            placeholder="Tags (comma-separated)"
            value={tags}
            onChange={e => setTags(e.target.value)}
          />
          {error && <p className="annotation-panel__error">{error}</p>}
          <button
            className="btn btn--primary"
            onClick={handleAdd}
            disabled={busy || !note.trim()}
          >
            {busy ? 'Saving…' : 'Add Annotation'}
          </button>
        </div>
      )}
    </div>
  )
}
