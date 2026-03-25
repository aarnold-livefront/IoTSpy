import { useState } from 'react'
import type { ApiSpecDocument, UpdateSpecRequest } from '../../types/api'

interface Props {
  spec: ApiSpecDocument
  onUpdate: (id: string, req: UpdateSpecRequest) => Promise<ApiSpecDocument | null>
}

export default function SpecEditor({ spec, onUpdate }: Props) {
  const [editing, setEditing] = useState(false)
  const [name, setName] = useState(spec.name)
  const [description, setDescription] = useState(spec.description)
  const [passthroughFirst, setPassthroughFirst] = useState(spec.passthroughFirst)
  const [showJson, setShowJson] = useState(false)

  const handleSave = async () => {
    await onUpdate(spec.id, { name, description, passthroughFirst })
    setEditing(false)
  }

  // Try to format OpenAPI JSON
  let formattedJson = spec.openApiJson
  try {
    formattedJson = JSON.stringify(JSON.parse(spec.openApiJson), null, 2)
  } catch {
    // keep as-is
  }

  // Count endpoints
  let endpointCount = 0
  try {
    const parsed = JSON.parse(spec.openApiJson)
    if (parsed.paths) {
      for (const path of Object.values(parsed.paths)) {
        endpointCount += Object.keys(path as object).length
      }
    }
  } catch {
    // ignore
  }

  return (
    <div style={{ marginBottom: 16 }}>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
        <div>
          {editing ? (
            <div style={{ display: 'flex', gap: 8, alignItems: 'center' }}>
              <input value={name} onChange={(e) => setName(e.target.value)} placeholder="Spec name" />
              <button className="btn btn--primary" onClick={() => void handleSave()}>
                Save
              </button>
              <button className="btn btn--secondary" onClick={() => setEditing(false)}>
                Cancel
              </button>
            </div>
          ) : (
            <h4
              style={{ margin: 0, cursor: 'pointer' }}
              onClick={() => setEditing(true)}
              title="Click to edit"
            >
              {spec.name}
            </h4>
          )}
          <div style={{ fontSize: 12, color: '#888', marginTop: 4 }}>
            Host: {spec.host} | Version: {spec.version} | {endpointCount} endpoints |{' '}
            {spec.useLlmAnalysis ? 'LLM-enhanced' : 'Raw inference'}
          </div>
        </div>
      </div>

      {editing && (
        <div style={{ marginTop: 8 }}>
          <label style={{ fontSize: 12, color: '#aaa' }}>Description</label>
          <textarea
            value={description}
            onChange={(e) => setDescription(e.target.value)}
            rows={2}
            style={{ width: '100%' }}
          />
          <label style={{ display: 'flex', alignItems: 'center', gap: 6, marginTop: 4 }}>
            <input
              type="checkbox"
              checked={passthroughFirst}
              onChange={(e) => setPassthroughFirst(e.target.checked)}
            />
            <span style={{ fontSize: 13 }}>Passthrough-first mode (observe real traffic before mocking)</span>
          </label>
        </div>
      )}

      <div style={{ marginTop: 8 }}>
        <button
          className="btn btn--secondary"
          onClick={() => setShowJson(!showJson)}
          style={{ fontSize: 12 }}
        >
          {showJson ? 'Hide' : 'Show'} OpenAPI JSON
        </button>
      </div>

      {showJson && (
        <pre
          style={{
            marginTop: 8,
            padding: 12,
            background: '#0d0d1a',
            borderRadius: 4,
            overflow: 'auto',
            maxHeight: 400,
            fontSize: 12,
            lineHeight: 1.4,
          }}
        >
          {formattedJson}
        </pre>
      )}
    </div>
  )
}
