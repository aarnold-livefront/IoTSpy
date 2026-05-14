import { useState } from 'react'
import { useScanScopes } from '../../hooks/useScanScopes'

export default function ScanScopesTab() {
  const { scopes, loading, saving, error, add, toggle, remove } = useScanScopes()
  const [name, setName] = useState('')
  const [cidr, setCidr] = useState('')
  const [formError, setFormError] = useState<string | null>(null)

  const handleAdd = async () => {
    if (!name.trim()) { setFormError('Name is required.'); return }
    if (!cidr.trim()) { setFormError('CIDR block is required.'); return }
    setFormError(null)
    await add(name.trim(), cidr.trim())
    setName('')
    setCidr('')
  }

  return (
    <div className="admin-section">
      <h2 className="admin-section-title">Scan Scopes</h2>
      <p className="plugins-tab__subtitle">
        Define CIDR blocks that are authorised for scanning. When at least one active scope
        exists, the scanner will reject requests for devices whose IP does not fall within
        any active scope. An empty list means no restriction.
      </p>

      <div className="scan-scope-form">
        <input
          className="scan-form__input"
          type="text"
          placeholder="Scope name (e.g. Lab network)"
          value={name}
          onChange={e => setName(e.target.value)}
          aria-label="Scope name"
        />
        <input
          className="scan-form__input"
          type="text"
          placeholder="CIDR (e.g. 192.168.1.0/24)"
          value={cidr}
          onChange={e => setCidr(e.target.value)}
          aria-label="CIDR block"
        />
        <button
          className="admin-btn admin-btn--primary"
          onClick={() => void handleAdd()}
          disabled={saving}
        >
          {saving ? 'Adding…' : 'Add Scope'}
        </button>
      </div>

      {(formError || error) && (
        <div className="plugins-tab__error" role="alert">{formError ?? error}</div>
      )}

      {loading ? (
        <div className="plugins-tab__empty">Loading…</div>
      ) : scopes.length === 0 ? (
        <div className="plugins-tab__empty plugins-tab__empty--centered">
          No scopes defined — all IPs are currently unrestricted.
        </div>
      ) : (
        <div className="admin-table-wrap">
          <table className="admin-table">
            <thead>
              <tr>
                <th>Name</th>
                <th>CIDR</th>
                <th>Status</th>
                <th>Created by</th>
                <th>Actions</th>
              </tr>
            </thead>
            <tbody>
              {scopes.map(scope => (
                <tr key={scope.id}>
                  <td>{scope.name}</td>
                  <td><code>{scope.cidr}</code></td>
                  <td>
                    {scope.isActive ? (
                      <span className="plugins-tab__status plugins-tab__status--loaded">● Active</span>
                    ) : (
                      <span className="plugins-tab__status plugins-tab__status--failed">● Inactive</span>
                    )}
                  </td>
                  <td className="plugins-tab__muted-cell">{scope.createdByUsername}</td>
                  <td className="scan-scope-actions">
                    <button
                      className="admin-btn admin-btn--secondary"
                      onClick={() => void toggle(scope.id)}
                    >
                      {scope.isActive ? 'Disable' : 'Enable'}
                    </button>
                    <button
                      className="admin-btn admin-btn--danger"
                      onClick={() => void remove(scope.id)}
                    >
                      Delete
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  )
}
