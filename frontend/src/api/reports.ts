import { getToken } from './client'

const BASE_URL = import.meta.env.VITE_API_URL ?? ''

function reportUrl(deviceId: string, format: 'html' | 'pdf'): string {
  return `${BASE_URL}/api/reports/devices/${deviceId}/${format}`
}

export async function downloadHtmlReport(deviceId: string): Promise<void> {
  const token = getToken()
  const res = await fetch(reportUrl(deviceId, 'html'), {
    headers: token ? { Authorization: `Bearer ${token}` } : {},
  })
  if (!res.ok) throw new Error(`Failed to download HTML report: ${res.status}`)
  const blob = await res.blob()
  const url = URL.createObjectURL(blob)
  const a = document.createElement('a')
  a.href = url
  a.download = `scan-report-${deviceId}.html`
  a.click()
  URL.revokeObjectURL(url)
}

export async function downloadPdfReport(deviceId: string): Promise<void> {
  const token = getToken()
  const res = await fetch(reportUrl(deviceId, 'pdf'), {
    headers: token ? { Authorization: `Bearer ${token}` } : {},
  })
  if (!res.ok) throw new Error(`Failed to download PDF report: ${res.status}`)
  const blob = await res.blob()
  const url = URL.createObjectURL(blob)
  const a = document.createElement('a')
  a.href = url
  a.download = `scan-report-${deviceId}.pdf`
  a.click()
  URL.revokeObjectURL(url)
}

export function getHtmlReportUrl(deviceId: string): string {
  return reportUrl(deviceId, 'html')
}

export function getPdfReportUrl(deviceId: string): string {
  return reportUrl(deviceId, 'pdf')
}
