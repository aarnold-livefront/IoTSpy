import '../../styles/header.css'

interface Props {
  isRunning: boolean
  port: number
  signalRConnected: boolean
}

export default function ProxyStatusBadge({ isRunning, port, signalRConnected }: Props) {
  const dotClass = isRunning
    ? 'proxy-status-badge__dot proxy-status-badge__dot--running'
    : signalRConnected
      ? 'proxy-status-badge__dot proxy-status-badge__dot--connecting'
      : 'proxy-status-badge__dot'

  const label = isRunning ? `Proxy: :${port}` : 'Proxy: Stopped'

  return (
    <div className="proxy-status-badge" title={isRunning ? `Listening on port ${port}` : 'Proxy not running'}>
      <span className={dotClass} />
      <span>{label}</span>
    </div>
  )
}
