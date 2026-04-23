import { useState } from 'react'
import { useManipulation } from '../../hooks/useManipulation'
import { useCaptures } from '../../hooks/useCaptures'
import RulesEditor from './RulesEditor'
import BreakpointsEditor from './BreakpointsEditor'
import ReplayPanel from './ReplayPanel'
import FuzzerPanel from './FuzzerPanel'
import ApiSpecPanel from '../apispec/ApiSpecPanel'
import ContentRulesPanel from '../contentrules/ContentRulesPanel'
import AssetLibrary from '../apispec/AssetLibrary'
import '../../styles/manipulation.css'

type ManipTab = 'trafficrules' | 'breakpoints' | 'replay' | 'fuzzer' | 'contentrules' | 'assets' | 'apispec'

export default function ManipulationPanel() {
  const [activeTab, setActiveTab] = useState<ManipTab>('trafficrules')

  const manip = useManipulation()
  const { captures } = useCaptures({ page: 1, pageSize: 100 })

  const tabs: { key: ManipTab; label: string }[] = [
    { key: 'trafficrules', label: 'Traffic Rules' },
    { key: 'breakpoints', label: 'Breakpoints' },
    { key: 'replay', label: 'Replay' },
    { key: 'fuzzer', label: 'Fuzzer' },
    { key: 'contentrules', label: 'Content Rules' },
    { key: 'assets', label: 'Assets' },
    { key: 'apispec', label: 'API Spec' },
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
        {activeTab === 'trafficrules' && (
          <RulesEditor
            rules={manip.rules}
            loading={manip.rulesLoading}
            error={manip.rulesError}
            onAdd={manip.addRule}
            onEdit={manip.editRule}
            onDelete={manip.removeRule}
          />
        )}
        {activeTab === 'breakpoints' && (
          <BreakpointsEditor
            breakpoints={manip.breakpoints}
            loading={manip.breakpointsLoading}
            error={manip.breakpointsError}
            onAdd={manip.addBreakpoint}
            onEdit={manip.editBreakpoint}
            onDelete={manip.removeBreakpoint}
          />
        )}
        {activeTab === 'replay' && (
          <ReplayPanel
            replays={manip.replays}
            loading={manip.replaysLoading}
            error={manip.replaysError}
            captures={captures}
            onReplay={manip.replay}
            onDelete={manip.removeReplay}
          />
        )}
        {activeTab === 'fuzzer' && (
          <FuzzerPanel
            jobs={manip.fuzzerJobs}
            selectedResults={manip.selectedFuzzerResults}
            loading={manip.fuzzerLoading}
            error={manip.fuzzerError}
            captures={captures}
            onStart={manip.fuzz}
            onViewResults={manip.viewFuzzerResults}
            onCancel={manip.cancelFuzzer}
            onDelete={manip.removeFuzzer}
          />
        )}
        {activeTab === 'contentrules' && <ContentRulesPanel />}
        {activeTab === 'assets' && <AssetLibrary />}
        {activeTab === 'apispec' && <ApiSpecPanel />}
      </div>
    </div>
  )
}
