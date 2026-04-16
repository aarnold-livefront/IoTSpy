import type { PresenceEntry } from '../../types/sessions'

interface PresenceIndicatorProps {
  presence: PresenceEntry[]
}

export default function PresenceIndicator({ presence }: PresenceIndicatorProps) {
  if (presence.length === 0) return null

  return (
    <div className="presence-indicator" title={presence.map(p => p.username).join(', ')}>
      <span className="presence-indicator__dot" />
      {presence.slice(0, 5).map((p) => (
        <span key={p.userId + p.joinedAt} className="presence-indicator__avatar" title={p.username}>
          {p.username.slice(0, 2).toUpperCase()}
        </span>
      ))}
      {presence.length > 5 && (
        <span className="presence-indicator__overflow">+{presence.length - 5}</span>
      )}
    </div>
  )
}
