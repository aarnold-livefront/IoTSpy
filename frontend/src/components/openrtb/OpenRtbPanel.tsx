import { useState } from 'react'
import { useOpenRtb } from '../../hooks/useOpenRtb'
import OpenRtbTrafficList from './OpenRtbTrafficList'
import OpenRtbInspector from './OpenRtbInspector'
import PiiPolicyEditor from './PiiPolicyEditor'
import PiiAuditLog from './PiiAuditLog'

type OpenRtbTab = 'traffic' | 'policies' | 'audit'

export default function OpenRtbPanel() {
  const [activeTab, setActiveTab] = useState<OpenRtbTab>('traffic')
  const [selectedEventId, setSelectedEventId] = useState<string | null>(null)

  const rtb = useOpenRtb()

  const tabs: { key: OpenRtbTab; label: string }[] = [
    { key: 'traffic', label: 'Traffic' },
    { key: 'policies', label: 'PII Policies' },
    { key: 'audit', label: 'Audit Log' },
  ]

  return (
    <div className="manipulation-panel">
      <div className="manip-tabs">
        {tabs.map((tab) => (
          <button
            key={tab.key}
            className={`manip-tab ${activeTab === tab.key ? 'manip-tab--active' : ''}`}
            onClick={() => setActiveTab(tab.key)}
          >
            {tab.label}
          </button>
        ))}
      </div>

      <div className="manip-content">
        {activeTab === 'traffic' && (
          <div style={{ display: 'flex', gap: '12px', height: '100%' }}>
            <div style={{ flex: '0 0 50%', overflow: 'auto' }}>
              <OpenRtbTrafficList
                events={rtb.events}
                total={rtb.eventsTotal}
                loading={rtb.eventsLoading}
                error={rtb.eventsError}
                selectedId={selectedEventId}
                onSelect={setSelectedEventId}
                onRefresh={rtb.refreshEvents}
              />
            </div>
            <div style={{ flex: '1', overflow: 'auto' }}>
              <OpenRtbInspector eventId={selectedEventId} />
            </div>
          </div>
        )}
        {activeTab === 'policies' && (
          <PiiPolicyEditor
            policies={rtb.policies}
            loading={rtb.policiesLoading}
            error={rtb.policiesError}
            onAdd={rtb.addPolicy}
            onEdit={rtb.editPolicy}
            onDelete={rtb.removePolicy}
            onReset={rtb.resetPolicies}
          />
        )}
        {activeTab === 'audit' && (
          <PiiAuditLog
            logs={rtb.auditLogs}
            total={rtb.auditTotal}
            stats={rtb.auditStats}
            loading={rtb.auditLoading}
            error={rtb.auditError}
            onRefresh={rtb.refreshAuditLog}
          />
        )}
      </div>
    </div>
  )
}
