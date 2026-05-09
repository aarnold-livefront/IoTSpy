import { usePlugins } from '../../hooks/usePlugins'
import '../../styles/plugins-tab.css'

export default function PluginsTab() {
  const { plugins, loading, reloading, error, reload } = usePlugins()

  return (
    <div className="admin-section">
      <div className="plugins-tab__header">
        <div>
          <h2 className="admin-section-title plugins-tab__title">Protocol Decoder Plugins</h2>
          <p className="plugins-tab__subtitle">
            External decoders loaded from the <code>plugins/</code> directory at startup.
          </p>
        </div>
        <button
          className="admin-btn admin-btn--primary plugins-tab__reload-btn"
          onClick={() => void reload()}
          disabled={reloading}
        >
          {reloading ? 'Reloading…' : 'Reload Plugins'}
        </button>
      </div>

      {error && <div className="plugins-tab__error" role="alert">{error}</div>}

      {loading ? (
        <div className="plugins-tab__empty">Loading…</div>
      ) : plugins.length === 0 ? (
        <div className="plugins-tab__empty plugins-tab__empty--centered">
          No plugins loaded. Place compiled plugin assemblies in the <code>plugins/</code> directory and click Reload.
        </div>
      ) : (
        <div className="admin-table-wrap">
          <table className="admin-table">
            <thead>
              <tr>
                <th>Protocol</th>
                <th>Name</th>
                <th>Version</th>
                <th>Status</th>
                <th>Assembly</th>
              </tr>
            </thead>
            <tbody>
              {plugins.map((p) => (
                <tr key={p.protocol}>
                  <td className="plugins-tab__protocol-cell">{p.protocol}</td>
                  <td>{p.name}</td>
                  <td className="plugins-tab__muted-cell">{p.version}</td>
                  <td>
                    {p.isLoaded ? (
                      <span className="plugins-tab__status plugins-tab__status--loaded">● Loaded</span>
                    ) : (
                      <span
                        className="plugins-tab__status plugins-tab__status--failed"
                        title={p.loadError ?? undefined}
                      >
                        ● Failed{p.loadError ? ` — ${p.loadError}` : ''}
                      </span>
                    )}
                  </td>
                  <td className="plugins-tab__assembly-cell">
                    <code>{p.assemblyPath}</code>
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
