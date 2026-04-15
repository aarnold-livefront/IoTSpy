import { apiFetch, getToken } from './client'
import type {
  CaptureDeviceDto,
  CapturedPacket,
  FreezeFrameDto,
  ProtocolDistributionDto,
  CommunicationPatternDto,
  SuspiciousActivityDto,
  PcapImportResult,
} from '../types/api'

// ── Devices ──────────────────────────────────────────────────────────────────

export function listCaptureDevices(): Promise<CaptureDeviceDto[]> {
  return apiFetch<CaptureDeviceDto[]>('/api/packet-capture/devices')
}

export function getCaptureDevice(id: string): Promise<CaptureDeviceDto> {
  return apiFetch<CaptureDeviceDto>(`/api/packet-capture/devices/${id}`)
}

// ── Capture lifecycle ────────────────────────────────────────────────────────

export function startCapture(deviceId: string): Promise<{ started: boolean; deviceId: string }> {
  return apiFetch('/api/packet-capture/start', {
    method: 'POST',
    body: JSON.stringify({ deviceId }),
  })
}

export function stopCapture(): Promise<{ stopped: boolean }> {
  return apiFetch('/api/packet-capture/stop', { method: 'POST' })
}

export function getCaptureStatus(): Promise<{ isCapturing: boolean }> {
  return apiFetch('/api/packet-capture/status')
}

// ── Packets ──────────────────────────────────────────────────────────────────

export interface PacketFilterParams {
  protocol?: string
  sourceIp?: string
  destinationIp?: string
  sourcePort?: number
  destinationPort?: number
  macAddress?: string
  showOnlyErrors?: boolean
  showOnlyRetransmissions?: boolean
  fromTime?: string
  toTime?: string
  payloadSearch?: string
  limit?: number
}

export function getPackets(filter?: PacketFilterParams): Promise<CapturedPacket[]> {
  const params = new URLSearchParams()
  if (filter) {
    for (const [key, val] of Object.entries(filter)) {
      if (val !== undefined && val !== null && val !== '') {
        params.set(key, String(val))
      }
    }
  }
  const qs = params.toString()
  return apiFetch<CapturedPacket[]>(`/api/packet-capture/packets${qs ? `?${qs}` : ''}`)
}

export function getPacket(id: string): Promise<CapturedPacket> {
  return apiFetch<CapturedPacket>(`/api/packet-capture/packets/${id}`)
}

export function deletePacket(id: string): Promise<{ deleted: boolean; packetId: string }> {
  return apiFetch(`/api/packet-capture/packets/${id}/delete`, { method: 'POST' })
}

// ── Freeze frame ─────────────────────────────────────────────────────────────

export function createFreezeFrame(packetId: string): Promise<FreezeFrameDto> {
  return apiFetch<FreezeFrameDto>(`/api/packet-capture/packets/${packetId}/freeze`, {
    method: 'POST',
  })
}

export function getFreezeFrame(packetId: string): Promise<FreezeFrameDto> {
  return apiFetch<FreezeFrameDto>(`/api/packet-capture/packets/${packetId}/freeze`)
}

// ── Analysis ─────────────────────────────────────────────────────────────────

export function getProtocolDistribution(): Promise<ProtocolDistributionDto> {
  return apiFetch<ProtocolDistributionDto>('/api/packet-capture/analysis/protocols')
}

export function getCommunicationPatterns(topN = 10): Promise<CommunicationPatternDto[]> {
  return apiFetch<CommunicationPatternDto[]>(
    `/api/packet-capture/analysis/patterns?topN=${topN}`
  )
}

export function getSuspiciousActivity(): Promise<SuspiciousActivityDto[]> {
  return apiFetch<SuspiciousActivityDto[]>('/api/packet-capture/analysis/suspicious')
}

// ── Global freeze/unfreeze ───────────────────────────────────────────────────

export function freezeAnalysis(): Promise<{ frozen: boolean }> {
  return apiFetch('/api/packet-capture/freeze', { method: 'POST' })
}

export function unfreezeAnalysis(): Promise<{ unfrozen: boolean }> {
  return apiFetch('/api/packet-capture/unfreeze', { method: 'POST' })
}

export function getFreezeStatus(): Promise<{ isFrozen: boolean; filteredCount: number }> {
  return apiFetch('/api/packet-capture/freeze/status')
}

// ── PCAP import / export ─────────────────────────────────────────────────────

export async function importPcap(file: File): Promise<PcapImportResult> {
  const form = new FormData()
  form.append('file', file)
  const token = getToken()
  const res = await fetch('/api/packet-capture/import', {
    method: 'POST',
    headers: token ? { Authorization: `Bearer ${token}` } : {},
    body: form,
  })
  if (!res.ok) {
    const err = await res.json().catch(() => ({ error: res.statusText }))
    throw new Error(err?.error ?? `Import failed: ${res.status}`)
  }
  return res.json()
}

export function exportPcapUrl(filter?: {
  protocol?: string
  sourceIp?: string
  destIp?: string
  from?: string
  to?: string
}): string {
  const params = new URLSearchParams()
  if (filter?.protocol) params.set('protocol', filter.protocol)
  if (filter?.sourceIp) params.set('sourceIp', filter.sourceIp)
  if (filter?.destIp) params.set('destIp', filter.destIp)
  if (filter?.from) params.set('from', filter.from)
  if (filter?.to) params.set('to', filter.to)
  const qs = params.toString()
  return `/api/packet-capture/export/pcap${qs ? `?${qs}` : ''}`
}
