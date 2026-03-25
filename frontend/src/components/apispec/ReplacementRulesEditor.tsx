import { useState } from 'react'
import type {
  ApiSpecDocument,
  ContentMatchType,
  ContentReplacementAction,
  ContentReplacementRule,
  CreateReplacementRuleRequest,
  UpdateReplacementRuleRequest,
} from '../../types/api'

interface Props {
  spec: ApiSpecDocument
  onAddRule: (specId: string, req: CreateReplacementRuleRequest) => Promise<ContentReplacementRule | null>
  onEditRule: (specId: string, ruleId: string, req: UpdateReplacementRuleRequest) => Promise<ContentReplacementRule | null>
  onDeleteRule: (specId: string, ruleId: string) => Promise<void>
}

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
]

export default function ReplacementRulesEditor({ spec, onAddRule, onEditRule, onDeleteRule }: Props) {
  const [showAdd, setShowAdd] = useState(false)
  const [formName, setFormName] = useState('')
  const [formMatchType, setFormMatchType] = useState<ContentMatchType>('ContentType')
  const [formMatchPattern, setFormMatchPattern] = useState('')
  const [formAction, setFormAction] = useState<ContentReplacementAction>('Redact')
  const [formValue, setFormValue] = useState('')
  const [formFilePath, setFormFilePath] = useState('')
  const [formContentType, setFormContentType] = useState('')
  const [formPriority, setFormPriority] = useState(0)

  const resetForm = () => {
    setFormName('')
    setFormMatchType('ContentType')
    setFormMatchPattern('')
    setFormAction('Redact')
    setFormValue('')
    setFormFilePath('')
    setFormContentType('')
    setFormPriority(0)
    setShowAdd(false)
  }

  const handleAdd = async () => {
    if (!formName.trim() || !formMatchPattern.trim()) return
    const req: CreateReplacementRuleRequest = {
      name: formName,
      matchType: formMatchType,
      matchPattern: formMatchPattern,
      action: formAction,
      replacementValue: formValue || undefined,
      replacementFilePath: formFilePath || undefined,
      replacementContentType: formContentType || undefined,
      priority: formPriority,
    }
    await onAddRule(spec.id, req)
    resetForm()
  }

  const handleToggle = async (rule: ContentReplacementRule) => {
    await onEditRule(spec.id, rule.id, { enabled: !rule.enabled })
  }

  const rules = spec.replacementRules ?? []

  return (
    <div style={{ marginTop: 16 }}>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
        <h4 style={{ margin: 0 }}>Content Replacement Rules ({rules.length})</h4>
        <button className="btn btn--primary" onClick={() => setShowAdd(true)} style={{ fontSize: 12 }}>
          Add Rule
        </button>
      </div>

      {showAdd && (
        <div
          style={{
            marginTop: 8,
            padding: 12,
            background: '#1a1a2e',
            borderRadius: 6,
            border: '1px solid #4a4a6a',
          }}
        >
          <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 8 }}>
            <label>
              <span style={{ fontSize: 11, color: '#aaa' }}>Name</span>
              <input value={formName} onChange={(e) => setFormName(e.target.value)} style={{ width: '100%' }} />
            </label>
            <label>
              <span style={{ fontSize: 11, color: '#aaa' }}>Match Type</span>
              <select
                value={formMatchType}
                onChange={(e) => setFormMatchType(e.target.value as ContentMatchType)}
                style={{ width: '100%' }}
              >
                {MATCH_TYPES.map((t) => (
                  <option key={t.value} value={t.value}>
                    {t.label}
                  </option>
                ))}
              </select>
            </label>
            <label>
              <span style={{ fontSize: 11, color: '#aaa' }}>
                Match Pattern
                {formMatchType === 'ContentType' && ' (e.g. image/*, video/mp4)'}
                {formMatchType === 'JsonPath' && ' (e.g. $.data.imageUrl)'}
                {formMatchType === 'BodyRegex' && ' (regex)'}
              </span>
              <input
                value={formMatchPattern}
                onChange={(e) => setFormMatchPattern(e.target.value)}
                style={{ width: '100%' }}
              />
            </label>
            <label>
              <span style={{ fontSize: 11, color: '#aaa' }}>Action</span>
              <select
                value={formAction}
                onChange={(e) => setFormAction(e.target.value as ContentReplacementAction)}
                style={{ width: '100%' }}
              >
                {ACTIONS.map((a) => (
                  <option key={a.value} value={a.value}>
                    {a.label}
                  </option>
                ))}
              </select>
            </label>
            {formAction !== 'Redact' && (
              <>
                <label>
                  <span style={{ fontSize: 11, color: '#aaa' }}>
                    {formAction === 'ReplaceWithFile' ? 'File Path' : 'Replacement Value'}
                  </span>
                  <input
                    value={formAction === 'ReplaceWithFile' ? formFilePath : formValue}
                    onChange={(e) =>
                      formAction === 'ReplaceWithFile'
                        ? setFormFilePath(e.target.value)
                        : setFormValue(e.target.value)
                    }
                    style={{ width: '100%' }}
                  />
                </label>
                <label>
                  <span style={{ fontSize: 11, color: '#aaa' }}>Override Content-Type</span>
                  <input
                    value={formContentType}
                    onChange={(e) => setFormContentType(e.target.value)}
                    placeholder="e.g. image/png"
                    style={{ width: '100%' }}
                  />
                </label>
              </>
            )}
            <label>
              <span style={{ fontSize: 11, color: '#aaa' }}>Priority</span>
              <input
                type="number"
                value={formPriority}
                onChange={(e) => setFormPriority(Number(e.target.value))}
                style={{ width: '100%' }}
              />
            </label>
          </div>
          <div style={{ display: 'flex', gap: 8, marginTop: 8 }}>
            <button className="btn btn--primary" onClick={() => void handleAdd()}>
              Add
            </button>
            <button className="btn btn--secondary" onClick={resetForm}>
              Cancel
            </button>
          </div>
        </div>
      )}

      <table style={{ width: '100%', marginTop: 8, fontSize: 13 }}>
        <thead>
          <tr>
            <th style={{ textAlign: 'left' }}>Name</th>
            <th style={{ textAlign: 'left' }}>Match</th>
            <th style={{ textAlign: 'left' }}>Action</th>
            <th style={{ textAlign: 'center' }}>Priority</th>
            <th style={{ textAlign: 'center' }}>Enabled</th>
            <th></th>
          </tr>
        </thead>
        <tbody>
          {rules.map((rule) => (
            <tr key={rule.id}>
              <td>{rule.name}</td>
              <td>
                <code style={{ fontSize: 11 }}>
                  {rule.matchType}: {rule.matchPattern}
                </code>
              </td>
              <td>{rule.action}</td>
              <td style={{ textAlign: 'center' }}>{rule.priority}</td>
              <td style={{ textAlign: 'center' }}>
                <input
                  type="checkbox"
                  checked={rule.enabled}
                  onChange={() => void handleToggle(rule)}
                />
              </td>
              <td>
                <button
                  className="btn btn--danger"
                  style={{ fontSize: 11, padding: '2px 6px' }}
                  onClick={() => void onDeleteRule(spec.id, rule.id)}
                >
                  Delete
                </button>
              </td>
            </tr>
          ))}
          {rules.length === 0 && (
            <tr>
              <td colSpan={6} style={{ textAlign: 'center', color: '#666', padding: 16 }}>
                No replacement rules. Add rules to replace content types (images, video) or JSON fields.
              </td>
            </tr>
          )}
        </tbody>
      </table>
    </div>
  )
}
