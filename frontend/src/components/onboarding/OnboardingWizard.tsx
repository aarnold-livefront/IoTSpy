import { useState } from 'react'
import '../../styles/onboarding.css'

interface Props {
  onComplete: () => void
}

type Step = 'welcome' | 'proxy-mode' | 'tls-setup' | 'device' | 'done'

const STEPS: Step[] = ['welcome', 'proxy-mode', 'tls-setup', 'device', 'done']

export default function OnboardingWizard({ onComplete }: Props) {
  const [step, setStep] = useState<Step>('welcome')
  const stepIndex = STEPS.indexOf(step)

  function next() {
    const nextIndex = stepIndex + 1
    if (nextIndex < STEPS.length) {
      setStep(STEPS[nextIndex])
    }
  }

  function prev() {
    const prevIndex = stepIndex - 1
    if (prevIndex >= 0) {
      setStep(STEPS[prevIndex])
    }
  }

  function finish() {
    localStorage.setItem('iotspy-onboarding-complete', 'true')
    onComplete()
  }

  return (
    <div className="onboarding-overlay">
      <div className="onboarding-card">
        <div className="onboarding-progress">
          {STEPS.map((s, i) => (
            <div
              key={s}
              className={`onboarding-progress__dot ${i <= stepIndex ? 'onboarding-progress__dot--active' : ''}`}
            />
          ))}
        </div>

        {step === 'welcome' && (
          <div className="onboarding-step">
            <h2>Welcome to IoTSpy</h2>
            <p>
              IoTSpy is an IoT network security research platform. This wizard will help
              you configure your first proxy session to start capturing and analyzing
              IoT device traffic.
            </p>
          </div>
        )}

        {step === 'proxy-mode' && (
          <div className="onboarding-step">
            <h2>Choose Proxy Mode</h2>
            <p>IoTSpy supports three interception modes:</p>
            <ul className="onboarding-list">
              <li>
                <strong>Explicit Proxy</strong> &mdash; Configure your IoT device to use the proxy
                address. Best for devices with proxy settings.
              </li>
              <li>
                <strong>Gateway Redirect</strong> &mdash; Use iptables to redirect traffic
                transparently. Requires running on the network gateway.
              </li>
              <li>
                <strong>ARP Spoof</strong> &mdash; Intercept traffic via ARP spoofing.
                Works on any machine on the same LAN segment.
              </li>
            </ul>
            <p className="onboarding-hint">
              Start with <strong>Explicit Proxy</strong> if you are unsure.
              You can change this later in Settings.
            </p>
          </div>
        )}

        {step === 'tls-setup' && (
          <div className="onboarding-step">
            <h2>TLS Interception</h2>
            <p>To inspect HTTPS traffic, IoTSpy generates a root CA certificate.</p>
            <ul className="onboarding-list">
              <li>
                <strong>Full MITM</strong> &mdash; Install the root CA on your device. Download it
                from the header bar (CA button). This gives full request/response visibility.
              </li>
              <li>
                <strong>TLS Passthrough</strong> &mdash; No CA installation needed. Captures TLS
                metadata (SNI, JA3/JA3S fingerprints, server certificates) without decryption.
              </li>
              <li>
                <strong>SSL Stripping</strong> &mdash; For devices that redirect HTTP to HTTPS.
                Serves content over plain HTTP while fetching via HTTPS upstream.
              </li>
            </ul>
            <p className="onboarding-hint">
              Toggle <em>Capture TLS</em> in Settings. SSL stripping can be enabled separately.
            </p>
          </div>
        )}

        {step === 'device' && (
          <div className="onboarding-step">
            <h2>Connect a Device</h2>
            <p>
              Point your IoT device&apos;s HTTP proxy to <code>{'<this-machine-ip>:8888'}</code>.
              IoTSpy will automatically detect the device and begin capturing traffic.
            </p>
            <p>
              Once traffic appears, use the <strong>Scanner</strong> panel to run automated
              security checks, or the <strong>Manipulation</strong> panel to create interception
              rules and replay requests.
            </p>
          </div>
        )}

        {step === 'done' && (
          <div className="onboarding-step">
            <h2>You&apos;re All Set!</h2>
            <p>
              Start the proxy using the <strong>Start</strong> button in the header,
              then connect your IoT device. Captured traffic will appear in real time.
            </p>
            <p className="onboarding-hint">
              You can re-open this wizard from Settings at any time.
            </p>
          </div>
        )}

        <div className="onboarding-actions">
          {stepIndex > 0 && step !== 'done' && (
            <button className="onboarding-btn onboarding-btn--secondary" onClick={prev}>
              Back
            </button>
          )}
          <div className="onboarding-actions__spacer" />
          {step === 'done' ? (
            <button className="onboarding-btn onboarding-btn--primary" onClick={finish}>
              Get Started
            </button>
          ) : (
            <button className="onboarding-btn onboarding-btn--primary" onClick={next}>
              Next
            </button>
          )}
          {step !== 'done' && (
            <button className="onboarding-btn onboarding-btn--ghost" onClick={finish}>
              Skip
            </button>
          )}
        </div>
      </div>
    </div>
  )
}
