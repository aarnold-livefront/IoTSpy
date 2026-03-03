import type { Device } from '../../types/api'

interface Props {
  devices: Device[]
  value: string
  onChange: (deviceId: string) => void
}

export default function DeviceFilter({ devices, value, onChange }: Props) {
  return (
    <select
      className="filter-select"
      value={value}
      onChange={(e) => onChange(e.target.value)}
      aria-label="Filter by device"
    >
      <option value="">All devices</option>
      {devices.map((d) => (
        <option key={d.id} value={d.id}>
          {d.label || d.ipAddress || d.hostname || d.id.slice(0, 8)}
        </option>
      ))}
    </select>
  )
}
