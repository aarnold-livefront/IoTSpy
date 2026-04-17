# Design: Admin UI & Body Viewer Improvements

**Date:** 2026-03-27
**Status:** Approved

---

## Overview

Two independent but co-shipped features:

1. **Admin UI** — a dedicated `/admin` page for database management, certificate administration, audit log review, and user management.
2. **Body Viewer: Stream Rendering** — collapsible per-event display for SSE and NDJSON response bodies.

---

## Feature 1: Admin UI (`/admin`)

### Route & Access

- New React route at `/admin`.
- A wrench icon in the `Header` component links to `/admin`. The link is only rendered when the logged-in user has the `Admin` role.
- Navigating to `/admin` as a non-Admin redirects to `/`.
- The page is a full-page layout (not a modal), consistent with the existing dashboard pages.

### Layout

Horizontal tab bar at the top of the page, matching the manipulation panel tab style:

```
Database | Certificates | Audit Log | Users
```

---

### Tab: Database

Three stat cards displayed in a responsive grid:

**Card: Captures & Logs**
- Stats: total row count, estimated storage size, oldest capture timestamp.
- Purge actions (each requires a confirmation dialog stating the record count to be deleted):
  - Purge older than N days (slider: 1–365)
  - Purge by device (dropdown of known devices)
  - Purge by host (text input)
  - Purge all captures
- Export actions (trigger browser file download):
  - Export as JSON — full `CapturedRequest` objects including headers and bodies
  - Export as CSV — summary columns: timestamp, method, host, path, status code, request size, response size, device

**Card: Packets**
- Stats: total row count, estimated storage size, oldest packet timestamp.
- Purge actions:
  - Purge older than N days
  - Purge all packets
- Export actions:
  - Export as JSON
  - Export as CSV — columns: timestamp, protocol, source IP, destination IP, source port, destination port, length

**Card: Configuration**
- Stats: counts of manipulation rules, breakpoints, scheduled scans, OpenRTB policies, API spec documents.
- No purge (configuration is not bulk-deleted via admin).
- Export action:
  - Export as JSON — full snapshot of all configuration tables: manipulation rules, breakpoints, fuzzer jobs, scheduled scans, OpenRTB policies and PII logs, API spec documents and replacement rules.

---

### Tab: Certificates

**Root CA section**
- Displays: subject/CN, serial number, validity dates (issued / expires), SHA-256 fingerprint.
- Buttons:
  - *Download DER* — existing `GET /api/certificates/root-ca/download`
  - *Download PEM* — existing `GET /api/certificates/root-ca/pem`
  - *Regenerate CA* — calls new `POST /api/certificates/root-ca/regenerate`. Requires a confirmation dialog: "This will invalidate all existing leaf certificates and require re-installing the root CA on all devices."

**Leaf Certificates section**
- Displays total leaf cert count.
- Table of the 50 most recently issued leaf certs: host, issued date, expiry date.
- *Purge all leaf certs* button — existing `DELETE /api/certificates/purge-leaf-certs`. Requires confirmation.

---

### Tab: Audit Log

- Paginated table (50 rows per page) of `AuditEntry` records from `GET /api/auth/audit`.
- Columns: timestamp, username, action, entity type, entity ID, IP address.
- No write actions — read-only view.

---

### Tab: Users

- Table of all user accounts from `GET /api/users`: username, display name, role badge (Admin/Operator/Viewer), created date.
- Inline role change: dropdown per row to update role, calls `PUT /api/users/{id}`.
- Delete user: trash icon per row, calls `DELETE /api/users/{id}`. Requires confirmation. Cannot delete your own account.
- The Admin role is required to access this tab (enforced on both frontend and backend).

---

## Feature 2: Body Viewer — Stream Rendering

### Detection

Before the existing JSON/XML checks in `resolvePretty`, a new stream detection pass runs:

| Condition | Detected as |
|---|---|
| `Content-Type: text/event-stream` | SSE stream |
| `Content-Type: application/x-ndjson` or `application/jsonl` | NDJSON stream |
| Other content type + body contains `\n` + every non-empty line parses as JSON | NDJSON sniff |

SSE parsing: split body on `\n\n`, extract `data:` fields from each block. Non-`data:` fields (`event:`, `id:`, `retry:`) are retained as metadata per event. Events with no `data:` field are skipped.

NDJSON parsing: split body on `\n`, filter blank lines, attempt `JSON.parse` on each line. Lines that fail to parse render as plain text rows.

### Rendering

When a stream is detected, Pretty mode changes to stream layout:

- **Toolbar badge**: `N events` badge added alongside the existing content-type and size badges.
- **Expand all / Collapse all** toggle appears in the toolbar.
- Each event renders as a row:
  - **Collapsed** (default): event index, event type label (SSE `event:` field, or first string-valued key in the JSON object), byte size of the raw event.
  - **Expanded** (click to toggle): full syntax-highlighted JSON using the existing `highlightJson` function. SSE metadata fields (`event:`, `id:`) shown above the JSON block.
  - Non-JSON data values render as plain text (e.g. `data: heartbeat`).
- Raw mode: unchanged — shows the full unprocessed stream body.
- Hex mode: unchanged.

### What this fixes

- SSE responses (`text/event-stream`) previously showed as an unstructured wall of text in both Pretty and Raw modes.
- NDJSON / chunked JSON streams had the same problem.
- The "pretty = raw look identical" issue for stream content is resolved by the structured event layout.

---

## Backend: New Endpoints

### `AdminController` (new)

All endpoints require `Admin` role.

| Method | Route | Description |
|---|---|---|
| `GET` | `/api/admin/stats` | Returns counts and sizes for captures, packets, scan findings |
| `DELETE` | `/api/admin/captures` | Purge captures. Query params: `olderThanDays` (int), `deviceId` (guid), `host` (string). At least one param required. |
| `DELETE` | `/api/admin/packets` | Purge packets. Query params: `olderThanDays` (int). At least one param required. |
| `GET` | `/api/admin/export/logs` | Download captures + scan findings. Query param: `format=json\|csv`. Streams response. |
| `GET` | `/api/admin/export/packets` | Download packets. Query param: `format=json\|csv`. Streams response. |
| `GET` | `/api/admin/export/config` | Download configuration snapshot as JSON. |

### `CertificatesController` (modified)

| Method | Route | Description |
|---|---|---|
| `POST` | `/api/certificates/root-ca/regenerate` | Delete all certs from DB, regenerate root CA. Admin role required. |

### `UsersController` (new)

All endpoints require `Admin` role.

| Method | Route | Description |
|---|---|---|
| `GET` | `/api/users` | List all users (id, username, displayName, role, createdAt). Excludes password hashes. |
| `PUT` | `/api/users/{id}` | Update role and/or displayName. Cannot demote the last Admin. |
| `DELETE` | `/api/users/{id}` | Delete user. Cannot delete self. Cannot delete last Admin. |

---

## Frontend: New & Modified Files

### New files
```
frontend/src/pages/AdminPage.tsx
frontend/src/components/admin/DatabaseTab.tsx
frontend/src/components/admin/CertificatesTab.tsx
frontend/src/components/admin/AuditLogTab.tsx
frontend/src/components/admin/UsersTab.tsx
frontend/src/styles/admin.css
```

### Modified files
```
frontend/src/components/common/BodyViewer.tsx   — stream detection + collapsible event rows
frontend/src/styles/body-viewer.css             — event row styles
frontend/src/components/layout/Header.tsx       — wrench icon linking to /admin (Admin role only)
frontend/src/App.tsx                            — add /admin route
src/IoTSpy.Api/Controllers/CertificatesController.cs  — regenerate endpoint
```

### New backend files
```
src/IoTSpy.Api/Controllers/AdminController.cs
src/IoTSpy.Api/Controllers/UsersController.cs
```

---

## Constraints & Notes

- No new EF Core migrations required — all data models already exist.
- Export endpoints stream the response (no in-memory buffering of large datasets); use `yield return` or `IAsyncEnumerable` for CSV rows.
- Purge endpoints return a `{ deleted: N }` JSON response.
- `DELETE /api/admin/captures` with no query params returns `400 Bad Request` to prevent accidental full purge via a missing parameter; the "purge all" action from the UI sends `?purgeAll=true`.
- The `Regenerate CA` action calls `ICertificateAuthority` to rebuild the root CA, then deletes all `CertificateEntry` rows and regenerates a fresh root.
- Audit log entries for all admin destructive actions (purge, regenerate, user delete) are written via the existing `AuditService`.
- Body viewer stream detection is purely frontend — no backend changes required.
