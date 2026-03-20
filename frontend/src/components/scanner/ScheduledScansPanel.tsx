import { useState, useEffect } from 'react'
import {
  listScheduledScans,
  createScheduledScan,
  updateScheduledScan,
  deleteScheduledScan,
  type ScheduledScan,
} from '../../api/scheduledScans'

interface Props {
  deviceId?: string
}

export function ScheduledScansPanel({ deviceId }: Props) {
  const [scans, setScans] = useState<ScheduledScan[]>([])
  const [loading, setLoading] = useState(false)
  const [cronExpr, setCronExpr] = useState('0 * * * *')
  const [error, setError] = useState<string | null>(null)

  const load = async () => {
    setLoading(true)
    try {
      const all = await listScheduledScans()
      setScans(deviceId ? all.filter((s) => s.deviceId === deviceId) : all)
    } catch (e) {
      setError(String(e))
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => {
    load()
  }, [deviceId])

  const handleCreate = async () => {
    if (!deviceId) return
    try {
      await createScheduledScan({ deviceId, cronExpression: cronExpr })
      await load()
    } catch (e) {
      setError(String(e))
    }
  }

  const handleToggle = async (scan: ScheduledScan) => {
    try {
      await updateScheduledScan(scan.id, { isEnabled: !scan.isEnabled })
      await load()
    } catch (e) {
      setError(String(e))
    }
  }

  const handleDelete = async (id: string) => {
    try {
      await deleteScheduledScan(id)
      await load()
    } catch (e) {
      setError(String(e))
    }
  }

  return (
    <div className="p-4">
      <h3 className="text-lg font-semibold mb-4">Scheduled Scans</h3>

      {error && (
        <div className="bg-red-100 text-red-800 p-2 rounded mb-3 text-sm">{error}</div>
      )}

      {deviceId && (
        <div className="flex gap-2 mb-4">
          <input
            className="border rounded px-2 py-1 text-sm flex-1"
            value={cronExpr}
            onChange={(e) => setCronExpr(e.target.value)}
            placeholder="Cron expression (e.g. 0 * * * *)"
          />
          <button
            className="bg-blue-600 text-white px-3 py-1 rounded text-sm"
            onClick={handleCreate}
          >
            Add Schedule
          </button>
        </div>
      )}

      {loading ? (
        <p className="text-sm text-gray-500">Loading...</p>
      ) : scans.length === 0 ? (
        <p className="text-sm text-gray-500">No scheduled scans configured.</p>
      ) : (
        <table className="w-full text-sm border-collapse">
          <thead>
            <tr className="bg-gray-100">
              <th className="border px-2 py-1 text-left">Cron</th>
              <th className="border px-2 py-1 text-left">Enabled</th>
              <th className="border px-2 py-1 text-left">Last Run</th>
              <th className="border px-2 py-1 text-left">Next Run</th>
              <th className="border px-2 py-1 text-left">Actions</th>
            </tr>
          </thead>
          <tbody>
            {scans.map((scan) => (
              <tr key={scan.id}>
                <td className="border px-2 py-1 font-mono">{scan.cronExpression}</td>
                <td className="border px-2 py-1">
                  <input
                    type="checkbox"
                    checked={scan.isEnabled}
                    onChange={() => handleToggle(scan)}
                  />
                </td>
                <td className="border px-2 py-1">
                  {scan.lastRunAt ? new Date(scan.lastRunAt).toLocaleString() : '—'}
                </td>
                <td className="border px-2 py-1">
                  {scan.nextRunAt ? new Date(scan.nextRunAt).toLocaleString() : '—'}
                </td>
                <td className="border px-2 py-1">
                  <button
                    className="text-red-600 hover:underline text-xs"
                    onClick={() => handleDelete(scan.id)}
                  >
                    Delete
                  </button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </div>
  )
}
