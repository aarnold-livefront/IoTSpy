import { useCallback, useState } from 'react'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import {
  getMqttStatus, startMqttProxy, stopMqttProxy,
  getCoapStatus, startCoapProxy, stopCoapProxy,
} from '../api/protocolProxy'
import type { MqttBrokerSettings, CoapProxySettings } from '../types/api'

const MQTT_KEY = ['protocol-proxy', 'mqtt']
const COAP_KEY = ['protocol-proxy', 'coap']

export function useProtocolProxy() {
  const queryClient = useQueryClient()
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const { data: mqttStatus, isLoading: mqttLoading } = useQuery({
    queryKey: MQTT_KEY,
    queryFn: getMqttStatus,
    refetchInterval: 5000,
  })

  const { data: coapStatus, isLoading: coapLoading } = useQuery({
    queryKey: COAP_KEY,
    queryFn: getCoapStatus,
    refetchInterval: 5000,
  })

  const startMqtt = useCallback(async (settings: MqttBrokerSettings) => {
    setBusy(true); setError(null)
    try {
      await startMqttProxy(settings)
      await queryClient.invalidateQueries({ queryKey: MQTT_KEY })
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to start MQTT proxy')
    } finally { setBusy(false) }
  }, [queryClient])

  const stopMqtt = useCallback(async () => {
    setBusy(true); setError(null)
    try {
      await stopMqttProxy()
      await queryClient.invalidateQueries({ queryKey: MQTT_KEY })
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to stop MQTT proxy')
    } finally { setBusy(false) }
  }, [queryClient])

  const startCoap = useCallback(async (settings: CoapProxySettings) => {
    setBusy(true); setError(null)
    try {
      await startCoapProxy(settings)
      await queryClient.invalidateQueries({ queryKey: COAP_KEY })
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to start CoAP proxy')
    } finally { setBusy(false) }
  }, [queryClient])

  const stopCoap = useCallback(async () => {
    setBusy(true); setError(null)
    try {
      await stopCoapProxy()
      await queryClient.invalidateQueries({ queryKey: COAP_KEY })
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to stop CoAP proxy')
    } finally { setBusy(false) }
  }, [queryClient])

  return {
    mqttStatus, mqttLoading,
    coapStatus, coapLoading,
    busy, error,
    startMqtt, stopMqtt,
    startCoap, stopCoap,
  }
}
