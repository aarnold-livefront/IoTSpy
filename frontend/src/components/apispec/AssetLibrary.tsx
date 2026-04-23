import { useCallback, useEffect, useRef, useState } from 'react'
import {
  deleteAsset,
  getAssetContentUrl,
  listAssets,
  uploadAssets,
} from '../../api/apispec'
import type { AssetInfo } from '../../types/api'

interface Props {
  /** When provided, clicking an asset calls this instead of showing delete controls. */
  onPick?: (asset: AssetInfo) => void
  /** Compact mode hides the header and renders inline (used inside picker modals). */
  compact?: boolean
}

const ALLOWED_MIME_PREFIXES = ['image/', 'video/', 'audio/', 'text/']
const ALLOWED_EXACT_MIMES = [
  'application/json',
  'application/octet-stream',
  'application/x-ndjson',
  'application/pdf',
]
const ALLOWED_EXTENSIONS = [
  '.png', '.jpg', '.jpeg', '.gif', '.webp', '.svg',
  '.mp4', '.webm', '.mov', '.mkv',
  '.mp3', '.wav', '.ogg', '.m4a', '.flac',
  '.json', '.txt', '.html', '.xml', '.csv',
  '.sse', '.ndjson',
  '.pdf', '.zip',
]

function isAllowed(file: File): boolean {
  if (ALLOWED_MIME_PREFIXES.some((p) => file.type.startsWith(p))) return true
  if (ALLOWED_EXACT_MIMES.includes(file.type)) return true
  const lower = file.name.toLowerCase()
  return ALLOWED_EXTENSIONS.some((e) => lower.endsWith(e))
}

function assetKind(name: string): 'image' | 'video' | 'audio' | 'stream' | 'text' | 'binary' {
  const lower = name.toLowerCase()
  if (/\.(png|jpg|jpeg|gif|webp|svg|bmp|ico)$/.test(lower)) return 'image'
  if (/\.(mp4|webm|mov|mkv)$/.test(lower)) return 'video'
  if (/\.(mp3|wav|ogg|m4a|flac)$/.test(lower)) return 'audio'
  if (/\.(sse|ndjson)$/.test(lower)) return 'stream'
  if (/\.(json|txt|html|xml|csv|css|js)$/.test(lower)) return 'text'
  return 'binary'
}

function kindBadgeColor(kind: ReturnType<typeof assetKind>): string {
  switch (kind) {
    case 'image': return '#4ade80'
    case 'video': return '#60a5fa'
    case 'audio': return '#c084fc'
    case 'stream': return '#f59e0b'
    case 'text': return '#94a3b8'
    case 'binary': return '#6b7280'
  }
}

function formatSize(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`
  if (bytes < 1024 * 1024 * 1024) return `${(bytes / 1024 / 1024).toFixed(1)} MB`
  return `${(bytes / 1024 / 1024 / 1024).toFixed(1)} GB`
}

export default function AssetLibrary({ onPick, compact }: Props) {
  const [assets, setAssets] = useState<AssetInfo[]>([])
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [dragOver, setDragOver] = useState(false)
  const fileInputRef = useRef<HTMLInputElement>(null)

  const refresh = useCallback(async () => {
    setLoading(true)
    try {
      setAssets(await listAssets())
      setError(null)
    } catch (e) {
      setError((e as Error).message)
    } finally {
      setLoading(false)
    }
  }, [])

  useEffect(() => { void refresh() }, [refresh])

  const handleFiles = async (files: File[]) => {
    const valid = files.filter(isAllowed)
    const rejected = files.length - valid.length
    if (valid.length === 0) {
      setError(rejected > 0 ? `${rejected} file(s) rejected — unsupported type.` : null)
      return
    }
    try {
      await uploadAssets(valid)
      await refresh()
      if (rejected > 0) setError(`Uploaded ${valid.length}, rejected ${rejected} (unsupported type).`)
      else setError(null)
    } catch (e) {
      setError(`Upload failed: ${(e as Error).message}`)
    }
  }

  const onDrop = (e: React.DragEvent) => {
    e.preventDefault()
    setDragOver(false)
    void handleFiles(Array.from(e.dataTransfer.files))
  }

  const onPicked = (e: React.ChangeEvent<HTMLInputElement>) => {
    if (!e.target.files) return
    void handleFiles(Array.from(e.target.files))
    if (fileInputRef.current) fileInputRef.current.value = ''
  }

  const handleDelete = async (asset: AssetInfo) => {
    if (!confirm(`Delete ${asset.fileName}?`)) return
    try {
      await deleteAsset(asset.fileName)
      await refresh()
    } catch (e) {
      setError(`Delete failed: ${(e as Error).message}`)
    }
  }

  return (
    <div style={{ marginTop: compact ? 0 : 16 }}>
      {!compact && (
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
          <h3 style={{ margin: 0 }}>Asset Library ({assets.length})</h3>
          <button
            className="btn btn--primary"
            style={{ fontSize: 12 }}
            onClick={() => fileInputRef.current?.click()}
          >
            Upload
          </button>
        </div>
      )}

      <input
        ref={fileInputRef}
        type="file"
        multiple
        accept={ALLOWED_EXTENSIONS.join(',')}
        onChange={onPicked}
        style={{ display: 'none' }}
      />

      <div
        onDragOver={(e) => { e.preventDefault(); setDragOver(true) }}
        onDragLeave={() => setDragOver(false)}
        onDrop={onDrop}
        onClick={() => !onPick && fileInputRef.current?.click()}
        style={{
          marginTop: 8,
          padding: 16,
          border: `2px dashed ${dragOver ? '#60a5fa' : '#4a4a6a'}`,
          borderRadius: 6,
          background: dragOver ? 'rgba(96, 165, 250, 0.08)' : '#1a1a2e',
          textAlign: 'center',
          color: '#aaa',
          fontSize: 13,
          cursor: onPick ? 'default' : 'pointer',
        }}
      >
        {dragOver ? 'Drop files to upload' : 'Drop files here or click to upload (images, video, audio, .sse, .ndjson, text)'}
      </div>

      {error && (
        <div style={{ marginTop: 8, padding: 8, background: '#3a1a1a', color: '#fca5a5', borderRadius: 4, fontSize: 12 }}>
          {error}
        </div>
      )}

      <div
        style={{
          marginTop: 8,
          display: 'grid',
          gridTemplateColumns: 'repeat(auto-fill, minmax(160px, 1fr))',
          gap: 8,
        }}
      >
        {assets.map((a) => {
          const kind = assetKind(a.fileName)
          const url = getAssetContentUrl(a.fileName)
          return (
            <div
              key={a.fileName}
              onClick={() => onPick?.(a)}
              style={{
                padding: 8,
                background: '#1a1a2e',
                border: '1px solid #4a4a6a',
                borderRadius: 6,
                cursor: onPick ? 'pointer' : 'default',
              }}
            >
              <div style={{
                height: 80,
                background: '#0f0f1a',
                borderRadius: 4,
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'center',
                overflow: 'hidden',
                marginBottom: 6,
              }}>
                {kind === 'image' && (
                  <img src={url} alt={a.fileName} style={{ maxWidth: '100%', maxHeight: '100%' }} />
                )}
                {kind === 'video' && (
                  <video src={url} preload="metadata" muted style={{ maxWidth: '100%', maxHeight: '100%' }} />
                )}
                {kind === 'audio' && (
                  <span style={{ fontSize: 24 }}>♪</span>
                )}
                {kind === 'stream' && (
                  <span style={{ fontSize: 24 }}>≋</span>
                )}
                {(kind === 'text' || kind === 'binary') && (
                  <span style={{ fontSize: 22, color: '#666' }}>{kind === 'text' ? 'T' : '□'}</span>
                )}
              </div>
              <div style={{ display: 'flex', alignItems: 'center', gap: 4, marginBottom: 4 }}>
                <span
                  style={{
                    fontSize: 9,
                    padding: '1px 5px',
                    background: kindBadgeColor(kind),
                    color: '#000',
                    borderRadius: 3,
                    textTransform: 'uppercase',
                    fontWeight: 600,
                  }}
                >
                  {kind}
                </span>
                <span style={{ fontSize: 10, color: '#777' }}>{formatSize(a.size)}</span>
              </div>
              <div
                title={a.fileName}
                style={{
                  fontSize: 11,
                  whiteSpace: 'nowrap',
                  overflow: 'hidden',
                  textOverflow: 'ellipsis',
                  color: '#ccc',
                }}
              >
                {a.fileName}
              </div>
              {!onPick && (
                <button
                  className="btn btn--danger"
                  style={{ fontSize: 10, padding: '2px 6px', marginTop: 4 }}
                  onClick={(e) => { e.stopPropagation(); void handleDelete(a) }}
                >
                  Delete
                </button>
              )}
            </div>
          )
        })}
        {!loading && assets.length === 0 && (
          <div style={{ gridColumn: '1 / -1', textAlign: 'center', color: '#666', padding: 16, fontSize: 12 }}>
            No assets yet. Drop files above to upload.
          </div>
        )}
      </div>
    </div>
  )
}
