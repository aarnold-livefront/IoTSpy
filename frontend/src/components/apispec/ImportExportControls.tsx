import { useRef } from 'react'
import type { ApiSpecDocument, ImportSpecRequest } from '../../types/api'

interface Props {
  selectedSpec: ApiSpecDocument | null
  onImport: (req: ImportSpecRequest) => Promise<ApiSpecDocument | null>
  onExport: (id: string) => Promise<string | null>
}

export default function ImportExportControls({ selectedSpec, onImport, onExport }: Props) {
  const fileInputRef = useRef<HTMLInputElement>(null)

  const handleImportClick = () => {
    fileInputRef.current?.click()
  }

  const handleFileChange = async (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0]
    if (!file) return

    try {
      const text = await file.text()
      await onImport({
        openApiJson: text,
        name: file.name.replace(/\.json$/i, ''),
      })
    } catch {
      // error handled by hook
    }

    // Reset file input
    if (fileInputRef.current) fileInputRef.current.value = ''
  }

  const handleExport = async () => {
    if (!selectedSpec) return
    const json = await onExport(selectedSpec.id)
    if (!json) return

    // Download as file
    const blob = new Blob([json], { type: 'application/json' })
    const url = URL.createObjectURL(blob)
    const a = document.createElement('a')
    a.href = url
    a.download = `openapi-${selectedSpec.host}-${selectedSpec.id.slice(0, 8)}.json`
    document.body.appendChild(a)
    a.click()
    document.body.removeChild(a)
    URL.revokeObjectURL(url)
  }

  return (
    <>
      <input
        ref={fileInputRef}
        type="file"
        accept=".json,application/json"
        style={{ display: 'none' }}
        onChange={(e) => void handleFileChange(e)}
      />
      <button className="btn btn--secondary" onClick={handleImportClick}>
        Import
      </button>
      {selectedSpec && (
        <button className="btn btn--secondary" onClick={() => void handleExport()}>
          Export
        </button>
      )}
    </>
  )
}
