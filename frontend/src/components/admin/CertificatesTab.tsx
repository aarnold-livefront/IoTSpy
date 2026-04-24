import { useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import type { CertificateEntry } from '../../types/api'
import { apiFetch } from '../../api/client'

interface RootCaInfo {
  id: string
  commonName: string
  serialNumber: string
  notBefore: string
  notAfter: string
  certificatePem: string
}

const ROOT_CA_KEY = ['cert-root-ca']
const LEAF_CERTS_KEY = ['cert-leaf']

export default function CertificatesTab() {
  const queryClient = useQueryClient()
  const [confirm, setConfirm] = useState<'regenerate' | 'purge-leaf' | null>(null)
  const [toast, setToast] = useState<string | null>(null)

  const { data: rootCa, isLoading: rootLoading } = useQuery<RootCaInfo>({
    queryKey: ROOT_CA_KEY,
    queryFn: () => apiFetch<RootCaInfo>('/api/certificates/root-ca'),
  })

  const { data: allCerts = [], isLoading: certsLoading } = useQuery<CertificateEntry[]>({
    queryKey: LEAF_CERTS_KEY,
    queryFn: () => apiFetch<CertificateEntry[]>('/api/certificates'),
  })

  const leafCerts = allCerts.filter(c => !c.isRootCa).slice(0, 50)
  const loading = rootLoading || certsLoading

  const showToast = (msg: string) => {
    setToast(msg)
    setTimeout(() => setToast(null), 3000)
  }

  const regenerateMutation = useMutation({
    mutationFn: () => apiFetch('/api/certificates/root-ca/regenerate', { method: 'POST' }),
    onSuccess: () => {
      showToast('Root CA regenerated successfully')
      void queryClient.invalidateQueries({ queryKey: ROOT_CA_KEY })
      void queryClient.invalidateQueries({ queryKey: LEAF_CERTS_KEY })
    },
    onError: () => showToast('Failed to regenerate CA'),
  })

  const purgeMutation = useMutation({
    mutationFn: () =>
      apiFetch<{ deleted: number }>('/api/certificates/purge-leaf-certs', { method: 'DELETE' }),
    onSuccess: (data) => {
      showToast(`Purged ${data.deleted} leaf certificates`)
      void queryClient.invalidateQueries({ queryKey: LEAF_CERTS_KEY })
    },
    onError: () => showToast('Failed to purge leaf certificates'),
  })

  const busy = regenerateMutation.isPending || purgeMutation.isPending

  if (loading) return <p style={{ color: 'var(--color-text-muted)' }}>Loading…</p>

  return (
    <>
      {toast && <div className="admin-toast">{toast}</div>}

      <div className="admin-section">
        <div className="admin-section-title">Root Certificate Authority</div>
        <div className="admin-card" style={{ maxWidth: 560 }}>
          <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: 'var(--font-size-xs)' }}>
            <tbody>
              {[
                ['Common Name', rootCa?.commonName],
                ['Serial Number', rootCa?.serialNumber],
                ['Valid From', rootCa ? new Date(rootCa.notBefore).toLocaleString() : '—'],
                ['Expires', rootCa ? new Date(rootCa.notAfter).toLocaleString() : '—'],
              ].map(([label, value]) => (
                <tr key={label as string}>
                  <td style={{ padding: '4px 0', color: 'var(--color-text-muted)', width: 120 }}>{label}</td>
                  <td style={{ padding: '4px 0', fontFamily: 'var(--font-mono)' }}>{value ?? '—'}</td>
                </tr>
              ))}
            </tbody>
          </table>
          <div className="admin-card__actions" style={{ marginTop: 'var(--space-3)' }}>
            <a className="admin-btn" href="/api/certificates/root-ca/download" download>Download DER</a>
            <a className="admin-btn" href="/api/certificates/root-ca/pem" download>Download PEM</a>
            <button className="admin-btn admin-btn--danger" disabled={busy}
              onClick={() => setConfirm('regenerate')}>
              Regenerate CA
            </button>
          </div>
        </div>
      </div>

      <div className="admin-section">
        <div className="admin-section-title">
          Leaf Certificates
          <span style={{ marginLeft: 'var(--space-2)', fontWeight: 400, color: 'var(--color-text-muted)' }}>
            ({leafCerts.length} shown)
          </span>
        </div>
        <div style={{ marginBottom: 'var(--space-3)' }}>
          <button className="admin-btn admin-btn--danger" disabled={busy}
            onClick={() => setConfirm('purge-leaf')}>
            Purge all leaf certs
          </button>
        </div>
        {leafCerts.length > 0 && (
          <div className="admin-table-wrap">
            <table className="admin-table">
              <thead>
                <tr>
                  <th>Host</th>
                  <th>Issued</th>
                  <th>Expires</th>
                </tr>
              </thead>
              <tbody>
                {leafCerts.map(c => (
                  <tr key={c.id}>
                    <td>{c.commonName}</td>
                    <td>{new Date(c.notBefore).toLocaleDateString()}</td>
                    <td>{new Date(c.notAfter).toLocaleDateString()}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>

      {confirm && (
        <div className="admin-overlay">
          <div className="admin-dialog">
            {confirm === 'regenerate' ? (
              <>
                <h3>Regenerate Root CA?</h3>
                <p>This will invalidate all existing leaf certificates and require re-installing the root CA on all devices.</p>
              </>
            ) : (
              <>
                <h3>Purge all leaf certificates?</h3>
                <p>All {leafCerts.length} leaf certificates will be deleted. They will be regenerated on next use.</p>
              </>
            )}
            <div className="admin-dialog__actions">
              <button className="admin-btn" onClick={() => setConfirm(null)}>Cancel</button>
              <button className="admin-btn admin-btn--danger" disabled={busy} onClick={async () => {
                if (confirm === 'regenerate') await regenerateMutation.mutateAsync()
                else await purgeMutation.mutateAsync()
                setConfirm(null)
              }}>Confirm</button>
            </div>
          </div>
        </div>
      )}
    </>
  )
}
