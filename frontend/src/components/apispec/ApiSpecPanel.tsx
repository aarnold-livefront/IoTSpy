import { useState } from 'react'
import { useApiSpec } from '../../hooks/useApiSpec'
import GenerateSpecDialog from './GenerateSpecDialog'
import SpecEditor from './SpecEditor'
import ReplacementRulesEditor from './ReplacementRulesEditor'
import ImportExportControls from './ImportExportControls'
import type { ApiSpecDocument } from '../../types/api'

const STATUS_COLORS: Record<string, string> = {
  Draft: '#888',
  Active: '#4caf50',
  Archived: '#ff9800',
}

export default function ApiSpecPanel() {
  const apiSpec = useApiSpec()
  const [showGenerate, setShowGenerate] = useState(false)

  const handleSelect = (spec: ApiSpecDocument) => {
    void apiSpec.select(spec.id)
  }

  const statusBadge = (spec: ApiSpecDocument) => {
    return (
      <span
        style={{
          display: 'inline-block',
          padding: '2px 8px',
          borderRadius: 4,
          fontSize: 11,
          background: STATUS_COLORS[spec.status] ?? '#888',
          color: '#fff',
        }}
      >
        {spec.status}
        {spec.mockEnabled && ' (Mock)'}
      </span>
    )
  }

  return (
    <div className="apispec-panel">
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 12 }}>
        <h3 style={{ margin: 0 }}>API Specs</h3>
        <div style={{ display: 'flex', gap: 8 }}>
          <ImportExportControls
            selectedSpec={apiSpec.selectedSpec}
            onImport={apiSpec.importSpec}
            onExport={apiSpec.exportSpec}
          />
          <button className="btn btn--primary" onClick={() => setShowGenerate(true)}>
            Generate from Traffic
          </button>
        </div>
      </div>

      {apiSpec.error && <div className="error-banner">{apiSpec.error}</div>}

      {showGenerate && (
        <GenerateSpecDialog
          onGenerate={async (req) => {
            await apiSpec.generate(req)
            setShowGenerate(false)
          }}
          onCancel={() => setShowGenerate(false)}
        />
      )}

      <div style={{ display: 'flex', gap: 16, marginTop: 12 }}>
        {/* Spec List */}
        <div style={{ width: 280, flexShrink: 0 }}>
          {apiSpec.loading && <div>Loading...</div>}
          {apiSpec.specs.map((spec) => (
            <div
              key={spec.id}
              onClick={() => handleSelect(spec)}
              style={{
                padding: '8px 12px',
                marginBottom: 4,
                borderRadius: 4,
                cursor: 'pointer',
                background: apiSpec.selectedSpec?.id === spec.id ? '#1a1a2e' : 'transparent',
                border: apiSpec.selectedSpec?.id === spec.id ? '1px solid #4a4a6a' : '1px solid transparent',
              }}
            >
              <div style={{ fontWeight: 500, fontSize: 14 }}>{spec.name}</div>
              <div style={{ fontSize: 12, color: '#888', marginTop: 2 }}>
                {spec.host} {statusBadge(spec)}
              </div>
            </div>
          ))}
          {!apiSpec.loading && apiSpec.specs.length === 0 && (
            <div style={{ color: '#666', fontSize: 13, padding: 12 }}>
              No API specs yet. Generate one from captured traffic or import an OpenAPI JSON.
            </div>
          )}
        </div>

        {/* Spec Detail */}
        {apiSpec.selectedSpec && (
          <div style={{ flex: 1, minWidth: 0 }}>
            <div style={{ display: 'flex', gap: 8, marginBottom: 12 }}>
              {!apiSpec.selectedSpec.mockEnabled ? (
                <button
                  className="btn btn--primary"
                  onClick={() => void apiSpec.activate(apiSpec.selectedSpec!.id)}
                >
                  Activate Mock
                </button>
              ) : (
                <button
                  className="btn btn--secondary"
                  onClick={() => void apiSpec.deactivate(apiSpec.selectedSpec!.id)}
                >
                  Deactivate Mock
                </button>
              )}
              <button
                className="btn btn--secondary"
                onClick={() => void apiSpec.refine(apiSpec.selectedSpec!.id)}
              >
                Refine with LLM
              </button>
              <button
                className="btn btn--danger"
                onClick={() => void apiSpec.remove(apiSpec.selectedSpec!.id)}
              >
                Delete
              </button>
            </div>

            <SpecEditor spec={apiSpec.selectedSpec} onUpdate={apiSpec.update} />

            <ReplacementRulesEditor
              spec={apiSpec.selectedSpec}
              onAddRule={apiSpec.addRule}
              onEditRule={apiSpec.editRule}
              onDeleteRule={apiSpec.removeRule}
            />
          </div>
        )}
      </div>
    </div>
  )
}
