import { useCallback, useEffect, useState } from 'react'
import {
  listProtoSchemas,
  uploadProtoSchemaJson,
  deleteProtoSchema,
  type ProtoSchema,
} from '../api/grpcSchemas'

export function useGrpcSchemas() {
  const [schemas, setSchemas] = useState<ProtoSchema[]>([])
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const refresh = useCallback(async () => {
    setLoading(true)
    setError(null)
    try {
      const { items } = await listProtoSchemas()
      setSchemas(items)
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Failed to load schemas')
    } finally {
      setLoading(false)
    }
  }, [])

  useEffect(() => { void refresh() }, [refresh])

  const addSchema = useCallback(async (name: string, protoText: string) => {
    const schema = await uploadProtoSchemaJson(name, protoText)
    setSchemas(prev => [schema, ...prev])
    return schema
  }, [])

  const removeSchema = useCallback(async (id: string) => {
    await deleteProtoSchema(id)
    setSchemas(prev => prev.filter(s => s.id !== id))
  }, [])

  return { schemas, loading, error, refresh, addSchema, removeSchema }
}
