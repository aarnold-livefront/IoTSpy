import { usePlugins } from '../../hooks/usePlugins'

export default function PluginsTab() {
  const { plugins, loading, reloading, error, reload } = usePlugins()

  return (
    <div className="admin-section">
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 16 }}>
        <div>
          <h2 className="admin-section__title" style={{ marginBottom: 4 }}>Protocol Decoder Plugins</h2>
          <p style={{ margin: 0, fontSize: 13, color: 'var(--color-text-muted)' }}>
            External decoders loaded from the <code>plugins/</code> directory at startup.
          </p>
        </div>
        <button
          className="btn btn--primary"
          onClick={() => void reload()}
          disabled={reloading}
          style={{ whiteSpace: 'nowrap' }}
        >
          {reloading ? 'Reloading…' : 'Reload Plugins'}
        </button>
      </div>

      {error && (
        <div style={{ padding: 8, background: 'var(--color-error-bg)', color: 'var(--color-error)', borderRadius: 4, marginBottom: 12, fontSize: 13 }}>
          {error}
        </div>
      )}

      {loading ? (
        <div style={{ color: 'var(--color-text-muted)', fontSize: 13 }}>Loading…</div>
      ) : plugins.length === 0 ? (
        <div style={{ color: 'var(--color-text-muted)', fontSize: 13, padding: '24px 0', textAlign: 'center' }}>
          No plugins loaded. Place compiled plugin assemblies in the <code>plugins/</code> directory and click Reload.
        </div>
      ) : (
        <table style={{ width: '100%', fontSize: 13, borderCollapse: 'collapse' }}>
          <thead>
            <tr style={{ borderBottom: '1px solid var(--color-border)' }}>
              <th style={{ textAlign: 'left', padding: '6px 8px' }}>Protocol</th>
              <th style={{ textAlign: 'left', padding: '6px 8px' }}>Name</th>
              <th style={{ textAlign: 'left', padding: '6px 8px' }}>Version</th>
              <th style={{ textAlign: 'left', padding: '6px 8px' }}>Status</th>
              <th style={{ textAlign: 'left', padding: '6px 8px' }}>Assembly</th>
            </tr>
          </thead>
          <tbody>
            {plugins.map((p) => (
              <tr key={p.protocol} style={{ borderBottom: '1px solid var(--color-border)' }}>
                <td style={{ padding: '6px 8px', fontWeight: 600 }}>{p.protocol}</td>
                <td style={{ padding: '6px 8px' }}>{p.name}</td>
                <td style={{ padding: '6px 8px', color: 'var(--color-text-muted)' }}>{p.version}</td>
                <td style={{ padding: '6px 8px' }}>
                  {p.isLoaded ? (
                    <span style={{ color: 'var(--color-success)', fontSize: 12 }}>● Loaded</span>
                  ) : (
                    <span style={{ color: 'var(--color-error)', fontSize: 12 }} title={p.loadError ?? undefined}>
                      ● Failed{p.loadError ? ` — ${p.loadError}` : ''}
                    </span>
                  )}
                </td>
                <td style={{ padding: '6px 8px', color: 'var(--color-text-muted)', fontSize: 11 }}>
                  <code>{p.assemblyPath}</code>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </div>
  )
}
