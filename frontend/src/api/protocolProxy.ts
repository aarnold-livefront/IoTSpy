import { apiFetch } from './client'
import type { MqttBrokerSettings, CoapProxySettings, MqttProxyStatus, CoapProxyStatus } from '../types/api'

export function getMqttStatus(): Promise<MqttProxyStatus> {
  return apiFetch<MqttProxyStatus>('/api/protocol-proxy/mqtt/status')
}

export function startMqttProxy(settings: MqttBrokerSettings): Promise<{ status: string; port: number; upstream: string }> {
  return apiFetch('/api/protocol-proxy/mqtt/start', { method: 'POST', body: JSON.stringify(settings) })
}

export function stopMqttProxy(): Promise<{ status: string }> {
  return apiFetch('/api/protocol-proxy/mqtt/stop', { method: 'POST' })
}

export function getCoapStatus(): Promise<CoapProxyStatus> {
  return apiFetch<CoapProxyStatus>('/api/protocol-proxy/coap/status')
}

export function startCoapProxy(settings: CoapProxySettings): Promise<{ status: string; port: number; upstream: string }> {
  return apiFetch('/api/protocol-proxy/coap/start', { method: 'POST', body: JSON.stringify(settings) })
}

export function stopCoapProxy(): Promise<{ status: string }> {
  return apiFetch('/api/protocol-proxy/coap/stop', { method: 'POST' })
}
