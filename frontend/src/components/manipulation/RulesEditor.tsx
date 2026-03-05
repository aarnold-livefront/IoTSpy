import { useState } from 'react'
import type {
  ManipulationRule,
  CreateManipulationRuleRequest,
  UpdateManipulationRuleRequest,
  ManipulationRuleAction,
  ManipulationPhase,
} from '../../types/api'
import '../../styles/manipulation.css'

interface Props {
  rules: ManipulationRule[]
  loading: boolean
  error: string | null
  onAdd: (req: CreateManipulationRuleRequest) => Promise<ManipulationRule | null>
  onEdit: (id: string, req: UpdateManipulationRuleRequest) => Promise<ManipulationRule | null>
  onDelete: (id: string) => void
}

const ACTIONS: ManipulationRuleAction[] = ['ModifyHeader', 'ModifyBody', 'ModifyStatus', 'Delay', 'Drop']
const PHASES: ManipulationPhase[] = ['Request', 'Response']

const emptyForm: CreateManipulationRuleRequest = {
  name: '',
  enabled: true,
  priority: 0,
  phase: 'Request',
  action: 'ModifyHeader',
  hostPattern: '',
  pathPattern: '',
  methodPattern: '',
  headerName: '',
  headerValue: '',
  bodyReplace: '',
  bodyReplaceWith: '',
  overrideStatusCode: undefined,
  delayMs: undefined,
}

export default function RulesEditor({ rules, loading, error, onAdd, onEdit, onDelete }: Props) {
  const [editing, setEditing] = useState<string | null>(null) // rule id or 'new'
  const [form, setForm] = useState<CreateManipulationRuleRequest>({ ...emptyForm })

  const startNew = () => {
    setForm({ ...emptyForm })
    setEditing('new')
  }

  const startEdit = (rule: ManipulationRule) => {
    setForm({
      name: rule.name,
      enabled: rule.enabled,
      priority: rule.priority,
      phase: rule.phase,
      action: rule.action,
      hostPattern: rule.hostPattern ?? '',
      pathPattern: rule.pathPattern ?? '',
      methodPattern: rule.methodPattern ?? '',
      headerName: rule.headerName ?? '',
      headerValue: rule.headerValue ?? '',
      bodyReplace: rule.bodyReplace ?? '',
      bodyReplaceWith: rule.bodyReplaceWith ?? '',
      overrideStatusCode: rule.overrideStatusCode,
      delayMs: rule.delayMs,
    })
    setEditing(rule.id)
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
      const result = await onEdit(editing, { ...form, id: editing } as UpdateManipulationRuleRequest)
      if (result) cancelEdit()
    }
  }

  const updateField = <K extends keyof CreateManipulationRuleRequest>(
    key: K,
    value: CreateManipulationRuleRequest[K],
  ) => {
    setForm((prev) => ({ ...prev, [key]: value }))
  }

  return (
    <div className="manip-section">
      <div className="manip-section__header">
        <span className="manip-section__title">
          Manipulation Rules {loading && <span className="manip-spinner" />}
        </span>
        <button className="manip-btn manip-btn--primary" onClick={startNew}>
          Add Rule
        </button>
      </div>

      {error && <div className="manip-error">{error}</div>}

      {/* Edit / Create form */}
      {editing && (
        <div className="manip-form">
          <div className="manip-form__row">
            <label className="manip-form__label">
              Name
              <input
                className="manip-form__input"
                value={form.name}
                onChange={(e) => updateField('name', e.target.value)}
                placeholder="Rule name"
              />
            </label>
            <label className="manip-form__label">
              Priority
              <input
                className="manip-form__input manip-form__input--narrow"
                type="number"
                value={form.priority ?? 0}
                onChange={(e) => updateField('priority', Number(e.target.value))}
              />
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
            <label className="manip-form__label">
              Action
              <select
                className="manip-form__select"
                value={form.action}
                onChange={(e) => updateField('action', e.target.value as ManipulationRuleAction)}
              >
                {ACTIONS.map((a) => <option key={a} value={a}>{a}</option>)}
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
                placeholder=".*example\.com"
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
            <label className="manip-form__label">
              Method Pattern
              <input
                className="manip-form__input"
                value={form.methodPattern ?? ''}
                onChange={(e) => updateField('methodPattern', e.target.value)}
                placeholder="GET|POST"
              />
            </label>
          </div>

          {/* Action-specific fields */}
          {(form.action === 'ModifyHeader') && (
            <div className="manip-form__row">
              <label className="manip-form__label">
                Header Name
                <input
                  className="manip-form__input"
                  value={form.headerName ?? ''}
                  onChange={(e) => updateField('headerName', e.target.value)}
                  placeholder="X-Custom-Header"
                />
              </label>
              <label className="manip-form__label">
                Header Value
                <input
                  className="manip-form__input"
                  value={form.headerValue ?? ''}
                  onChange={(e) => updateField('headerValue', e.target.value)}
                  placeholder="value"
                />
              </label>
            </div>
          )}

          {(form.action === 'ModifyBody') && (
            <div className="manip-form__row">
              <label className="manip-form__label">
                Body Replace (pattern)
                <input
                  className="manip-form__input"
                  value={form.bodyReplace ?? ''}
                  onChange={(e) => updateField('bodyReplace', e.target.value)}
                  placeholder="search string"
                />
              </label>
              <label className="manip-form__label">
                Replace With
                <input
                  className="manip-form__input"
                  value={form.bodyReplaceWith ?? ''}
                  onChange={(e) => updateField('bodyReplaceWith', e.target.value)}
                  placeholder="replacement string"
                />
              </label>
            </div>
          )}

          {(form.action === 'ModifyStatus') && (
            <div className="manip-form__row">
              <label className="manip-form__label">
                Status Code
                <input
                  className="manip-form__input manip-form__input--narrow"
                  type="number"
                  value={form.overrideStatusCode ?? 200}
                  onChange={(e) => updateField('overrideStatusCode', Number(e.target.value))}
                  min={100}
                  max={599}
                />
              </label>
            </div>
          )}

          {(form.action === 'Delay') && (
            <div className="manip-form__row">
              <label className="manip-form__label">
                Delay (ms)
                <input
                  className="manip-form__input manip-form__input--narrow"
                  type="number"
                  value={form.delayMs ?? 1000}
                  onChange={(e) => updateField('delayMs', Number(e.target.value))}
                  min={0}
                />
              </label>
            </div>
          )}

          <div className="manip-form__actions">
            <button className="manip-btn manip-btn--primary" onClick={handleSave}>
              {editing === 'new' ? 'Create' : 'Save'}
            </button>
            <button className="manip-btn manip-btn--ghost" onClick={cancelEdit}>
              Cancel
            </button>
          </div>
        </div>
      )}

      {/* Rules list */}
      <div className="manip-table">
        {rules.length === 0 && !editing ? (
          <div className="manip-empty">No manipulation rules configured.</div>
        ) : (
          rules.map((rule) => (
            <div key={rule.id} className={`manip-row ${!rule.enabled ? 'manip-row--disabled' : ''}`}>
              <div className="manip-row__main">
                <span className={`manip-badge manip-badge--${rule.action.toLowerCase()}`}>
                  {rule.action}
                </span>
                <span className={`manip-phase manip-phase--${rule.phase.toLowerCase()}`}>
                  {rule.phase}
                </span>
                <span className="manip-row__name">{rule.name}</span>
                {!rule.enabled && <span className="manip-row__disabled-tag">disabled</span>}
              </div>
              <div className="manip-row__patterns">
                {rule.hostPattern && <code className="manip-pattern">{rule.hostPattern}</code>}
                {rule.pathPattern && <code className="manip-pattern">{rule.pathPattern}</code>}
                {rule.methodPattern && <code className="manip-pattern">{rule.methodPattern}</code>}
              </div>
              <div className="manip-row__actions">
                <button className="manip-btn manip-btn--small" onClick={() => startEdit(rule)}>
                  Edit
                </button>
                <button
                  className="manip-btn manip-btn--small manip-btn--danger"
                  onClick={() => onDelete(rule.id)}
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
