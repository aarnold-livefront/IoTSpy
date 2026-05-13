import { useCallback, useEffect, useRef, useState } from 'react'
import {
  deleteAsset,
  getAssetContentUrl,
  listAssets,
  uploadAssets,
} from '../../api/apispec'
import ConfirmDialog from '../common/ConfirmDialog'
import type { AssetInfo } from '../../types/api'
import '../../styles/asset-library.css'

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
  const [confirmDeleteName, setConfirmDeleteName] = useState<string | null>(null)

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

  const handleDeleteConfirmed = async (fileName: string) => {
    setConfirmDeleteName(null)
    try {
      await deleteAsset(fileName)
      await refresh()
    } catch (e) {
      setError(`Delete failed: ${(e as Error).message}`)
    }
  }

  return (
    <div className={`asset-library${compact ? ' asset-library--compact' : ''}`}>
      {confirmDeleteName && (
        <ConfirmDialog
          title="Delete asset"
          message={`Delete "${confirmDeleteName}"? This cannot be undone.`}
          confirmLabel="Delete"
          danger
          onConfirm={() => void handleDeleteConfirmed(confirmDeleteName)}
          onCancel={() => setConfirmDeleteName(null)}
        />
      )}

      <div className="asset-library__header">
        {!compact
          ? <h3 className="asset-library__title">Asset Library ({assets.length})</h3>
          : <span className="asset-library__subtitle">Assets ({assets.length})</span>
        }
        <button
          className="btn btn--primary asset-library__upload-btn"
          onClick={() => fileInputRef.current?.click()}
        >
          Upload
        </button>
      </div>

      <input
        ref={fileInputRef}
        type="file"
        multiple
        accept={ALLOWED_EXTENSIONS.join(',')}
        onChange={onPicked}
        className="asset-library__file-input"
      />

      <div
        className={`asset-library__drop-zone${dragOver ? ' asset-library__drop-zone--active' : ''}`}
        onDragOver={(e) => { e.preventDefault(); setDragOver(true) }}
        onDragLeave={() => setDragOver(false)}
        onDrop={onDrop}
        onClick={() => fileInputRef.current?.click()}
      >
        {dragOver ? 'Drop files to upload' : 'Drop files here or click to upload (images, video, audio, .sse, .ndjson, text)'}
      </div>

      {error && (
        <div className="asset-library__error">{error}</div>
      )}

      <div className="asset-library__grid">
        {assets.map((a) => {
          const kind = assetKind(a.fileName)
          const url = getAssetContentUrl(a.fileName)
          return (
            <div
              key={a.fileName}
              className={`asset-library__card${onPick ? ' asset-library__card--pickable' : ''}`}
              onClick={() => onPick?.(a)}
              tabIndex={onPick ? 0 : undefined}
              onKeyDown={onPick ? (e) => {
                if (e.key === 'Enter' || e.key === ' ') { e.preventDefault(); onPick(a) }
              } : undefined}
            >
              <div className="asset-library__preview">
                {kind === 'image' && (
                  <img src={url} alt={a.fileName} className="asset-library__preview-media" />
                )}
                {kind === 'video' && (
                  <video src={url} preload="metadata" muted className="asset-library__preview-media" />
                )}
                {kind === 'audio' && <span className="asset-library__preview-icon">♪</span>}
                {kind === 'stream' && <span className="asset-library__preview-icon">≋</span>}
                {(kind === 'text' || kind === 'binary') && (
                  <span className="asset-library__preview-icon--faint">{kind === 'text' ? 'T' : '□'}</span>
                )}
              </div>

              <div className="asset-library__badge-row">
                <span className="asset-library__kind-badge" data-kind={kind}>{kind}</span>
                <span className="asset-library__size">{formatSize(a.size)}</span>
              </div>

              <div className="asset-library__filename" title={a.fileName}>
                {a.fileName}
              </div>

              <button
                className="btn btn--danger asset-library__delete-btn"
                onClick={(e) => { e.stopPropagation(); setConfirmDeleteName(a.fileName) }}
              >
                Delete
              </button>
            </div>
          )
        })}
        {!loading && assets.length === 0 && (
          <div className="asset-library__empty">
            No assets yet. Drop files above to upload.
          </div>
        )}
      </div>
    </div>
  )
}
