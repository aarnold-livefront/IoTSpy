import { useEffect } from 'react'

interface ShortcutMap {
  onDelete?: () => void
  onEscape?: () => void
  onSave?: () => void
}

export function useKeyboardShortcuts({ onDelete, onEscape, onSave }: ShortcutMap) {
  useEffect(() => {
    const handle = (e: KeyboardEvent) => {
      const tag = (e.target as Element).tagName
      const isEditable = tag === 'INPUT' || tag === 'TEXTAREA' || tag === 'SELECT' ||
        (e.target as HTMLElement).isContentEditable

      if (e.key === 'Escape') {
        onEscape?.()
      } else if (e.key === 'Delete' && !isEditable && onDelete) {
        e.preventDefault()
        onDelete()
      } else if ((e.ctrlKey || e.metaKey) && e.key === 's' && onSave) {
        e.preventDefault()
        onSave()
      }
    }
    document.addEventListener('keydown', handle)
    return () => document.removeEventListener('keydown', handle)
  }, [onDelete, onEscape, onSave])
}
