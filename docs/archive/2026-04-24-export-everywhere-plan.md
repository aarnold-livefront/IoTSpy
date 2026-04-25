# Export Everywhere

## Goal

Give users portable, shareable representations of the data and configurations they build in IoTSpy: captured streaming responses as reusable asset templates, fuzzer results, scan findings, and full ruleset bundles that can be moved across environments.

---

## Features

### 1 — Capture → Streaming Asset Export *(implement first)*

Captured SSE (`text/event-stream`) and NDJSON (`application/x-ndjson`) response bodies are already stored in `CapturedRequest.ResponseBody`. This feature exposes them as usable asset files for `MockSseStream` content rules.

**Two delivery modes:**

| Mode | Endpoint | Effect |
|---|---|---|
| Save to server | `POST /api/captures/{id}/export-as-asset` | Writes file to `data/assets/` and returns path; immediately available in the Assets library |
| Download to client | `GET /api/captures/{id}/download-body` | Streams file to the browser as a download; user edits locally, then uploads via `POST /api/apispec/assets` |

**Shared validation (private helper on `CapturesController`):**
1. Fetch via `CaptureRepository.GetByIdAsync(id)` — 404 if missing.
2. 422 if `ResponseBody` is null/empty or starts with `"b64:"` (binary — can't be an SSE asset).
3. Parse `Content-Type` from `ResponseHeaders` JSON.
4. Map to extension:
   - `text/event-stream` → `.sse`
   - `application/x-ndjson`, `application/json-stream`, `application/jsonlines` → `.ndjson`
   - Anything else → 422.
5. Build safe filename: `{sanitizedHost}_{sanitizedPath}_{Guid.NewGuid():N}{ext}` (strip non-alphanumeric, truncate segments to ~32 chars).

**Save-to-server additional steps:**
- `Directory.CreateDirectory(assetsDir)` guard.
- Write `ResponseBody` as UTF-8 to `Path.Combine(assetsDir, filename)`.
- Return `ExportCaptureAsAssetResult { FileName, FilePath, ContentType, SizeBytes }`.

**Download additional step:**
- Return `File(Encoding.UTF8.GetBytes(responseBody), contentType, filename)` — `FileContentResult` with `Content-Disposition: attachment`.

**Assets directory:** extract `Path.Combine(AppContext.BaseDirectory, "data", "assets")` from `ApiSpecController` into a shared `AssetsPaths.AssetsDirectory` static (new tiny file `src/IoTSpy.Api/AssetsPaths.cs`).

**Frontend — `ResponseTab.tsx`:**
- Show "Save as Asset" + "Download" buttons when `Content-Type` from `responseHeaders` is `text/event-stream` or contains `ndjson`/`json-stream`.
- "Save as Asset" → `POST /api/captures/{id}/export-as-asset`; success toast with filename + copy-path button.
- "Download" → link to `GET /api/captures/{id}/download-body` with the `download` attribute.

**API module additions — `frontend/src/api/captures.ts`:**
```ts
exportAsAsset(id: string): Promise<ExportCaptureAsAssetResult>
downloadBodyUrl(id: string): string   // returns URL for <a download> tag
```

**Tests (in `IoTSpy.Api.Tests` or `IoTSpy.Storage.Tests`):**
- SSE capture → `.sse` file written, correct content.
- NDJSON capture → `.ndjson` extension.
- 404 for unknown ID.
- 422 for `"b64:"` binary body.
- 422 for non-streaming Content-Type.
- Download endpoint returns correct `Content-Disposition` header and body bytes.

**Manual verification:**
1. Intercept a real SSE endpoint through the proxy.
2. Open capture detail — confirm both buttons appear only for streaming content types.
3. "Save as Asset" → file appears in the Assets tab.
4. "Download" → browser saves the `.sse`/`.ndjson` file.
5. Create a `MockSseStream` Content Rule pointing at the exported file; trigger it and verify the browser receives the stream.

---

### 2 — Fuzzer Results Export

**Endpoint:** `GET /api/manipulation/fuzzers/{id}/export`

Returns `application/json` with `Content-Disposition: attachment; filename="fuzzer-{id}.json"`:
```json
{
  "fuzzerId": "...",
  "exportedAt": "2026-04-24T00:00:00Z",
  "results": [ /* FuzzerResult records for this run */ ]
}
```

**Frontend:** "Export" button in the Fuzzer tab results view.

---

### 3 — Scan Findings Export

**Endpoint:** `GET /api/scanner/jobs/{id}/export`

Returns `application/json` with `Content-Disposition: attachment; filename="scan-{id}.json"`:
```json
{
  "jobId": "...",
  "deviceId": "...",
  "exportedAt": "2026-04-24T00:00:00Z",
  "findings": [ /* ScanFinding records */ ]
}
```

**Frontend:** "Export" button in the scan job detail panel.

---

### 4 — Ruleset Bundle Export

**Endpoint:** `GET /api/manipulation/export` (optional `?specId=` to scope to one API spec)

Returns a self-contained JSON bundle with `Content-Disposition: attachment; filename="ruleset.json"`:
```json
{
  "exportedAt": "2026-04-24T00:00:00Z",
  "trafficRules": [...],
  "breakpoints": [...],
  "contentReplacementRules": [...],
  "apiSpecs": [
    { "document": { ... }, "rules": [...] }
  ],
  "referencedAssets": ["abc123_stream.sse"]
}
```

Asset files referenced by `ReplacementFilePath` are **not** embedded — `referencedAssets` tells users which files to carry over alongside the bundle.

**Frontend:** "Export Ruleset" button in the Manipulation panel toolbar.

---

## Implementation order

1. **1** — Capture → asset export (highest immediate value, smallest scope)
2. **2** — Fuzzer results export
3. **3** — Scan findings export
4. **4** — Ruleset bundle export (most complex — touches multiple repositories)

Each sub-feature is independently shippable.
