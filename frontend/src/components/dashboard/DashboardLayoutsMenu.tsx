import { useEffect, useRef, useState } from 'react'
import { useDashboardLayouts } from '../../hooks/useDashboardLayouts'
import '../../styles/dashboard-layouts.css'

interface Props {
  /** JSON-serialized current dashboard state (e.g. `{ viewMode }`) to persist when saving. */
  currentLayoutJson: string
  /** JSON-serialized current capture filters to persist when saving. */
  currentFiltersJson: string
  /** Called with the saved `layoutJson`/`filtersJson` pair when the user loads a preset. */
  onApply: (layoutJson: string, filtersJson: string) => void
}

export default function DashboardLayoutsMenu({ currentLayoutJson, currentFiltersJson, onApply }: Props) {
  const { layouts, loading, saving, error, save, setDefault, remove } = useDashboardLayouts()
  const [open, setOpen] = useState(false)
  const [name, setName] = useState('')
  const [saveAsDefault, setSaveAsDefault] = useState(false)
  const [formError, setFormError] = useState<string | null>(null)
  const wrapRef = useRef<HTMLDivElement>(null)

  useEffect(() => {
    if (!open) return
    function handleOutsideClick(e: MouseEvent) {
      if (wrapRef.current && !wrapRef.current.contains(e.target as Node)) {
        setOpen(false)
      }
    }
    document.addEventListener('mousedown', handleOutsideClick)
    return () => document.removeEventListener('mousedown', handleOutsideClick)
  }, [open])

  const handleSave = async () => {
    if (!name.trim()) {
      setFormError('Layout name is required.')
      return
    }
    setFormError(null)
    const result = await save(name.trim(), currentLayoutJson, currentFiltersJson, saveAsDefault)
    if (result) {
      setName('')
      setSaveAsDefault(false)
    }
  }

  const handleLoad = (layoutJson: string, filtersJson: string) => {
    onApply(layoutJson, filtersJson)
    setOpen(false)
  }

  return (
    <div className="dashboard-layouts" ref={wrapRef}>
      <button
        className="dashboard-layouts__trigger"
        onClick={() => setOpen(o => !o)}
        aria-haspopup="true"
        aria-expanded={open}
      >
        Layouts{layouts.length > 0 && <span className="dashboard-layouts__count">{layouts.length}</span>}
      </button>

      {open && (
        <div className="dashboard-layouts__panel" role="menu">
          <div className="dashboard-layouts__section-title">Saved layouts</div>

          {loading ? (
            <div className="dashboard-layouts__empty">Loading…</div>
          ) : layouts.length === 0 ? (
            <div className="dashboard-layouts__empty">No saved layouts yet.</div>
          ) : (
            <ul className="dashboard-layouts__list">
              {layouts.map(layout => (
                <li key={layout.id} className="dashboard-layouts__item">
                  <button
                    className="dashboard-layouts__item-name"
                    onClick={() => handleLoad(layout.layoutJson, layout.filtersJson)}
                    title={`Load "${layout.name}"`}
                  >
                    {layout.isDefault && <span className="dashboard-layouts__star" title="Default layout">★</span>}
                    {layout.name}
                  </button>
                  <div className="dashboard-layouts__item-actions">
                    {!layout.isDefault && (
                      <button
                        className="dashboard-layouts__icon-btn"
                        onClick={() => void setDefault(layout.id)}
                        title="Set as default"
                        aria-label={`Set ${layout.name} as default`}
                      >
                        ☆
                      </button>
                    )}
                    <button
                      className="dashboard-layouts__icon-btn dashboard-layouts__icon-btn--danger"
                      onClick={() => void remove(layout.id)}
                      title="Delete layout"
                      aria-label={`Delete ${layout.name}`}
                    >
                      ✕
                    </button>
                  </div>
                </li>
              ))}
            </ul>
          )}

          <div className="dashboard-layouts__divider" />

          <div className="dashboard-layouts__section-title">Save current view</div>
          <div className="dashboard-layouts__form">
            <input
              className="dashboard-layouts__input"
              type="text"
              placeholder="Layout name"
              value={name}
              onChange={e => setName(e.target.value)}
              aria-label="Layout name"
            />
            <label className="dashboard-layouts__checkbox-row">
              <input
                type="checkbox"
                checked={saveAsDefault}
                onChange={e => setSaveAsDefault(e.target.checked)}
              />
              Set as default
            </label>
            <button
              className="dashboard-layouts__save-btn"
              onClick={() => void handleSave()}
              disabled={saving}
            >
              {saving ? 'Saving…' : 'Save Layout'}
            </button>
          </div>

          {(formError || error) && (
            <div className="dashboard-layouts__error" role="alert">{formError ?? error}</div>
          )}
        </div>
      )}
    </div>
  )
}
