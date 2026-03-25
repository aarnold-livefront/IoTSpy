import { useCallback, useEffect, useRef, useState } from 'react'
import type { Device } from '../../types/api'

interface Props {
  devices: Device[]
  value: string            // deviceId or ''
  clientIp: string         // manual IP/MAC filter
  onDeviceChange: (deviceId: string) => void
  onClientIpChange: (clientIp: string) => void
}

/** Matches an IPv4 address, IPv6 prefix, or MAC address pattern */
function looksLikeAddress(input: string): boolean {
  const trimmed = input.trim()
  // IPv4 (partial or full)
  if (/^\d{1,3}(\.\d{0,3}){0,3}$/.test(trimmed)) return true
  // IPv6 (any hex:hex pattern)
  if (/^[0-9a-f]{0,4}(:[0-9a-f]{0,4})+$/i.test(trimmed)) return true
  // MAC address (colon or dash separated)
  if (/^([0-9a-f]{2}[:\-]){1,5}[0-9a-f]{0,2}$/i.test(trimmed)) return true
  return false
}

export default function DeviceFilter({ devices, value, clientIp, onDeviceChange, onClientIpChange }: Props) {
  const [inputValue, setInputValue] = useState('')
  const [showDropdown, setShowDropdown] = useState(false)
  const [highlightIdx, setHighlightIdx] = useState(-1)
  const containerRef = useRef<HTMLDivElement>(null)
  const inputRef = useRef<HTMLInputElement>(null)

  // Sync display text when external value changes
  useEffect(() => {
    if (value) {
      const dev = devices.find(d => d.id === value)
      setInputValue(dev ? formatDevice(dev) : value.slice(0, 8))
    } else if (clientIp) {
      setInputValue(clientIp)
    } else {
      setInputValue('')
    }
  }, [value, clientIp, devices])

  const filtered = devices.filter(d => {
    if (!inputValue.trim()) return true
    const q = inputValue.toLowerCase()
    return (
      d.ipAddress.toLowerCase().includes(q) ||
      d.macAddress.toLowerCase().includes(q) ||
      d.hostname.toLowerCase().includes(q) ||
      d.label.toLowerCase().includes(q) ||
      d.vendor.toLowerCase().includes(q)
    )
  })

  const selectDevice = useCallback((dev: Device) => {
    onClientIpChange('')
    onDeviceChange(dev.id)
    setInputValue(formatDevice(dev))
    setShowDropdown(false)
    setHighlightIdx(-1)
  }, [onDeviceChange, onClientIpChange])

  function handleInputChange(text: string) {
    setInputValue(text)
    setShowDropdown(true)
    setHighlightIdx(-1)

    // If user clears input, clear both filters
    if (!text.trim()) {
      onDeviceChange('')
      onClientIpChange('')
    }
  }

  function commitManualValue() {
    const trimmed = inputValue.trim()
    if (!trimmed) {
      onDeviceChange('')
      onClientIpChange('')
      setShowDropdown(false)
      return
    }

    // Check if input matches a known device
    const exactMatch = devices.find(d =>
      d.ipAddress === trimmed ||
      d.macAddress.toLowerCase() === trimmed.toLowerCase() ||
      d.hostname.toLowerCase() === trimmed.toLowerCase()
    )
    if (exactMatch) {
      selectDevice(exactMatch)
      return
    }

    // Treat as manual IP/MAC filter
    onDeviceChange('')
    onClientIpChange(trimmed)
    setShowDropdown(false)
  }

  function handleKeyDown(e: React.KeyboardEvent) {
    if (e.key === 'Escape') {
      setShowDropdown(false)
      return
    }
    if (e.key === 'Enter') {
      if (highlightIdx >= 0 && highlightIdx < filtered.length) {
        selectDevice(filtered[highlightIdx])
      } else {
        commitManualValue()
      }
      return
    }
    if (e.key === 'ArrowDown') {
      e.preventDefault()
      setHighlightIdx(i => Math.min(i + 1, filtered.length - 1))
      return
    }
    if (e.key === 'ArrowUp') {
      e.preventDefault()
      setHighlightIdx(i => Math.max(i - 1, 0))
    }
  }

  // Close dropdown when clicking outside
  useEffect(() => {
    function handleClickOutside(e: MouseEvent) {
      if (containerRef.current && !containerRef.current.contains(e.target as Node)) {
        setShowDropdown(false)
      }
    }
    document.addEventListener('mousedown', handleClickOutside)
    return () => document.removeEventListener('mousedown', handleClickOutside)
  }, [])

  const isManualFilter = !value && !!clientIp
  const hasActiveFilter = !!value || !!clientIp

  return (
    <div className="device-filter" ref={containerRef}>
      <div className={`device-filter__input-wrap${hasActiveFilter ? ' device-filter__input-wrap--active' : ''}`}>
        <input
          ref={inputRef}
          className="filter-input device-filter__input"
          type="text"
          placeholder="Device IP, MAC, or name…"
          value={inputValue}
          onChange={e => handleInputChange(e.target.value)}
          onFocus={() => setShowDropdown(true)}
          onBlur={() => {
            // Delay to allow dropdown click to register
            setTimeout(() => {
              if (!containerRef.current?.contains(document.activeElement)) {
                commitManualValue()
              }
            }, 150)
          }}
          onKeyDown={handleKeyDown}
          aria-label="Filter by device"
          aria-expanded={showDropdown}
          aria-haspopup="listbox"
          autoComplete="off"
        />
        {hasActiveFilter && (
          <button
            className="device-filter__clear"
            onClick={() => {
              setInputValue('')
              onDeviceChange('')
              onClientIpChange('')
              inputRef.current?.focus()
            }}
            title="Clear device filter"
            aria-label="Clear device filter"
            tabIndex={-1}
          >
            ✕
          </button>
        )}
      </div>

      {isManualFilter && looksLikeAddress(clientIp) && (
        <span className="device-filter__badge" title={`Filtering by IP/MAC: ${clientIp}`}>
          {clientIp}
        </span>
      )}

      {showDropdown && (filtered.length > 0 || inputValue.trim()) && (
        <ul className="device-filter__dropdown" role="listbox">
          {!inputValue.trim() && (
            <li
              className="device-filter__option device-filter__option--all"
              onClick={() => {
                onDeviceChange('')
                onClientIpChange('')
                setInputValue('')
                setShowDropdown(false)
              }}
              role="option"
              aria-selected={!value && !clientIp}
            >
              All devices
            </li>
          )}
          {filtered.map((d, i) => (
            <li
              key={d.id}
              className={`device-filter__option${i === highlightIdx ? ' device-filter__option--highlighted' : ''}${d.id === value ? ' device-filter__option--selected' : ''}`}
              onClick={() => selectDevice(d)}
              onMouseEnter={() => setHighlightIdx(i)}
              role="option"
              aria-selected={d.id === value}
            >
              <span className="device-filter__option-label">
                {d.label || d.hostname || d.ipAddress}
              </span>
              <span className="device-filter__option-meta">
                {d.ipAddress}
                {d.macAddress && <span className="device-filter__option-mac">{d.macAddress}</span>}
              </span>
            </li>
          ))}
          {filtered.length === 0 && inputValue.trim() && (
            <li className="device-filter__option device-filter__option--hint">
              Press Enter to filter by &quot;{inputValue.trim()}&quot;
            </li>
          )}
        </ul>
      )}
    </div>
  )
}

function formatDevice(d: Device): string {
  if (d.label) return d.label
  if (d.hostname) return d.hostname
  return d.ipAddress || d.id.slice(0, 8)
}
