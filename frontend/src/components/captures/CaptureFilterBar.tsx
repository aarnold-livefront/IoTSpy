import { useCallback, useState } from 'react'
import DeviceFilter from '../devices/DeviceFilter'
import type { CaptureFilters, Device } from '../../types/api'
import '../../styles/capture-list.css'

interface Props {
  devices: Device[]
  filters: CaptureFilters
  onChange: (filters: CaptureFilters) => void
}

const METHODS = ['GET', 'POST', 'PUT', 'DELETE', 'PATCH', 'HEAD', 'OPTIONS']

export default function CaptureFilterBar({ devices, filters, onChange }: Props) {
  const [hostInput, setHostInput] = useState(filters.host ?? '')
  const [qInput, setQInput] = useState(filters.q ?? '')

  const update = useCallback(
    (patch: Partial<CaptureFilters>) => onChange({ ...filters, ...patch, page: 1 }),
    [filters, onChange],
  )

  function handleHostBlur() {
    update({ host: hostInput || undefined })
  }

  function handleQBlur() {
    update({ q: qInput || undefined })
  }

  function handleClear() {
    setHostInput('')
    setQInput('')
    onChange({ page: 1, pageSize: 50 })
  }

  const hasFilters = !!(filters.deviceId || filters.clientIp || filters.host || filters.method || filters.statusCode || filters.q)

  return (
    <div className="capture-filter-bar">
      <DeviceFilter
        devices={devices}
        value={filters.deviceId ?? ''}
        clientIp={filters.clientIp ?? ''}
        onDeviceChange={(v) => update({ deviceId: v || undefined })}
        onClientIpChange={(v) => update({ clientIp: v || undefined, deviceId: undefined })}
      />

      <select
        className="filter-select"
        value={filters.method ?? ''}
        onChange={(e) => update({ method: e.target.value || undefined })}
        aria-label="Filter by method"
      >
        <option value="">Method</option>
        {METHODS.map((m) => (
          <option key={m} value={m}>{m}</option>
        ))}
      </select>

      <input
        className="filter-input"
        type="number"
        placeholder="Status"
        min={100}
        max={599}
        value={filters.statusCode ?? ''}
        onChange={(e) =>
          update({ statusCode: e.target.value ? Number(e.target.value) : undefined })
        }
        aria-label="Filter by status code"
        style={{ width: 72 }}
      />

      <input
        className="filter-input"
        type="text"
        placeholder="Host"
        value={hostInput}
        onChange={(e) => setHostInput(e.target.value)}
        onBlur={handleHostBlur}
        onKeyDown={(e) => e.key === 'Enter' && handleHostBlur()}
        aria-label="Filter by host"
        style={{ width: 120 }}
      />

      <input
        className="filter-input filter-input--search"
        type="search"
        placeholder="Search…"
        value={qInput}
        onChange={(e) => setQInput(e.target.value)}
        onBlur={handleQBlur}
        onKeyDown={(e) => e.key === 'Enter' && handleQBlur()}
        aria-label="Search captures"
      />

      {hasFilters && (
        <button className="filter-btn-clear" onClick={handleClear} title="Clear filters">
          Clear
        </button>
      )}
    </div>
  )
}
