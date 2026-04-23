import { render, screen, waitFor } from '@testing-library/react'
import { describe, it, expect, vi, beforeEach } from 'vitest'
import AssetLibrary from '../components/apispec/AssetLibrary'

// Mock the API layer so we don't hit the network in unit tests.
vi.mock('../api/apispec', async () => {
  return {
    listAssets: vi.fn(async () => [
      { filePath: '/x/logo.png', fileName: 'logo.png', size: 1024, lastModified: '2026-04-20T00:00:00Z' },
      { filePath: '/x/feed.ndjson', fileName: 'feed.ndjson', size: 512, lastModified: '2026-04-20T00:00:00Z' },
    ]),
    uploadAssets: vi.fn(async (files: File[]) =>
      files.map((f) => ({ filePath: `/x/${f.name}`, fileName: f.name })),
    ),
    deleteAsset: vi.fn(async () => undefined),
    getAssetContentUrl: (f: string) => `/api/apispec/assets/${encodeURIComponent(f)}/content`,
  }
})

describe('AssetLibrary', () => {
  beforeEach(() => { vi.clearAllMocks() })

  it('renders the known assets with kind badges', async () => {
    render(<AssetLibrary />)
    await waitFor(() => expect(screen.getByText('logo.png')).toBeTruthy())
    expect(screen.getByText('feed.ndjson')).toBeTruthy()
    // Kind badges are rendered lowercase per the component.
    expect(screen.getByText('image')).toBeTruthy()
    expect(screen.getByText('stream')).toBeTruthy()
  })

  it('shows the drop zone with hint text', async () => {
    render(<AssetLibrary />)
    await waitFor(() => expect(screen.getByText('logo.png')).toBeTruthy())
    expect(screen.getByText(/drop files here/i)).toBeTruthy()
  })
})
