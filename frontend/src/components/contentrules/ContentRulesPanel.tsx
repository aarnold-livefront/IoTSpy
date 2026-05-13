import { useEffect, useMemo, useRef, useState } from 'react'
import { useContentRules } from '../../hooks/useContentRules'
import { previewContentRule } from '../../api/contentrules'
import { useFocusTrap } from '../../hooks/useFocusTrap'
import AssetLibrary from '../apispec/AssetLibrary'
import RulePreviewModal from '../apispec/RulePreviewModal'
import type {
  AssetInfo,
  ContentMatchType,
  ContentReplacementAction,
  ContentReplacementRule,
} from '../../types/api'
import '../../styles/content-rules.css'
import '../../styles/modal.css'

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

interface AssetPickerModalProps {
  onClose: () => void
  onPick: (a: AssetInfo) => void
}

function AssetPickerModal({ onClose, onPick }: AssetPickerModalProps) {
  const dialogRef = useRef<HTMLDivElement>(null)
  const onCloseRef = useRef(onClose)
  useFocusTrap(dialogRef)

  useEffect(() => { onCloseRef.current = onClose })

  useEffect(() => {
    const handle = (e: KeyboardEvent) => { if (e.key === 'Escape') onCloseRef.current() }
    document.addEventListener('keydown', handle)
    return () => document.removeEventListener('keydown', handle)
  }, [])

  return (
    <div
      className="modal-overlay"
      onClick={onClose}
    >
      <div
        ref={dialogRef}
        className="modal asset-picker-modal"
        onClick={(e) => e.stopPropagation()}
        role="dialog"
        aria-modal="true"
        aria-labelledby="asset-picker-title"
      >
        <div className="modal__header">
          <span className="modal__title" id="asset-picker-title">Pick an asset</span>
          <button className="btn btn--secondary" onClick={onClose} aria-label="Close asset picker">×</button>
        </div>
        <div className="modal__body">
          <AssetLibrary compact onPick={onPick} />
        </div>
      </div>
    </div>
  )
}

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
    <div className="cr-panel">
      <div className="cr-panel__header">
        <h3 style={{ margin: 0 }}>
          Content Rules{' '}
          {loading && <span className="cr-panel__loading">Loading…</span>}
        </h3>
        <button className="btn btn--primary" onClick={() => setShowAdd(true)} style={{ fontSize: 'var(--font-size-sm)' }}>
          Add Rule
        </button>
      </div>

      <div className="cr-host-filter">
        <input
          value={hostFilter}
          onChange={(e) => setHostFilter(e.target.value)}
          placeholder="Filter by host (e.g. ads.example.com)"
        />
      </div>

      {error && <div className="cr-error">{error}</div>}

      {showAdd && (
        <div className="cr-add-form">
          <div className="cr-add-form__grid">
            <label>
              <span className="cr-add-form__hint">Name</span>
              <input value={formName} onChange={(e) => setFormName(e.target.value)} />
            </label>
            <label>
              <span className="cr-add-form__hint">Host (exact, required)</span>
              <input value={hostFilter} onChange={(e) => setHostFilter(e.target.value)} placeholder="ads.example.com" />
            </label>
            <label>
              <span className="cr-add-form__hint">Match Type</span>
              <select value={formMatchType} onChange={(e) => setFormMatchType(e.target.value as ContentMatchType)}>
                {MATCH_TYPES.map((t) => <option key={t.value} value={t.value}>{t.label}</option>)}
              </select>
            </label>
            <label>
              <span className="cr-add-form__hint">
                Match Pattern
                {formMatchType === 'ContentType' && ' (e.g. image/*, video/mp4)'}
                {formMatchType === 'JsonPath' && ' (e.g. $.data.imageUrl)'}
                {formMatchType === 'BodyRegex' && ' (regex)'}
              </span>
              <input value={formMatchPattern} onChange={(e) => setFormMatchPattern(e.target.value)} />
            </label>
            <label>
              <span className="cr-add-form__hint">Action</span>
              <select value={formAction} onChange={(e) => setFormAction(e.target.value as ContentReplacementAction)}>
                {ACTIONS.map((a) => <option key={a.value} value={a.value}>{a.label}</option>)}
              </select>
            </label>
            {needsFile && (
              <label className="cr-add-form__full">
                <span className="cr-add-form__hint">{isSse ? 'Event file (.sse or .ndjson)' : 'Asset file path'}</span>
                <div className="cr-add-form__file-row">
                  <input value={formFilePath} onChange={(e) => setFormFilePath(e.target.value)} placeholder="Pick from library or paste path" />
                  <button className="btn btn--secondary" onClick={() => setShowPicker(true)} style={{ fontSize: 'var(--font-size-xs)', whiteSpace: 'nowrap' }}>Pick asset</button>
                </div>
              </label>
            )}
            {needsValue && (
              <label className="cr-add-form__full">
                <span className="cr-add-form__hint">Replacement Value</span>
                <input value={formValue} onChange={(e) => setFormValue(e.target.value)} />
              </label>
            )}
            {(needsFile || needsValue) && (
              <label>
                <span className="cr-add-form__hint">Override Content-Type</span>
                <input value={formContentType} onChange={(e) => setFormContentType(e.target.value)} placeholder="e.g. image/png" />
              </label>
            )}
            {isSse && (
              <>
                <label>
                  <span className="cr-add-form__hint">Inter-event delay (ms)</span>
                  <input type="number" min={0} value={formSseDelay} onChange={(e) => setFormSseDelay(Number(e.target.value))} />
                </label>
                <label className="cr-add-form__sse-check">
                  <input type="checkbox" checked={formSseLoop} onChange={(e) => setFormSseLoop(e.target.checked)} />
                  <span>Loop forever</span>
                </label>
              </>
            )}
            <label>
              <span className="cr-add-form__hint">Priority</span>
              <input type="number" value={formPriority} onChange={(e) => setFormPriority(Number(e.target.value))} />
            </label>
            <label>
              <span className="cr-add-form__hint">Host Pattern (regex, optional)</span>
              <input value={formHostPattern} onChange={(e) => setFormHostPattern(e.target.value)} placeholder="e.g. ads\.example\.com" />
            </label>
            <label className="cr-add-form__full">
              <span className="cr-add-form__hint">Path Pattern (regex, optional)</span>
              <input value={formPathPattern} onChange={(e) => setFormPathPattern(e.target.value)} placeholder="e.g. /ads/.*\.gif" />
            </label>
          </div>
          <div className="cr-add-form__actions">
            <button className="btn btn--primary" onClick={() => void handleAdd()}>Add</button>
            <button className="btn btn--secondary" onClick={resetForm}>Cancel</button>
          </div>
        </div>
      )}

      <table className="cr-table">
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
                  <div style={{ fontSize: 'var(--font-size-xs)', color: 'var(--color-text-muted)', marginTop: 2 }}>
                    {rule.hostPattern && <span>host: <code>{rule.hostPattern}</code></span>}
                    {rule.hostPattern && rule.pathPattern && ' · '}
                    {rule.pathPattern && <span>path: <code>{rule.pathPattern}</code></span>}
                  </div>
                )}
              </td>
              <td style={{ fontSize: 'var(--font-size-xs)', color: 'var(--color-text-muted)' }}>{rule.host}</td>
              <td><code style={{ fontSize: 'var(--font-size-xs)' }}>{rule.matchType}: {rule.matchPattern}</code></td>
              <td>{rule.action}</td>
              <td style={{ textAlign: 'center' }}>{rule.priority}</td>
              <td style={{ textAlign: 'center' }}>
                <input type="checkbox" checked={rule.enabled} onChange={() => void handleToggle(rule)} />
              </td>
              <td style={{ whiteSpace: 'nowrap' }}>
                <button className="btn btn--secondary" style={{ fontSize: 'var(--font-size-xs)', padding: '2px 6px', marginRight: 4 }} onClick={() => setPreviewTarget(rule)}>Preview</button>
                <button className="btn btn--danger" style={{ fontSize: 'var(--font-size-xs)', padding: '2px 6px' }} onClick={() => void removeRule('', rule.id)}>Delete</button>
              </td>
            </tr>
          ))}
          {!loading && filteredRules.length === 0 && (
            <tr><td colSpan={7} className="cr-table__empty">
              {rules.length === 0 ? 'No content rules yet. Click Add Rule to get started.' : 'No rules match the current host filter.'}
            </td></tr>
          )}
        </tbody>
      </table>

      {showPicker && (
        <AssetPickerModal
          onClose={() => setShowPicker(false)}
          onPick={(a: AssetInfo) => { setFormFilePath(a.filePath); setShowPicker(false) }}
        />
      )}

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
