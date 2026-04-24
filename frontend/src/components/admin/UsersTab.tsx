import { useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import type { UserSummary } from '../../types/api'
import { apiFetch, ApiError } from '../../api/client'

interface Props {
  currentUsername: string
}

const ROLES = ['admin', 'operator', 'viewer'] as const
const USERS_KEY = ['admin-users']

export default function UsersTab({ currentUsername }: Props) {
  const queryClient = useQueryClient()
  const [confirmDelete, setConfirmDelete] = useState<UserSummary | null>(null)
  const [toast, setToast] = useState<string | null>(null)
  const [showCreate, setShowCreate] = useState(false)
  const [newUser, setNewUser] = useState({ username: '', password: '', displayName: '', role: 'viewer' as string })

  const { data: users = [], isLoading: loading, error: queryError } = useQuery<UserSummary[]>({
    queryKey: USERS_KEY,
    queryFn: () => apiFetch<UserSummary[]>('/api/auth/users'),
  })

  const roleToEnum = (r: string) => r.charAt(0).toUpperCase() + r.slice(1)

  const showToast = (msg: string) => {
    setToast(msg)
    setTimeout(() => setToast(null), 3000)
  }

  const updateMutation = useMutation({
    mutationFn: ({ id, body }: { id: string; body: object }) =>
      apiFetch(`/api/auth/users/${id}`, { method: 'PUT', body: JSON.stringify(body) }),
    onSuccess: () => {
      showToast(`Updated user`)
      void queryClient.invalidateQueries({ queryKey: USERS_KEY })
    },
    onError: (err) => showToast(err instanceof ApiError ? err.message : 'Failed to update user'),
  })

  const deleteMutation = useMutation({
    mutationFn: (user: UserSummary) =>
      apiFetch(`/api/auth/users/${user.id}`, { method: 'DELETE' }),
    onSuccess: (_data, user) => {
      showToast(`Deleted user ${user.username}`)
      void queryClient.invalidateQueries({ queryKey: USERS_KEY })
      setConfirmDelete(null)
    },
    onError: (err) => showToast(err instanceof ApiError ? err.message : 'Failed to delete user'),
  })

  const createMutation = useMutation({
    mutationFn: (body: object) =>
      apiFetch('/api/auth/users', { method: 'POST', body: JSON.stringify(body) }),
    onSuccess: () => {
      showToast(`Created user ${newUser.username}`)
      setShowCreate(false)
      setNewUser({ username: '', password: '', displayName: '', role: 'viewer' })
      void queryClient.invalidateQueries({ queryKey: USERS_KEY })
    },
    onError: (err) => showToast(err instanceof ApiError ? err.message : 'Failed to create user'),
  })

  const busy = updateMutation.isPending || deleteMutation.isPending || createMutation.isPending

  const updateRole = (user: UserSummary, role: string) =>
    updateMutation.mutate({ id: user.id, body: { role: roleToEnum(role) } })

  const updateDisplayName = (user: UserSummary, displayName: string) =>
    updateMutation.mutate({ id: user.id, body: { displayName } })

  const createUser = () =>
    createMutation.mutate({ ...newUser, role: roleToEnum(newUser.role) })

  if (loading) return <p style={{ color: 'var(--color-text-muted)' }}>Loading…</p>
  if (queryError) return <p style={{ color: 'var(--color-error)' }}>Failed to load users</p>

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
                <td>
                  <input
                    type="text"
                    defaultValue={u.displayName}
                    disabled={busy}
                    style={{ fontSize: 'var(--font-size-xs)', background: 'var(--color-surface)', border: '1px solid var(--color-border)', borderRadius: 'var(--radius-sm)', color: 'var(--color-text)', padding: '2px 4px', width: '100%' }}
                    onBlur={e => {
                      if (e.target.value !== u.displayName) {
                        updateDisplayName(u, e.target.value)
                      }
                    }}
                  />
                </td>
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
              <button className="admin-btn admin-btn--danger" disabled={busy} onClick={() => deleteMutation.mutate(confirmDelete)}>Delete</button>
            </div>
          </div>
        </div>
      )}
    </>
  )
}
