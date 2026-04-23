import { useMemo, useState } from 'react'
import { useContentRules } from '../../hooks/useContentRules'
import { previewContentRule } from '../../api/contentrules'
import AssetLibrary from '../apispec/AssetLibrary'
import RulePreviewModal from '../apispec/RulePreviewModal'
import type {
  AssetInfo,
  ContentMatchType,
  ContentReplacementAction,
  ContentReplacementRule,
} from '../../types/api'

const MATCH_TYPES: { value: ContentMatchType; label: string }[] = [
  { value: 'ContentType', label: 'Content-Type' },
  { value: 'JsonPath', label: 'JSON Path' },
  { value: 'HeaderValue', label: 'Header Value' },
  { value: 'BodyRegex', label: 'Body Regex' },
]

const ACTIONS: { value: ContentReplacementAction; label: string }[] = [
  { value: 'ReplaceWithFile', label: 'Replace with File' },
  { value: 'ReplaceWithValue', label: 'Replace with Value' },
  { value: 'ReplaceWithUrl', label: 'Replace with URL' },
  { value: 'Redact', label: 'Redact' },
  { value: 'TrackingPixel', label: 'Tracking Pixel (1×1 GIF)' },
  { value: 'MockSseStream', label: 'Mock SSE Stream' },
]

export default function ContentRulesPanel() {
  const [hostFilter, setHostFilter] = useState('')
  const { rules, loading, error, addRule, editRule, removeRule } = useContentRules()

  const filteredRules = useMemo(() => {
    const f = hostFilter.trim().toLowerCase()
    return f ? rules.filter((r) => r.host?.toLowerCase().includes(f)) : rules
  }, [rules, hostFilter])

  const [showAdd, setShowAdd] = useState(false)
  const [showPicker, setShowPicker] = useState(false)
  const [previewTarget, setPreviewTarget] = useState<ContentReplacementRule | null>(null)

  const [formName, setFormName] = useState('')
  const [formMatchType, setFormMatchType] = useState<ContentMatchType>('ContentType')
  const [formMatchPattern, setFormMatchPattern] = useState('')
  const [formAction, setFormAction] = useState<ContentReplacementAction>('Redact')
  const [formValue, setFormValue] = useState('')
  const [formFilePath, setFormFilePath] = useState('')
  const [formContentType, setFormContentType] = useState('')
  const [formPriority, setFormPriority] = useState(0)
  const [formSseDelay, setFormSseDelay] = useState(0)
  const [formSseLoop, setFormSseLoop] = useState(false)
  const [formHostPattern, setFormHostPattern] = useState('')
  const [formPathPattern, setFormPathPattern] = useState('')

  const needsFile = formAction === 'ReplaceWithFile' || formAction === 'MockSseStream'
  const needsValue = formAction === 'ReplaceWithValue' || formAction === 'ReplaceWithUrl'
  const isSse = formAction === 'MockSseStream'

  const resetForm = () => {
    setFormName(''); setFormMatchPattern(''); setFormAction('Redact')
    setFormValue(''); setFormFilePath(''); setFormContentType('')
    setFormPriority(0); setFormSseDelay(0); setFormSseLoop(false)
    setFormHostPattern(''); setFormPathPattern('')
    setShowAdd(false)
  }

  const handleAdd = async () => {
    if (!formName.trim() || !formMatchPattern.trim()) return
    await addRule('', {
      host: hostFilter.trim(),
      name: formName,
      matchType: formMatchType,
      matchPattern: formMatchPattern,
      action: formAction,
      replacementValue: needsValue ? formValue || undefined : undefined,
      replacementFilePath: needsFile ? formFilePath || undefined : undefined,
      replacementContentType: formContentType || undefined,
      priority: formPriority,
      sseInterEventDelayMs: isSse ? formSseDelay : undefined,
      sseLoop: isSse ? formSseLoop : undefined,
      hostPattern: formHostPattern.trim() || undefined,
      pathPattern: formPathPattern.trim() || undefined,
    })
    resetForm()
  }

  const handleToggle = (rule: ContentReplacementRule) =>
    editRule('', rule.id, { enabled: !rule.enabled })

  return (
    <div style={{ padding: 16 }}>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 12 }}>
        <h3 style={{ margin: 0 }}>Content Rules {loading && <span style={{ fontSize: 12, color: '#888' }}>Loading…</span>}</h3>
        <button className="btn btn--primary" onClick={() => setShowAdd(true)} style={{ fontSize: 12 }}>
          Add Rule
        </button>
      </div>

      {/* Host filter */}
      <div style={{ marginBottom: 12 }}>
        <input
          value={hostFilter}
          onChange={(e) => setHostFilter(e.target.value)}
          placeholder="Filter by host (e.g. ads.example.com)"
          style={{ width: '100%', maxWidth: 400 }}
        />
      </div>

      {error && (
        <div style={{ padding: 8, background: '#3a1a1a', color: '#fca5a5', borderRadius: 4, marginBottom: 8, fontSize: 12 }}>
          {error}
        </div>
      )}

      {showAdd && (
        <div style={{ marginTop: 8, marginBottom: 12, padding: 12, background: '#1a1a2e', borderRadius: 6, border: '1px solid #4a4a6a' }}>
          <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 8 }}>
            <label>
              <span style={{ fontSize: 11, color: '#aaa' }}>Name</span>
              <input value={formName} onChange={(e) => setFormName(e.target.value)} style={{ width: '100%' }} />
            </label>
            <label>
              <span style={{ fontSize: 11, color: '#aaa' }}>Host (exact, required)</span>
              <input value={hostFilter} onChange={(e) => setHostFilter(e.target.value)} placeholder="ads.example.com" style={{ width: '100%' }} />
            </label>
            <label>
              <span style={{ fontSize: 11, color: '#aaa' }}>Match Type</span>
              <select value={formMatchType} onChange={(e) => setFormMatchType(e.target.value as ContentMatchType)} style={{ width: '100%' }}>
                {MATCH_TYPES.map((t) => <option key={t.value} value={t.value}>{t.label}</option>)}
              </select>
            </label>
            <label>
              <span style={{ fontSize: 11, color: '#aaa' }}>
                Match Pattern
                {formMatchType === 'ContentType' && ' (e.g. image/*, video/mp4)'}
                {formMatchType === 'JsonPath' && ' (e.g. $.data.imageUrl)'}
                {formMatchType === 'BodyRegex' && ' (regex)'}
              </span>
              <input value={formMatchPattern} onChange={(e) => setFormMatchPattern(e.target.value)} style={{ width: '100%' }} />
            </label>
            <label>
              <span style={{ fontSize: 11, color: '#aaa' }}>Action</span>
              <select value={formAction} onChange={(e) => setFormAction(e.target.value as ContentReplacementAction)} style={{ width: '100%' }}>
                {ACTIONS.map((a) => <option key={a.value} value={a.value}>{a.label}</option>)}
              </select>
            </label>
            {needsFile && (
              <label style={{ gridColumn: '1 / -1' }}>
                <span style={{ fontSize: 11, color: '#aaa' }}>{isSse ? 'Event file (.sse or .ndjson)' : 'Asset file path'}</span>
                <div style={{ display: 'flex', gap: 4 }}>
                  <input value={formFilePath} onChange={(e) => setFormFilePath(e.target.value)} style={{ flex: 1 }} placeholder="Pick from library or paste path" />
                  <button className="btn btn--secondary" onClick={() => setShowPicker(true)} style={{ fontSize: 11, whiteSpace: 'nowrap' }}>Pick asset</button>
                </div>
              </label>
            )}
            {needsValue && (
              <label style={{ gridColumn: '1 / -1' }}>
                <span style={{ fontSize: 11, color: '#aaa' }}>Replacement Value</span>
                <input value={formValue} onChange={(e) => setFormValue(e.target.value)} style={{ width: '100%' }} />
              </label>
            )}
            {(needsFile || needsValue) && (
              <label>
                <span style={{ fontSize: 11, color: '#aaa' }}>Override Content-Type</span>
                <input value={formContentType} onChange={(e) => setFormContentType(e.target.value)} placeholder="e.g. image/png" style={{ width: '100%' }} />
              </label>
            )}
            {isSse && (
              <>
                <label>
                  <span style={{ fontSize: 11, color: '#aaa' }}>Inter-event delay (ms)</span>
                  <input type="number" min={0} value={formSseDelay} onChange={(e) => setFormSseDelay(Number(e.target.value))} style={{ width: '100%' }} />
                </label>
                <label style={{ display: 'flex', alignItems: 'center', gap: 6, marginTop: 16 }}>
                  <input type="checkbox" checked={formSseLoop} onChange={(e) => setFormSseLoop(e.target.checked)} />
                  <span style={{ fontSize: 12 }}>Loop forever</span>
                </label>
              </>
            )}
            <label>
              <span style={{ fontSize: 11, color: '#aaa' }}>Priority</span>
              <input type="number" value={formPriority} onChange={(e) => setFormPriority(Number(e.target.value))} style={{ width: '100%' }} />
            </label>
            <label>
              <span style={{ fontSize: 11, color: '#aaa' }}>Host Pattern (regex, optional)</span>
              <input value={formHostPattern} onChange={(e) => setFormHostPattern(e.target.value)} placeholder="e.g. ads\.example\.com" style={{ width: '100%' }} />
            </label>
            <label style={{ gridColumn: '1 / -1' }}>
              <span style={{ fontSize: 11, color: '#aaa' }}>Path Pattern (regex, optional)</span>
              <input value={formPathPattern} onChange={(e) => setFormPathPattern(e.target.value)} placeholder="e.g. /ads/.*\.gif" style={{ width: '100%' }} />
            </label>
          </div>
          <div style={{ display: 'flex', gap: 8, marginTop: 8 }}>
            <button className="btn btn--primary" onClick={() => void handleAdd()}>Add</button>
            <button className="btn btn--secondary" onClick={resetForm}>Cancel</button>
          </div>
        </div>
      )}

      <table style={{ width: '100%', marginTop: 8, fontSize: 13 }}>
        <thead>
          <tr>
            <th style={{ textAlign: 'left' }}>Name</th>
            <th style={{ textAlign: 'left' }}>Host</th>
            <th style={{ textAlign: 'left' }}>Match</th>
            <th style={{ textAlign: 'left' }}>Action</th>
            <th style={{ textAlign: 'center' }}>Pri</th>
            <th style={{ textAlign: 'center' }}>On</th>
            <th></th>
          </tr>
        </thead>
        <tbody>
          {filteredRules.map((rule) => (
            <tr key={rule.id}>
              <td>
                <div>{rule.name}</div>
                {(rule.hostPattern || rule.pathPattern) && (
                  <div style={{ fontSize: 10, color: '#888', marginTop: 2 }}>
                    {rule.hostPattern && <span>host: <code>{rule.hostPattern}</code></span>}
                    {rule.hostPattern && rule.pathPattern && ' · '}
                    {rule.pathPattern && <span>path: <code>{rule.pathPattern}</code></span>}
                  </div>
                )}
              </td>
              <td style={{ fontSize: 11, color: '#aaa' }}>{rule.host}</td>
              <td><code style={{ fontSize: 11 }}>{rule.matchType}: {rule.matchPattern}</code></td>
              <td>{rule.action}</td>
              <td style={{ textAlign: 'center' }}>{rule.priority}</td>
              <td style={{ textAlign: 'center' }}>
                <input type="checkbox" checked={rule.enabled} onChange={() => void handleToggle(rule)} />
              </td>
              <td style={{ whiteSpace: 'nowrap' }}>
                <button className="btn btn--secondary" style={{ fontSize: 11, padding: '2px 6px', marginRight: 4 }} onClick={() => setPreviewTarget(rule)}>Preview</button>
                <button className="btn btn--danger" style={{ fontSize: 11, padding: '2px 6px' }} onClick={() => void removeRule('', rule.id)}>Delete</button>
              </td>
            </tr>
          ))}
          {!loading && filteredRules.length === 0 && (
            <tr><td colSpan={7} style={{ textAlign: 'center', color: '#666', padding: 16 }}>
              {rules.length === 0 ? 'No content rules yet. Click Add Rule to get started.' : 'No rules match the current host filter.'}
            </td></tr>
          )}
        </tbody>
      </table>

      {/* Asset picker modal */}
      {showPicker && (
        <div onClick={() => setShowPicker(false)} style={{ position: 'fixed', inset: 0, background: 'rgba(0,0,0,0.6)', display: 'flex', alignItems: 'center', justifyContent: 'center', zIndex: 1000 }}>
          <div onClick={(e) => e.stopPropagation()} style={{ width: 'min(720px, 94vw)', maxHeight: '90vh', overflow: 'auto', background: '#1a1a2e', border: '1px solid #4a4a6a', borderRadius: 8, padding: 20 }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 8 }}>
              <h3 style={{ margin: 0 }}>Pick an asset</h3>
              <button className="btn btn--secondary" onClick={() => setShowPicker(false)}>Close</button>
            </div>
            <AssetLibrary compact onPick={(a: AssetInfo) => { setFormFilePath(a.filePath); setShowPicker(false) }} />
          </div>
        </div>
      )}

      {/* Preview modal — uses the content-rules preview endpoint */}
      {previewTarget && (
        <RulePreviewModal
          specId=""
          ruleId={previewTarget.id}
          ruleName={previewTarget.name}
          onClose={() => setPreviewTarget(null)}
          overridePreview={previewContentRule}
        />
      )}
    </div>
  )
}
