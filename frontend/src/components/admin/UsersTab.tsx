import { useState, useEffect, useCallback } from 'react'
import type { UserSummary } from '../../types/api'
import { apiFetch, ApiError } from '../../api/client'

interface Props {
  currentUsername: string
}

const ROLES = ['admin', 'operator', 'viewer'] as const

export default function UsersTab({ currentUsername }: Props) {
  const [users, setUsers] = useState<UserSummary[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [confirmDelete, setConfirmDelete] = useState<UserSummary | null>(null)
  const [busy, setBusy] = useState(false)
  const [toast, setToast] = useState<string | null>(null)
  const [showCreate, setShowCreate] = useState(false)
  const [newUser, setNewUser] = useState({ username: '', password: '', displayName: '', role: 'viewer' as string })

  const load = useCallback(async () => {
    setLoading(true)
    setError(null)
    try {
      const list = await apiFetch<UserSummary[]>('/api/auth/users')
      setUsers(list)
    } catch {
      setError('Failed to load users')
    } finally {
      setLoading(false)
    }
  }, [])

  useEffect(() => {
    void load()
  }, [load])

  const showToast = (msg: string) => {
    setToast(msg)
    setTimeout(() => setToast(null), 3000)
  }

  const roleToEnum = (r: string) => r.charAt(0).toUpperCase() + r.slice(1)

  const updateRole = async (user: UserSummary, role: string) => {
    setBusy(true)
    try {
      await apiFetch(`/api/auth/users/${user.id}`, {
        method: 'PUT',
        body: JSON.stringify({ role: roleToEnum(role) }),
      })
      showToast(`Updated ${user.username} to ${role}`)
      await load()
    } catch (err) {
      showToast(err instanceof ApiError ? err.message : 'Failed to update role')
    } finally {
      setBusy(false)
    }
  }

  const deleteUser = async (user: UserSummary) => {
    setBusy(true)
    try {
      await apiFetch(`/api/auth/users/${user.id}`, { method: 'DELETE' })
      showToast(`Deleted user ${user.username}`)
      await load()
    } catch (err) {
      showToast(err instanceof ApiError ? err.message : 'Failed to delete user')
    } finally {
      setBusy(false)
      setConfirmDelete(null)
    }
  }

  const createUser = async () => {
    setBusy(true)
    try {
      await apiFetch('/api/auth/users', {
        method: 'POST',
        body: JSON.stringify({ ...newUser, role: roleToEnum(newUser.role) }),
      })
      showToast(`Created user ${newUser.username}`)
      setShowCreate(false)
      setNewUser({ username: '', password: '', displayName: '', role: 'viewer' })
      await load()
    } catch (err) {
      showToast(err instanceof ApiError ? err.message : 'Failed to create user')
    } finally {
      setBusy(false)
    }
  }

  if (loading) return <p style={{ color: 'var(--color-text-muted)' }}>Loading…</p>
  if (error) return <p style={{ color: 'var(--color-error)' }}>{error}</p>

  return (
    <>
      {toast && <div className="admin-toast">{toast}</div>}

      <div style={{ marginBottom: 'var(--space-3)' }}>
        <button className="admin-btn admin-btn--primary" onClick={() => setShowCreate(true)}>
          + Add user
        </button>
      </div>

      <div className="admin-table-wrap">
        <table className="admin-table">
          <thead>
            <tr>
              <th>Username</th>
              <th>Display Name</th>
              <th>Role</th>
              <th>Created</th>
              <th>Last Login</th>
              <th></th>
            </tr>
          </thead>
          <tbody>
            {users.map(u => (
              <tr key={u.id}>
                <td>{u.username}{u.username === currentUsername && ' (you)'}</td>
                <td>{u.displayName}</td>
                <td>
                  <select
                    value={u.role}
                    disabled={busy}
                    style={{ fontSize: 'var(--font-size-xs)', background: 'var(--color-surface)', border: '1px solid var(--color-border)', borderRadius: 'var(--radius-sm)', color: 'var(--color-text)', padding: '2px 4px' }}
                    onChange={e => updateRole(u, e.target.value)}
                  >
                    {ROLES.map(r => <option key={r} value={r}>{r}</option>)}
                  </select>
                </td>
                <td>{new Date(u.createdAt).toLocaleDateString()}</td>
                <td>{u.lastLoginAt ? new Date(u.lastLoginAt).toLocaleDateString() : '—'}</td>
                <td>
                  {u.username !== currentUsername && (
                    <button className="admin-btn admin-btn--danger"
                      style={{ padding: '1px 8px' }}
                      disabled={busy}
                      onClick={() => setConfirmDelete(u)}>
                      Delete
                    </button>
                  )}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      {showCreate && (
        <div className="admin-overlay">
          <div className="admin-dialog">
            <h3>Create User</h3>
            {(['username', 'password', 'displayName'] as const).map(field => (
              <div key={field} style={{ marginBottom: 'var(--space-2)' }}>
                <label style={{ fontSize: 'var(--font-size-xs)', color: 'var(--color-text-muted)', display: 'block', marginBottom: 2 }}>
                  {field === 'displayName' ? 'Display Name' : field.charAt(0).toUpperCase() + field.slice(1)}
                </label>
                <input
                  type={field === 'password' ? 'password' : 'text'}
                  value={newUser[field]}
                  onChange={e => setNewUser(prev => ({ ...prev, [field]: e.target.value }))}
                  style={{ width: '100%', fontSize: 'var(--font-size-xs)', background: 'var(--color-surface)', border: '1px solid var(--color-border)', borderRadius: 'var(--radius-sm)', color: 'var(--color-text)', padding: '4px 8px' }}
                />
              </div>
            ))}
            <div style={{ marginBottom: 'var(--space-4)' }}>
              <label style={{ fontSize: 'var(--font-size-xs)', color: 'var(--color-text-muted)', display: 'block', marginBottom: 2 }}>Role</label>
              <select value={newUser.role} onChange={e => setNewUser(prev => ({ ...prev, role: e.target.value }))}
                style={{ fontSize: 'var(--font-size-xs)', background: 'var(--color-surface)', border: '1px solid var(--color-border)', borderRadius: 'var(--radius-sm)', color: 'var(--color-text)', padding: '4px 8px' }}>
                {ROLES.map(r => <option key={r} value={r}>{r}</option>)}
              </select>
            </div>
            <div className="admin-dialog__actions">
              <button className="admin-btn" onClick={() => setShowCreate(false)}>Cancel</button>
              <button className="admin-btn admin-btn--primary" disabled={busy || !newUser.username || !newUser.password} onClick={createUser}>Create</button>
            </div>
          </div>
        </div>
      )}

      {confirmDelete && (
        <div className="admin-overlay">
          <div className="admin-dialog">
            <h3>Delete user?</h3>
            <p>Delete account <strong>{confirmDelete.username}</strong>? This cannot be undone.</p>
            <div className="admin-dialog__actions">
              <button className="admin-btn" onClick={() => setConfirmDelete(null)}>Cancel</button>
              <button className="admin-btn admin-btn--danger" disabled={busy} onClick={() => deleteUser(confirmDelete)}>Delete</button>
            </div>
          </div>
        </div>
      )}
    </>
  )
}
