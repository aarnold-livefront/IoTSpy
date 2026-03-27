import { useState, useMemo, useCallback, useEffect } from 'react'
import '../../styles/body-viewer.css'

type ViewMode = 'pretty' | 'raw' | 'hex'

interface Props {
  body: string
  headersJson: string
  bodySize: number
}

// ─── Header parsing ──────────────────────────────────────────────────────────

function parseHeaderValue(headersJson: string, name: string): string {
  if (!headersJson) return ''
  try {
    const obj: Record<string, string> = JSON.parse(headersJson)
    const key = Object.keys(obj).find(k => k.toLowerCase() === name.toLowerCase())
    return key ? obj[key] : ''
  } catch {
    // Fallback for raw line-by-line headers (CoAP, etc.)
    const lower = name.toLowerCase()
    const line = headersJson.split(/\r?\n/).find(l => l.toLowerCase().startsWith(lower + ':'))
    return line ? line.slice(name.length + 1).trim() : ''
  }
}

function getContentType(headersJson: string): string {
  return parseHeaderValue(headersJson, 'Content-Type').split(';')[0].trim().toLowerCase()
}

function getContentEncoding(headersJson: string): string {
  return parseHeaderValue(headersJson, 'Content-Encoding').trim().toLowerCase()
}

// ─── JSON syntax highlighting ────────────────────────────────────────────────
// Produces an HTML string. Content is HTML-escaped before any spans are
// injected, so captured traffic cannot inject HTML via this path.

function highlightJson(json: string): string {
  const escaped = json
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')

  // Replace string tokens with null-byte-delimited placeholders so that the
  // number/keyword regexes below do not fire on content inside strings.
  const tokens: string[] = []
  let processed = escaped.replace(/"((?:[^"\\]|\\.)*)"/g, (match, _content, offset, src) => {
    const after = src.slice(offset + match.length).trimStart()
    const cls = after.startsWith(':') ? 'bv-json-key' : 'bv-json-str'
    const idx = tokens.length
    tokens.push(`<span class="${cls}">${match}</span>`)
    return `\x00T${idx}\x00`
  })

  // Numbers (outside of strings)
  processed = processed.replace(
    /\b(-?\d+(?:\.\d+)?(?:[eE][+-]?\d+)?)\b/g,
    '<span class="bv-json-num">$1</span>',
  )
  // Keywords (outside of strings)
  processed = processed.replace(
    /\b(true|false|null)\b/g,
    '<span class="bv-json-kw">$1</span>',
  )

  // Restore string tokens — prefix "T" prevents the number regex above from
  // matching the numeric index inside the \x00T{n}\x00 placeholder.
  return processed.replace(/\x00T(\d+)\x00/g, (_, i) => tokens[parseInt(i)])
}

// ─── XML / HTML formatter ────────────────────────────────────────────────────

function formatXml(source: string): string {
  try {
    const parser = new DOMParser()
    const doc = parser.parseFromString(source, 'application/xml')
    if (doc.querySelector('parsererror')) return source
    return new XMLSerializer().serializeToString(doc)
  } catch {
    return source
  }
}

// ─── Hex dump ────────────────────────────────────────────────────────────────

const HEX_MAX_BYTES = 8192
const HEX_ROW_BYTES = 16

function buildHexDump(data: Uint8Array | string): { lines: string[]; truncated: boolean } {
  const totalLen = data instanceof Uint8Array ? data.length : data.length
  const len = Math.min(totalLen, HEX_MAX_BYTES)
  const truncated = totalLen > HEX_MAX_BYTES
  const lines: string[] = []

  const getByte = data instanceof Uint8Array
    ? (i: number) => data[i]
    : (i: number) => (data as string).charCodeAt(i) & 0xff

  for (let i = 0; i < len; i += HEX_ROW_BYTES) {
    const offset = i.toString(16).padStart(6, '0')
    const bytes: number[] = []
    for (let j = i; j < Math.min(i + HEX_ROW_BYTES, len); j++) {
      bytes.push(getByte(j))
    }
    const h1 = bytes.slice(0, 8).map(b => b.toString(16).padStart(2, '0')).join(' ')
    const h2 = bytes.slice(8).map(b => b.toString(16).padStart(2, '0')).join(' ')
    const hex = `${h1.padEnd(23)}  ${h2.padEnd(23)}`
    const ascii = bytes.map(b => (b >= 0x20 && b < 0x7f) ? String.fromCharCode(b) : '.').join('')
    lines.push(`${offset}  ${hex}  ${ascii}`)
  }
  return { lines, truncated }
}

// ─── Pretty content resolver ─────────────────────────────────────────────────

type PrettyResult =
  | { kind: 'json'; html: string }
  | { kind: 'xml'; text: string }
  | { kind: 'text'; text: string }

function resolvePretty(body: string, contentType: string): PrettyResult {
  // JSON: explicit or sniff-and-try
  const isJsonCt = contentType === 'application/json' || contentType === 'text/json'
  const tryJson = isJsonCt || contentType === '' || contentType === 'text/plain'
    || contentType === 'application/octet-stream'
  if (tryJson) {
    try {
      const pretty = JSON.stringify(JSON.parse(body), null, 2)
      return { kind: 'json', html: highlightJson(pretty) }
    } catch {
      if (isJsonCt) return { kind: 'text', text: body }
    }
  }

  // XML / HTML
  if (contentType.includes('xml') || contentType === 'text/html') {
    return { kind: 'xml', text: formatXml(body) }
  }

  return { kind: 'text', text: body }
}

// ─── Component ───────────────────────────────────────────────────────────────

export default function BodyViewer({ body, headersJson, bodySize }: Props) {
  const contentType = useMemo(() => getContentType(headersJson), [headersJson])
  const contentEncoding = useMemo(() => getContentEncoding(headersJson), [headersJson])

  const isImage = contentType.startsWith('image/')
  const isSvg = contentType === 'image/svg+xml'

  // Detect base64-encoded binary bodies (stored with "b64:" prefix to avoid
  // UTF-8 corruption and SQLite null-byte truncation of binary data).
  const isBase64 = body.startsWith('b64:')
  const decodedBytes = useMemo((): Uint8Array | null => {
    if (!isBase64) return null
    try {
      const bin = atob(body.slice(4))
      const bytes = new Uint8Array(bin.length)
      for (let i = 0; i < bin.length; i++) bytes[i] = bin.charCodeAt(i)
      return bytes
    } catch {
      return null
    }
  }, [body, isBase64])

  const [mode, setMode] = useState<ViewMode>('pretty')
  const [copied, setCopied] = useState(false)
  const [imgError, setImgError] = useState(false)

  // Build an object URL for binary image rendering.
  // Uses decoded bytes when available (base64 storage), otherwise falls back to
  // char-code extraction (legacy path, may be corrupted for non-UTF-8 content).
  const imageUrl = useMemo(() => {
    if (!isImage || isSvg || !body) return null
    try {
      let bytes: Uint8Array
      if (decodedBytes) {
        bytes = decodedBytes
      } else {
        bytes = new Uint8Array(body.length)
        for (let i = 0; i < body.length; i++) bytes[i] = body.charCodeAt(i) & 0xff
      }
      const blob = new Blob([bytes.buffer as ArrayBuffer], { type: contentType })
      return URL.createObjectURL(blob)
    } catch {
      return null
    }
  }, [body, contentType, isImage, isSvg, decodedBytes])

  useEffect(() => () => { if (imageUrl) URL.revokeObjectURL(imageUrl) }, [imageUrl])

  const pretty = useMemo(() => resolvePretty(body, contentType), [body, contentType])
  const hexData = useMemo(() => (mode === 'hex' ? buildHexDump(decodedBytes ?? body) : null), [mode, body, decodedBytes])

  const handleCopy = useCallback(() => {
    navigator.clipboard.writeText(body).then(() => {
      setCopied(true)
      setTimeout(() => setCopied(false), 1500)
    })
  }, [body])

  if (!body) return null

  const encodingLabel = contentEncoding
    ? ['gzip', 'deflate', 'br'].includes(contentEncoding)
      ? `${contentEncoding} ✓ decoded`
      : contentEncoding
    : null

  return (
    <div className="bv-root">
      {/* Toolbar */}
      <div className="bv-toolbar">
        <div className="bv-modes">
          {(['pretty', 'raw', 'hex'] as ViewMode[]).map(m => (
            <button
              key={m}
              className={`bv-mode-btn${mode === m ? ' bv-mode-btn--active' : ''}`}
              onClick={() => setMode(m)}
            >
              {m[0].toUpperCase() + m.slice(1)}
            </button>
          ))}
        </div>

        <div className="bv-badges">
          {contentType && <span className="bv-badge">{contentType}</span>}
          {encodingLabel && <span className="bv-badge bv-badge--encoding">{encodingLabel}</span>}
          <span className="bv-badge bv-badge--size">{bodySize.toLocaleString()} B</span>
        </div>

        <button className="bv-copy-btn" onClick={handleCopy}>
          {copied ? '✓ Copied' : 'Copy'}
        </button>
      </div>

      {/* Content */}
      <div className="bv-content">
        {/* Hex view */}
        {mode === 'hex' && hexData && (
          <pre className="bv-hex">
            <span className="bv-hex-hdr">
              {'Offset    00 01 02 03 04 05 06 07  08 09 0a 0b 0c 0d 0e 0f  ASCII\n'}
            </span>
            {hexData.lines.join('\n')}
            {hexData.truncated
              ? `\n\n… showing first ${HEX_MAX_BYTES.toLocaleString()} of ${bodySize.toLocaleString()} bytes`
              : ''}
          </pre>
        )}

        {/* Raw view */}
        {mode === 'raw' && <pre className="bv-raw">{body}</pre>}

        {/* Pretty view */}
        {mode === 'pretty' && (
          <>
            {/* Binary image */}
            {isImage && !isSvg && (
              imageUrl && !imgError
                ? (
                  <div className="bv-img-wrap">
                    <img
                      src={imageUrl}
                      alt="Captured response"
                      className="bv-img"
                      onError={() => setImgError(true)}
                    />
                  </div>
                )
                : (
                  <div className="bv-binary-note">
                    ⚠ Binary image content could not be rendered. Switch to Hex view to inspect bytes.
                  </div>
                )
            )}

            {/* SVG: plain text (it's XML-based and stored correctly) */}
            {isSvg && <pre className="bv-pretty">{body}</pre>}

            {/* JSON with syntax highlighting */}
            {!isImage && pretty.kind === 'json' && (
              <pre
                className="bv-pretty bv-pretty--json"
                // Safe: body is HTML-escaped inside highlightJson before any spans are injected
                dangerouslySetInnerHTML={{ __html: pretty.html }}
              />
            )}

            {/* XML / HTML / plain text */}
            {!isImage && pretty.kind !== 'json' && (
              <pre className="bv-pretty">{pretty.kind === 'xml' ? pretty.text : pretty.text}</pre>
            )}
          </>
        )}
      </div>
    </div>
  )
}
