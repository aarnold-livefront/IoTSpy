import type { RiskTag } from '../../types/analytics'
import './RiskTagBadge.css'

interface Props {
  tag: RiskTag
  confidence?: number
}

const TAG_LABELS: Record<RiskTag, string> = {
  ExfiltrationRisk: 'Exfil',
  PiiDetected: 'PII',
  DataBroker: 'Broker',
  SuspiciousTls: 'TLS',
  UnusualPort: 'Port',
  MqttCredentialExposure: 'MQTT Cred',
  DnsTunneling: 'DNS Tunnel',
  HighEntropyPayload: 'Entropy'
}

export default function RiskTagBadge({ tag, confidence }: Props) {
  const title = confidence != null
    ? `${tag} (${Math.round(confidence * 100)}% confidence)`
    : tag

  return (
    <span
      className={`risk-tag risk-tag--${tag.toLowerCase().replace(/[^a-z]/g, '-')}`}
      title={title}
    >
      {TAG_LABELS[tag]}
    </span>
  )
}
