import { useState } from 'react'
import { useScanner } from '../../hooks/useScanner'
import { useDevices } from '../../hooks/useDevices'
import ScanJobList from './ScanJobList'
import ScanFindingsView from './ScanFindingsView'
import type { StartScanRequest } from '../../types/api'
import '../../styles/scanner.css'

export default function ScannerPanel() {
  const { jobs, selectedJob, loading, error, scan, selectJob, cancel, remove } = useScanner()
  const { devices } = useDevices()

  // Scan form state
  const [formDeviceId, setFormDeviceId] = useState('')
  const [formPortRange, setFormPortRange] = useState('1-1024')
  const [formConcurrency, setFormConcurrency] = useState(100)
  const [formTimeout, setFormTimeout] = useState(2000)
  const [formFingerprint, setFormFingerprint] = useState(true)
  const [formCredTest, setFormCredTest] = useState(true)
  const [formCveLookup, setFormCveLookup] = useState(true)
  const [formConfigAudit, setFormConfigAudit] = useState(true)
  const [scanning, setScanning] = useState(false)

  const handleScan = async () => {
    if (!formDeviceId) return
    setScanning(true)
    const request: StartScanRequest = {
      deviceId: formDeviceId,
      portRange: formPortRange || undefined,
      maxConcurrency: formConcurrency,
      timeoutMs: formTimeout,
      enableFingerprinting: formFingerprint,
      enableCredentialTest: formCredTest,
      enableCveLookup: formCveLookup,
      enableConfigAudit: formConfigAudit,
    }
    await scan(request)
    setScanning(false)
  }

  return (
    <div className="scanner-panel">
      {/* Scan trigger form */}
      <div className="scan-form">
        <div className="scan-form__title">Security Scanner</div>
        <div className="scan-form__row">
          <label className="scan-form__label">
            Device
            <select
              className="scan-form__select"
              value={formDeviceId}
              onChange={(e) => setFormDeviceId(e.target.value)}
            >
              <option value="">Select device...</option>
              {devices.map((d) => (
                <option key={d.id} value={d.id}>
                  {d.label || d.hostname || d.ipAddress} ({d.ipAddress})
                </option>
              ))}
            </select>
          </label>
          <label className="scan-form__label">
            Port Range
            <input
              className="scan-form__input"
              type="text"
              value={formPortRange}
              onChange={(e) => setFormPortRange(e.target.value)}
              placeholder="1-1024"
            />
          </label>
          <label className="scan-form__label">
            Concurrency
            <input
              className="scan-form__input"
              type="number"
              value={formConcurrency}
              onChange={(e) => setFormConcurrency(Number(e.target.value))}
              min={1}
              max={1000}
            />
          </label>
          <label className="scan-form__label">
            Timeout (ms)
            <input
              className="scan-form__input"
              type="number"
              value={formTimeout}
              onChange={(e) => setFormTimeout(Number(e.target.value))}
              min={100}
            />
          </label>
        </div>
        <div className="scan-form__row scan-form__row--checks">
          <label className="scan-form__checkbox">
            <input
              type="checkbox"
              checked={formFingerprint}
              onChange={(e) => setFormFingerprint(e.target.checked)}
            />
            Fingerprinting
          </label>
          <label className="scan-form__checkbox">
            <input
              type="checkbox"
              checked={formCredTest}
              onChange={(e) => setFormCredTest(e.target.checked)}
            />
            Credential Test
          </label>
          <label className="scan-form__checkbox">
            <input
              type="checkbox"
              checked={formCveLookup}
              onChange={(e) => setFormCveLookup(e.target.checked)}
            />
            CVE Lookup
          </label>
          <label className="scan-form__checkbox">
            <input
              type="checkbox"
              checked={formConfigAudit}
              onChange={(e) => setFormConfigAudit(e.target.checked)}
            />
            Config Audit
          </label>
          <button
            className="scan-btn scan-btn--primary"
            onClick={handleScan}
            disabled={!formDeviceId || scanning}
          >
            {scanning ? 'Starting...' : 'Start Scan'}
          </button>
        </div>
      </div>

      {error && <div className="scan-error">{error}</div>}

      {/* Main content: job list + findings detail */}
      <div className="scanner-panel__body">
        <div className="scanner-panel__left">
          <div className="scanner-panel__section-title">
            Scan Jobs {loading && <span className="scan-spinner" />}
          </div>
          <ScanJobList
            jobs={jobs}
            selectedId={selectedJob?.id ?? null}
            onSelect={selectJob}
            onCancel={cancel}
            onDelete={remove}
          />
        </div>
        <div className="scanner-panel__right">
          {selectedJob ? (
            <>
              <div className="scanner-panel__section-title">
                Findings for scan {selectedJob.id.slice(0, 8)}...
                {selectedJob.device && (
                  <span className="scan-finding-device">
                    {' '}({selectedJob.device.label || selectedJob.device.ipAddress})
                  </span>
                )}
              </div>
              {selectedJob.errorMessage && (
                <div className="scan-error">{selectedJob.errorMessage}</div>
              )}
              <div className="scan-summary">
                <div className="scan-summary__item">
                  <span className="scan-summary__value">{selectedJob.portsScanned}</span>
                  <span className="scan-summary__label">Ports Scanned</span>
                </div>
                <div className="scan-summary__item">
                  <span className="scan-summary__value">{selectedJob.openPortsFound}</span>
                  <span className="scan-summary__label">Open Ports</span>
                </div>
                <div className="scan-summary__item">
                  <span className="scan-summary__value">{selectedJob.findingsCount}</span>
                  <span className="scan-summary__label">Findings</span>
                </div>
              </div>
              <ScanFindingsView findings={selectedJob.findings ?? []} />
            </>
          ) : (
            <div className="scanner-panel__placeholder">
              Select a scan job to view its findings
            </div>
          )}
        </div>
      </div>
    </div>
  )
}
