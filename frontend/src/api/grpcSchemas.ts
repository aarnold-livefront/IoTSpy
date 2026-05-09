import { apiFetch } from './client'

export interface ProtoSchema {
  id: string
  name: string
  rawProto: string
  fieldMapJson: string
  createdAt: string
}

export interface ProtoSchemaListResponse {
  items: ProtoSchema[]
  total: number
}

export function listProtoSchemas(): Promise<ProtoSchemaListResponse> {
  return apiFetch<ProtoSchemaListResponse>('/api/grpc/schemas')
}

export function getProtoSchema(id: string): Promise<ProtoSchema> {
  return apiFetch<ProtoSchema>(`/api/grpc/schemas/${id}`)
}

export function uploadProtoSchemaJson(name: string, protoText: string): Promise<ProtoSchema> {
  return apiFetch<ProtoSchema>('/api/grpc/schemas/json', {
    method: 'POST',
    body: JSON.stringify({ name, protoText }),
  })
}

export function deleteProtoSchema(id: string): Promise<void> {
  return apiFetch<void>(`/api/grpc/schemas/${id}`, { method: 'DELETE' })
}
