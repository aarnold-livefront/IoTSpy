import { useState, useEffect } from 'react'
import ConfirmDialog from '../common/ConfirmDialog'
import type {
  Breakpoint,
  CreateBreakpointRequest,
  UpdateBreakpointRequest,
  ScriptLanguage,
  ManipulationPhase,
} from '../../types/api'
import '../../styles/manipulation.css'

interface Props {
  breakpoints: Breakpoint[]
  loading: boolean
  error: string | null
  onAdd: (req: CreateBreakpointRequest) => Promise<Breakpoint | null>
  onEdit: (id: string, req: UpdateBreakpointRequest) => Promise<Breakpoint | null>
  onDelete: (id: string) => void
}

const LANGUAGES: ScriptLanguage[] = ['JavaScript', 'CSharp']
const PHASES: ManipulationPhase[] = ['Request', 'Response']

const defaultScript: Record<ScriptLanguage, string> = {
  JavaScript: `// 'request' object is available with: method, host, path, headers, body
// Return modified request or null to pass through
// Example:
// request.headers["X-Debug"] = "true";
// return request;
return null;`,
  CSharp: `// HttpMessage 'message' is available
// Return modified message or null to pass through
// Example:
// message.Headers["X-Debug"] = "true";
// return message;
return null;`,
}

const emptyForm: CreateBreakpointRequest = {
  name: '',
  enabled: true,
  language: 'JavaScript',
  phase: 'Request',
  hostPattern: '',
  pathPattern: '',
  scriptCode: defaultScript['JavaScript'],
}

export default function BreakpointsEditor({ breakpoints, loading, error, onAdd, onEdit, onDelete }: Props) {
  const [editing, setEditing] = useState<string | null>(null)
  const [form, setForm] = useState<CreateBreakpointRequest>({ ...emptyForm })
  const [confirmDeleteId, setConfirmDeleteId] = useState<string | null>(null)

  const startNew = () => {
    setForm({ ...emptyForm })
    setEditing('new')
  }

  const startEdit = (bp: Breakpoint) => {
    setForm({
      name: bp.name,
      enabled: bp.enabled,
      language: bp.language,
      phase: bp.phase,
      hostPattern: bp.hostPattern ?? '',
      pathPattern: bp.pathPattern ?? '',
      scriptCode: bp.scriptCode,
    })
    setEditing(bp.id)
  }

  const cancelEdit = () => {
    setEditing(null)
    setForm({ ...emptyForm })
  }

  const handleSave = async () => {
    if (!form.name.trim()) return
    if (editing === 'new') {
      const result = await onAdd(form)
      if (result) cancelEdit()
    } else if (editing) {
      const result = await onEdit(editing, { ...form, id: editing } as UpdateBreakpointRequest)
      if (result) cancelEdit()
    }
  }

  // Keyboard shortcuts for the active form
  useEffect(() => {
    if (!editing) return
    const handle = (e: KeyboardEvent) => {
      if (e.key === 'Escape') { e.stopPropagation(); cancelEdit() }
      if ((e.ctrlKey || e.metaKey) && e.key === 's') { e.preventDefault(); void handleSave() }
    }
    document.addEventListener('keydown', handle)
    return () => document.removeEventListener('keydown', handle)
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [editing, form])

  const updateField = <K extends keyof CreateBreakpointRequest>(
    key: K,
    value: CreateBreakpointRequest[K],
  ) => {
    setForm((prev) => ({ ...prev, [key]: value }))
  }

  const handleLanguageChange = (lang: ScriptLanguage) => {
    updateField('language', lang)
    const oldDefault = defaultScript[form.language]
    if (form.scriptCode === oldDefault) {
      updateField('scriptCode', defaultScript[lang])
    }
  }

  const confirmingBp = confirmDeleteId ? breakpoints.find((b) => b.id === confirmDeleteId) : null

  return (
    <div className="manip-section">
      {confirmDeleteId && confirmingBp && (
        <ConfirmDialog
          title="Delete breakpoint"
          message={`Delete "${confirmingBp.name}"? This cannot be undone.`}
          confirmLabel="Delete"
          danger
          onConfirm={() => { onDelete(confirmDeleteId); setConfirmDeleteId(null) }}
          onCancel={() => setConfirmDeleteId(null)}
        />
      )}

      <div className="manip-section__header">
        <span className="manip-section__title">
          Scripted Breakpoints {loading && <span className="manip-spinner" />}
        </span>
        <button className="manip-btn manip-btn--primary" onClick={startNew}>
          Add Breakpoint
        </button>
      </div>

      {error && <div className="manip-error">{error}</div>}

      {editing && (
        <div className="manip-form">
          <div className="manip-form__row">
            <label className="manip-form__label">
              Name
              <input
                className="manip-form__input"
                value={form.name}
                onChange={(e) => updateField('name', e.target.value)}
                placeholder="Breakpoint name"
                autoFocus
              />
            </label>
            <label className="manip-form__label">
              Language
              <select
                className="manip-form__select"
                value={form.language}
                onChange={(e) => handleLanguageChange(e.target.value as ScriptLanguage)}
              >
                {LANGUAGES.map((l) => <option key={l} value={l}>{l}</option>)}
              </select>
            </label>
            <label className="manip-form__label">
              Phase
              <select
                className="manip-form__select"
                value={form.phase}
                onChange={(e) => updateField('phase', e.target.value as ManipulationPhase)}
              >
                {PHASES.map((p) => <option key={p} value={p}>{p}</option>)}
              </select>
            </label>
            <label className="manip-form__checkbox">
              <input
                type="checkbox"
                checked={form.enabled ?? true}
                onChange={(e) => updateField('enabled', e.target.checked)}
              />
              Enabled
            </label>
          </div>

          <div className="manip-form__row">
            <label className="manip-form__label">
              Host Pattern
              <input
                className="manip-form__input"
                value={form.hostPattern ?? ''}
                onChange={(e) => updateField('hostPattern', e.target.value)}
                placeholder=".*"
              />
            </label>
            <label className="manip-form__label">
              Path Pattern
              <input
                className="manip-form__input"
                value={form.pathPattern ?? ''}
                onChange={(e) => updateField('pathPattern', e.target.value)}
                placeholder="/api/.*"
              />
            </label>
          </div>

          <div className="manip-form__row manip-form__row--full">
            <label className="manip-form__label manip-form__label--full">
              Script ({form.language})
              <textarea
                className="manip-form__textarea manip-form__textarea--code"
                value={form.scriptCode}
                onChange={(e) => updateField('scriptCode', e.target.value)}
                rows={12}
                spellCheck={false}
              />
            </label>
          </div>

          <div className="manip-form__actions">
            <button className="manip-btn manip-btn--primary" onClick={handleSave}>
              {editing === 'new' ? 'Create' : 'Save'}
            </button>
            <button className="manip-btn manip-btn--ghost" onClick={cancelEdit}>
              Cancel
            </button>
            <span className="manip-form__hint">Ctrl+S to save · Esc to cancel</span>
          </div>
        </div>
      )}

      <div className="manip-table">
        {breakpoints.length === 0 && !editing ? (
          <div className="manip-empty">
            <div className="manip-empty__message">No scripted breakpoints yet.</div>
            <div className="manip-empty__hint">Add a breakpoint to intercept matching requests and transform them with JavaScript or C#.</div>
          </div>
        ) : (
          breakpoints.map((bp) => (
            <div key={bp.id} className={`manip-row ${!bp.enabled ? 'manip-row--disabled' : ''}`}>
              <div className="manip-row__main">
                <span className={`manip-badge manip-badge--${bp.language.toLowerCase()}`}>
                  {bp.language}
                </span>
                <span className={`manip-phase manip-phase--${bp.phase.toLowerCase()}`}>
                  {bp.phase}
                </span>
                <span className="manip-row__name">{bp.name}</span>
                {!bp.enabled && <span className="manip-row__disabled-tag">disabled</span>}
              </div>
              <div className="manip-row__patterns">
                {bp.hostPattern && <code className="manip-pattern">{bp.hostPattern}</code>}
                {bp.pathPattern && <code className="manip-pattern">{bp.pathPattern}</code>}
              </div>
              <div className="manip-row__script-preview">
                <code>{bp.scriptCode.split('\n').slice(0, 2).join(' ')}{bp.scriptCode.split('\n').length > 2 ? '...' : ''}</code>
              </div>
              <div className="manip-row__actions">
                <button className="manip-btn manip-btn--small" onClick={() => startEdit(bp)}>
                  Edit
                </button>
                <button
                  className="manip-btn manip-btn--small manip-btn--danger"
                  onClick={() => setConfirmDeleteId(bp.id)}
                >
                  Delete
                </button>
              </div>
            </div>
          ))
        )}
      </div>
    </div>
  )
}
