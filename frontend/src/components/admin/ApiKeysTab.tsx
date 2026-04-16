import { useState, useEffect, useCallback } from 'react'
import type { ApiKeyCreated, ApiKeySummary } from '../../types/api'
import { listApiKeys, createApiKey, revokeApiKey, rotateApiKey } from '../../api/apiKeys'
import { ApiError } from '../../api/client'

const AVAILABLE_SCOPES = [
  'captures:read',
  'captures:write',
  'scanner:read',
  'scanner:write',
  'manipulation:read',
  'manipulation:write',
  'packets:read',
  'packets:write',
  'admin',
] as const

export default function ApiKeysTab() {
  const [keys, setKeys] = useState<ApiKeySummary[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [toast, setToast] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)
  const [showCreate, setShowCreate] = useState(false)
  const [newName, setNewName] = useState('')
  const [newScopes, setNewScopes] = useState<string[]>([])
  const [newExpiry, setNewExpiry] = useState('')
  const [revealedKey, setRevealedKey] = useState<{ id: string; plaintext: string } | null>(null)
  const [confirmRevoke, setConfirmRevoke] = useState<ApiKeySummary | null>(null)

  const load = useCallback(async () => {
    setLoading(true)
    setError(null)
    try {
      setKeys(await listApiKeys())
    } catch {
      setError('Failed to load API keys')
    } finally {
      setLoading(false)
    }
  }, [])

  useEffect(() => { void load() }, [load])

  const showToast = (msg: string) => {
    setToast(msg)
    setTimeout(() => setToast(null), 4000)
  }

  const handleCreate = async () => {
    setBusy(true)
    try {
      const result: ApiKeyCreated = await createApiKey(
        newName,
        newScopes,
        newExpiry ? new Date(newExpiry).toISOString() : null,
      )
      setRevealedKey({ id: result.id, plaintext: result.key })
      setShowCreate(false)
      setNewName('')
      setNewScopes([])
      setNewExpiry('')
      await load()
    } catch (err) {
      showToast(err instanceof ApiError ? err.message : 'Failed to create key')
    } finally {
      setBusy(false)
    }
  }

  const handleRevoke = async (key: ApiKeySummary) => {
    setBusy(true)
    try {
      await revokeApiKey(key.id)
      showToast(`Revoked key "${key.name}"`)
      await load()
    } catch (err) {
      showToast(err instanceof ApiError ? err.message : 'Failed to revoke key')
    } finally {
      setBusy(false)
      setConfirmRevoke(null)
    }
  }

  const handleRotate = async (key: ApiKeySummary) => {
    setBusy(true)
    try {
      const result = await rotateApiKey(key.id)
      setRevealedKey({ id: result.id, plaintext: result.key })
      showToast(`Rotated key "${key.name}" — copy the new key now`)
      await load()
    } catch (err) {
      showToast(err instanceof ApiError ? err.message : 'Failed to rotate key')
    } finally {
      setBusy(false)
    }
  }

  const copyToClipboard = async (text: string) => {
    try {
      await navigator.clipboard.writeText(text)
      showToast('Copied to clipboard')
    } catch {
      showToast('Copy failed — select and copy manually')
    }
  }

  const toggleScope = (scope: string) =>
    setNewScopes(prev =>
      prev.includes(scope) ? prev.filter(s => s !== scope) : [...prev, scope],
    )

  if (loading) return <p style={{ color: 'var(--color-text-muted)' }}>Loading…</p>
  if (error) return <p style={{ color: 'var(--color-error)' }}>{error}</p>

  return (
    <>
      {toast && <div className="admin-toast">{toast}</div>}

      <div style={{ marginBottom: 'var(--space-3)' }}>
        <button className="admin-btn admin-btn--primary" onClick={() => setShowCreate(true)}>
          + New API key
        </button>
      </div>

      {/* Revealed key banner — shown after create or rotate */}
      {revealedKey && (
        <div className="admin-key-reveal">
          <p style={{ marginBottom: 'var(--space-2)', color: 'var(--color-warning, #f59e0b)', fontWeight: 600 }}>
            Copy this key now — it will not be shown again.
          </p>
          <div style={{ display: 'flex', gap: 'var(--space-2)', alignItems: 'center' }}>
            <code style={{
              flex: 1, fontSize: 'var(--font-size-xs)', background: 'var(--color-surface-2)',
              padding: '6px 10px', borderRadius: 'var(--radius-sm)', wordBreak: 'break-all',
              color: 'var(--color-accent)',
            }}>
              {revealedKey.plaintext}
            </code>
            <button className="admin-btn" onClick={() => copyToClipboard(revealedKey.plaintext)}>
              Copy
            </button>
            <button className="admin-btn" onClick={() => setRevealedKey(null)}>Dismiss</button>
          </div>
        </div>
      )}

      <div className="admin-table-wrap">
        <table className="admin-table">
          <thead>
            <tr>
              <th>Name</th>
              <th>Scopes</th>
              <th>Created</th>
              <th>Last used</th>
              <th>Expires</th>
              <th>Status</th>
              <th></th>
            </tr>
          </thead>
          <tbody>
            {keys.length === 0 && (
              <tr><td colSpan={7} style={{ color: 'var(--color-text-muted)', textAlign: 'center' }}>No API keys</td></tr>
            )}
            {keys.map(k => (
              <tr key={k.id} style={{ opacity: k.isRevoked ? 0.5 : 1 }}>
                <td>{k.name}</td>
                <td>
                  <div style={{ display: 'flex', flexWrap: 'wrap', gap: 3 }}>
                    {k.scopes.map(s => (
                      <span key={s} className="badge" style={{ fontSize: '10px', padding: '1px 5px' }}>{s}</span>
                    ))}
                  </div>
                </td>
                <td>{new Date(k.createdAt).toLocaleDateString()}</td>
                <td>{k.lastUsedAt ? new Date(k.lastUsedAt).toLocaleDateString() : '—'}</td>
                <td>{k.expiresAt ? new Date(k.expiresAt).toLocaleDateString() : '—'}</td>
                <td>
                  <span className={`badge ${k.isRevoked ? 'badge--error' : 'badge--success'}`}>
                    {k.isRevoked ? 'revoked' : 'active'}
                  </span>
                </td>
                <td>
                  {!k.isRevoked && (
                    <div style={{ display: 'flex', gap: 4 }}>
                      <button
                        className="admin-btn"
                        style={{ padding: '1px 8px' }}
                        disabled={busy}
                        onClick={() => handleRotate(k)}
                      >
                        Rotate
                      </button>
                      <button
                        className="admin-btn admin-btn--danger"
                        style={{ padding: '1px 8px' }}
                        disabled={busy}
                        onClick={() => setConfirmRevoke(k)}
                      >
                        Revoke
                      </button>
                    </div>
                  )}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      {/* Create dialog */}
      {showCreate && (
        <div className="admin-overlay">
          <div className="admin-dialog" style={{ minWidth: 400 }}>
            <h3>Create API key</h3>

            <div style={{ marginBottom: 'var(--space-2)' }}>
              <label style={{ fontSize: 'var(--font-size-xs)', color: 'var(--color-text-muted)', display: 'block', marginBottom: 2 }}>
                Name
              </label>
              <input
                type="text"
                value={newName}
                onChange={e => setNewName(e.target.value)}
                placeholder="e.g. CI pipeline"
                style={{ width: '100%', fontSize: 'var(--font-size-xs)', background: 'var(--color-surface)', border: '1px solid var(--color-border)', borderRadius: 'var(--radius-sm)', color: 'var(--color-text)', padding: '4px 8px' }}
              />
            </div>

            <div style={{ marginBottom: 'var(--space-2)' }}>
              <label style={{ fontSize: 'var(--font-size-xs)', color: 'var(--color-text-muted)', display: 'block', marginBottom: 4 }}>
                Scopes
              </label>
              <div style={{ display: 'flex', flexWrap: 'wrap', gap: 6 }}>
                {AVAILABLE_SCOPES.map(s => (
                  <label key={s} style={{ display: 'flex', alignItems: 'center', gap: 4, fontSize: 'var(--font-size-xs)', cursor: 'pointer' }}>
                    <input
                      type="checkbox"
                      checked={newScopes.includes(s)}
                      onChange={() => toggleScope(s)}
                    />
                    {s}
                  </label>
                ))}
              </div>
            </div>

            <div style={{ marginBottom: 'var(--space-4)' }}>
              <label style={{ fontSize: 'var(--font-size-xs)', color: 'var(--color-text-muted)', display: 'block', marginBottom: 2 }}>
                Expiry (optional)
              </label>
              <input
                type="date"
                value={newExpiry}
                onChange={e => setNewExpiry(e.target.value)}
                style={{ fontSize: 'var(--font-size-xs)', background: 'var(--color-surface)', border: '1px solid var(--color-border)', borderRadius: 'var(--radius-sm)', color: 'var(--color-text)', padding: '4px 8px' }}
              />
            </div>

            <div className="admin-dialog__actions">
              <button className="admin-btn" onClick={() => setShowCreate(false)}>Cancel</button>
              <button
                className="admin-btn admin-btn--primary"
                disabled={busy || !newName.trim() || newScopes.length === 0}
                onClick={handleCreate}
              >
                Create
              </button>
            </div>
          </div>
        </div>
      )}

      {/* Revoke confirmation */}
      {confirmRevoke && (
        <div className="admin-overlay">
          <div className="admin-dialog">
            <h3>Revoke key?</h3>
            <p>Revoke <strong>{confirmRevoke.name}</strong>? All requests using it will be rejected immediately.</p>
            <div className="admin-dialog__actions">
              <button className="admin-btn" onClick={() => setConfirmRevoke(null)}>Cancel</button>
              <button
                className="admin-btn admin-btn--danger"
                disabled={busy}
                onClick={() => handleRevoke(confirmRevoke)}
              >
                Revoke
              </button>
            </div>
          </div>
        </div>
      )}
    </>
  )
}
