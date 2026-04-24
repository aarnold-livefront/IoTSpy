import { useEffect, useRef } from 'react'
import '../../styles/modal.css'

interface Props {
  title: string
  message: string
  confirmLabel?: string
  danger?: boolean
  onConfirm: () => void
  onCancel: () => void
}

export default function ConfirmDialog({
  title,
  message,
  confirmLabel = 'Confirm',
  danger = false,
  onConfirm,
  onCancel,
}: Props) {
  const cancelRef = useRef<HTMLButtonElement>(null)

  useEffect(() => {
    cancelRef.current?.focus()
  }, [])

  useEffect(() => {
    const handle = (e: KeyboardEvent) => {
      if (e.key === 'Escape') { e.stopPropagation(); onCancel() }
      if (e.key === 'Enter') { e.stopPropagation(); onConfirm() }
    }
    document.addEventListener('keydown', handle, true)
    return () => document.removeEventListener('keydown', handle, true)
  }, [onConfirm, onCancel])

  return (
    <div
      className="modal-overlay"
      onClick={onCancel}
      role="dialog"
      aria-modal="true"
      aria-labelledby="confirm-dialog-title"
    >
      <div className="modal modal--sm" onClick={(e) => e.stopPropagation()}>
        <div className="modal__header">
          <span className="modal__title" id="confirm-dialog-title">{title}</span>
        </div>
        <div className="modal__body">
          <p style={{ color: 'var(--color-text-muted)', fontSize: 'var(--font-size-md)' }}>{message}</p>
        </div>
        <div className="modal__footer">
          <button ref={cancelRef} className="btn-modal-cancel" onClick={onCancel}>
            Cancel
          </button>
          <button
            className={danger ? 'btn-modal-danger' : 'btn-modal-save'}
            onClick={onConfirm}
          >
            {confirmLabel}
          </button>
        </div>
      </div>
    </div>
  )
}
