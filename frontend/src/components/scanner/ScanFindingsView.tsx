import type { ScanFinding, ScanFindingSeverity } from '../../types/api'
import '../../styles/scanner.css'

interface Props {
  findings: ScanFinding[]
}

const severityOrder: Record<ScanFindingSeverity, number> = {
  Critical: 0,
  High: 1,
  Medium: 2,
  Low: 3,
  Info: 4,
}

const severityClass: Record<ScanFindingSeverity, string> = {
  Critical: 'severity--critical',
  High: 'severity--high',
  Medium: 'severity--medium',
  Low: 'severity--low',
  Info: 'severity--info',
}

function groupBySeverity(findings: ScanFinding[]): Record<ScanFindingSeverity, ScanFinding[]> {
  const groups: Record<string, ScanFinding[]> = {}
  for (const f of findings) {
    if (!groups[f.severity]) groups[f.severity] = []
    groups[f.severity].push(f)
  }
  return groups as Record<ScanFindingSeverity, ScanFinding[]>
}

export default function ScanFindingsView({ findings }: Props) {
  if (findings.length === 0) {
    return <div className="scan-findings__empty">No findings for this scan.</div>
  }

  const grouped = groupBySeverity(findings)
  const sortedKeys = (Object.keys(grouped) as ScanFindingSeverity[]).sort(
    (a, b) => severityOrder[a] - severityOrder[b],
  )

  return (
    <div className="scan-findings">
      {sortedKeys.map((severity) => (
        <div key={severity} className="scan-findings__group">
          <div className={`scan-findings__group-header ${severityClass[severity]}`}>
            <span className={`severity-badge ${severityClass[severity]}`}>{severity}</span>
            <span className="scan-findings__group-count">
              {grouped[severity].length} finding{grouped[severity].length !== 1 ? 's' : ''}
            </span>
          </div>
          {grouped[severity].map((finding) => (
            <div key={finding.id} className="scan-finding-card">
              <div className="scan-finding-card__header">
                <span className="scan-finding-card__type">{finding.type}</span>
                {finding.port > 0 && (
                  <span className="scan-finding-card__port">Port {finding.port}/{finding.protocol}</span>
                )}
                {finding.service && (
                  <span className="scan-finding-card__service">{finding.service}</span>
                )}
                {finding.cveId && (
                  <span className="scan-finding-card__cve">{finding.cveId}</span>
                )}
              </div>
              <div className="scan-finding-card__title">{finding.title}</div>
              <div className="scan-finding-card__desc">{finding.description}</div>
              {finding.evidence && (
                <div className="scan-finding-card__evidence">
                  <span className="scan-finding-card__label">Evidence:</span>
                  <code>{finding.evidence}</code>
                </div>
              )}
              {finding.remediation && (
                <div className="scan-finding-card__remediation">
                  <span className="scan-finding-card__label">Remediation:</span>
                  <span>{finding.remediation}</span>
                </div>
              )}
            </div>
          ))}
        </div>
      ))}
    </div>
  )
}
