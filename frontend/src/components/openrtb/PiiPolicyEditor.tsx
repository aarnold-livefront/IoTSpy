import { useState } from 'react'
import type {
  OpenRtbPiiPolicy,
  CreatePiiPolicyRequest,
  UpdatePiiPolicyRequest,
  PiiRedactionStrategy,
} from '../../types/api'

interface Props {
  policies: OpenRtbPiiPolicy[]
  loading: boolean
  error: string | null
  onAdd: (req: CreatePiiPolicyRequest) => Promise<OpenRtbPiiPolicy>
  onEdit: (id: string, req: UpdatePiiPolicyRequest) => Promise<OpenRtbPiiPolicy>
  onDelete: (id: string) => Promise<void>
  onReset: () => Promise<void>
}

const strategies: PiiRedactionStrategy[] = [
  'Redact', 'TruncateIp', 'HashSha256', 'GeneralizeGeo', 'GeneralizeUserAgent', 'Remove',
]

const strategyDescriptions: Record<PiiRedactionStrategy, string> = {
  Redact: 'Replace with placeholder (0.0.0.0, [REDACTED])',
  TruncateIp: 'Truncate IP to /24 subnet',
  HashSha256: 'SHA-256 hash (preserves joins)',
  GeneralizeGeo: 'Round to ~1km precision',
  GeneralizeUserAgent: 'Keep browser/OS family only',
  Remove: 'Remove field entirely',
}

export default function PiiPolicyEditor({
  policies, loading, error, onAdd, onEdit, onDelete, onReset,
}: Props) {
  const [showForm, setShowForm] = useState(false)
  const [editId, setEditId] = useState<string | null>(null)
  const [fieldPath, setFieldPath] = useState('')
  const [strategy, setStrategy] = useState<PiiRedactionStrategy>('Redact')
  const [hostPattern, setHostPattern] = useState('')
  const [priority, setPriority] = useState(0)

  const handleSubmit = async () => {
    if (!fieldPath.trim()) return
    try {
      if (editId) {
        await onEdit(editId, { fieldPath, strategy, hostPattern: hostPattern || undefined, priority })
      } else {
        await onAdd({ fieldPath, strategy, hostPattern: hostPattern || undefined, priority })
      }
      resetForm()
    } catch {
      // error handled by hook
    }
  }

  const startEdit = (p: OpenRtbPiiPolicy) => {
    setEditId(p.id)
    setFieldPath(p.fieldPath)
    setStrategy(p.strategy)
    setHostPattern(p.hostPattern ?? '')
    setPriority(p.priority)
    setShowForm(true)
  }

  const resetForm = () => {
    setShowForm(false)
    setEditId(null)
    setFieldPath('')
    setStrategy('Redact')
    setHostPattern('')
    setPriority(0)
  }

  return (
    <div className="pii-policy-editor">
      <div className="openrtb-header">
        <h3>PII Redaction Policies</h3>
        <div style={{ display: 'flex', gap: '8px' }}>
          <button onClick={onReset} className="btn-secondary">Reset Defaults</button>
          <button onClick={() => { resetForm(); setShowForm(true) }}>Add Policy</button>
        </div>
      </div>

      {error && <div className="openrtb-error">{error}</div>}

      {showForm && (
        <div className="policy-form">
          <div className="form-row">
            <label>Field Path:
              <input value={fieldPath} onChange={(e) => setFieldPath(e.target.value)}
                placeholder="e.g. device.ip, user.id" />
            </label>
            <label>Strategy:
              <select value={strategy} onChange={(e) => setStrategy(e.target.value as PiiRedactionStrategy)}>
                {strategies.map((s) => (
                  <option key={s} value={s}>{s} - {strategyDescriptions[s]}</option>
                ))}
              </select>
            </label>
          </div>
          <div className="form-row">
            <label>Host Pattern (regex, optional):
              <input value={hostPattern} onChange={(e) => setHostPattern(e.target.value)}
                placeholder="e.g. .*doubleclick\\.net" />
            </label>
            <label>Priority:
              <input type="number" value={priority} onChange={(e) => setPriority(Number(e.target.value))} />
            </label>
          </div>
          <div className="form-actions">
            <button onClick={handleSubmit}>{editId ? 'Update' : 'Create'}</button>
            <button onClick={resetForm} className="btn-secondary">Cancel</button>
          </div>
        </div>
      )}

      <table className="openrtb-table">
        <thead>
          <tr>
            <th>Field Path</th>
            <th>Strategy</th>
            <th>Host Pattern</th>
            <th>Priority</th>
            <th>Enabled</th>
            <th>Actions</th>
          </tr>
        </thead>
        <tbody>
          {policies.map((p) => (
            <tr key={p.id}>
              <td><code>{p.fieldPath}</code></td>
              <td><span className="badge">{p.strategy}</span></td>
              <td>{p.hostPattern || '-'}</td>
              <td>{p.priority}</td>
              <td>
                <input type="checkbox" checked={p.enabled}
                  onChange={() => onEdit(p.id, { enabled: !p.enabled })} />
              </td>
              <td>
                <button className="btn-sm" onClick={() => startEdit(p)}>Edit</button>
                <button className="btn-sm btn-danger" onClick={() => onDelete(p.id)}>Delete</button>
              </td>
            </tr>
          ))}
          {policies.length === 0 && !loading && (
            <tr><td colSpan={6} style={{ textAlign: 'center', color: 'var(--color-text-muted)' }}>
              No policies configured. Click "Reset Defaults" to seed default PII policies.
            </td></tr>
          )}
        </tbody>
      </table>
    </div>
  )
}
