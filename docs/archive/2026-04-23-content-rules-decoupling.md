# Content Rules Decoupling — Implementation Plan

**Branch:** `phase22-rich_media_content_replacement` (current)  
**Status:** COMPLETE  
**Goal:** Decouple `ContentReplacementRule` from `ApiSpecDocument` so users can create
content manipulation rules without first creating an API spec. Rules become first-class
entities scoped by host directly.

---

## Background / why

`ContentReplacementRule` currently has a required `ApiSpecDocumentId` FK. The proxy engine
(`ApiSpecMockService.ApplyMockAsync`) only picks up rules for a host if there is a matching
`ApiSpecDocument` with `Status == Active && MockEnabled == true`. This means:

- You must create/generate/import an API spec before you can add any replacement rule
- "Activating" a spec is a separate step, and the UI buries rules under the "API Spec" tab
- The spec is conceptually a documentation artifact (OpenAPI JSON); coupling it to live traffic
  manipulation is a design mistake that confuses users

---

## Scope

### Backend (IoTSpy.Storage + IoTSpy.Core + IoTSpy.Manipulation + IoTSpy.Api)

**Step 1 — Core model change** (`src/IoTSpy.Core/Models/ContentReplacementRule.cs`)
- Make `ApiSpecDocumentId` nullable (`Guid?`)
- Add `string? Host` property — used for standalone rules not attached to any spec
- Rule is "standalone" when `ApiSpecDocumentId == null`; `Host` is then required for scoping

**Step 2 — EF migration** (run from repo root)
```bash
dotnet ef migrations add DecoupleContentRules \
  --project src/IoTSpy.Storage \
  --startup-project src/IoTSpy.Api
dotnet ef database update \
  --project src/IoTSpy.Storage \
  --startup-project src/IoTSpy.Api
```
Migration must:
- Make `ApiSpecDocumentId` nullable on `ContentReplacementRules` table
- Add nullable `Host` column to `ContentReplacementRules` table

**Step 3 — Repository** (`src/IoTSpy.Storage/Repositories/ApiSpecRepository.cs`)
- Add `GetStandaloneRulesForHostAsync(string host, CancellationToken ct)` to
  `IApiSpecRepository` and implement it:
  ```csharp
  db.ContentReplacementRules
      .Where(r => r.ApiSpecDocumentId == null && r.Host == host && r.Enabled)
      .OrderBy(r => r.Priority)
      .ToListAsync(ct)
  ```
- Also add `GetAllStandaloneRulesAsync` (for the new list endpoint)
- Existing rule CRUD methods already work; just need to handle nullable FK

**Step 4 — Service layer** (`src/IoTSpy.Manipulation/ApiSpec/ApiSpecMockService.cs`)

In `ApplyMockAsync`, after existing spec-attached rules are applied, also query and apply
standalone rules for the host. Merge and re-sort by priority:

```csharp
// Existing: spec-attached rules
var spec = await GetActiveSpecAsync(message.Host, ct);
var specRules = spec?.ReplacementRules.Where(r => r.Enabled).ToList() ?? [];

// New: standalone rules (no spec required)
var standaloneRules = await GetStandaloneRulesAsync(message.Host, ct);

var allRules = specRules.Concat(standaloneRules)
    .OrderBy(r => r.Priority)
    .ToList();

if (allRules.Count == 0) return false;
return await contentReplacer.ApplyAsync(message, allRules, ct);
```

`GetStandaloneRulesAsync` uses a scoped `IApiSpecRepository` (same pattern as
`GetActiveSpecAsync`).

**Step 5 — New API controller** (`src/IoTSpy.Api/Controllers/ContentRulesController.cs`)

New controller at `/api/contentrules` — no spec required. Endpoints:

| Method | Path | Description |
|---|---|---|
| GET | `/api/contentrules` | List all standalone rules (optionally filter by `?host=`) |
| POST | `/api/contentrules` | Create standalone rule (`Host` required in body) |
| PUT | `/api/contentrules/{id}` | Update standalone rule |
| DELETE | `/api/contentrules/{id}` | Delete standalone rule |
| POST | `/api/contentrules/{id}/preview` | Preview rule (same shape as existing spec preview) |

Request DTO for create/update — same fields as existing `CreateReplacementRuleRequest` but
with `Host` (required on create) instead of a `specId` path param:

```csharp
public record CreateContentRuleRequest(
    string Host,               // e.g. "ads.example.com"
    string Name,
    ContentMatchType MatchType,
    string MatchPattern,
    ContentReplacementAction Action,
    string? ReplacementValue = null,
    string? ReplacementFilePath = null,
    string? ReplacementContentType = null,
    string? HostPattern = null,
    string? PathPattern = null,
    int Priority = 0,
    bool Enabled = true,
    int? SseInterEventDelayMs = null,
    bool? SseLoop = null
);
```

Preview endpoint delegates to `ReplacementPreviewService` (already exists, no changes needed).

**Step 6 — Tests**

Add tests in `IoTSpy.Manipulation.Tests` or the relevant test project:
- Standalone rule is applied by proxy for matching host
- Spec-attached rules still work (backwards compatibility)
- Standalone rule for host X does not apply to host Y
- Priority ordering across standalone + spec rules

---

### Frontend

**Step 7 — API client** (`frontend/src/api/contentrules.ts` — new file)

Mirror shape of `frontend/src/api/apispec.ts` rule functions but hitting `/api/contentrules`:
- `listContentRules(host?: string): Promise<ContentReplacementRule[]>`
- `createContentRule(req: CreateContentRuleRequest): Promise<ContentReplacementRule>`
- `updateContentRule(id: string, req: UpdateContentRuleRequest): Promise<ContentReplacementRule>`
- `deleteContentRule(id: string): Promise<void>`
- `previewContentRule(id: string, req: PreviewRuleRequest): Promise<PreviewRuleResult>`

**Step 8 — Types** (`frontend/src/types/api.ts`)
- Add `CreateContentRuleRequest` interface (same as `CreateReplacementRuleRequest` + `host: string`)
- Add `UpdateContentRuleRequest` (same fields, all optional)
- `ContentReplacementRule` already has `host?: string` — confirm it's present or add it

**Step 9 — Hook** (`frontend/src/hooks/useContentRules.ts` — new file)

```typescript
// Manages standalone content rules for a selected host
export function useContentRules(host: string | null) { ... }
// Returns: rules, loading, error, addRule, editRule, removeRule, previewRule
```

**Step 10 — New panel** (`frontend/src/components/contentrules/ContentRulesPanel.tsx` — new file)

Structure:
```
ContentRulesPanel
  ├── Host selector (text input + button, or dropdown from known captured hosts)
  ├── AssetLibrary (existing component, compact=false)
  └── RulesEditor (existing ReplacementRulesEditor adapted for standalone rules)
       └── RulePreviewModal (existing, already updated with capture mode)
```

The host selector should suggest hosts from captured traffic (call
`listCaptures({ pageSize: 1 })` per distinct host or use a dedicated hosts endpoint if one
exists — check `GET /api/captures` distinct hosts).

**Step 11 — Wire into ManipulationPanel** (`frontend/src/components/manipulation/ManipulationPanel.tsx`)

- Add `'contentrules'` tab: **"Content Rules"**
- Keep `'apispec'` tab but strip `ReplacementRulesEditor` out of `ApiSpecPanel` — the API Spec
  tab becomes: spec list, generate/import/export/refine only (no rules, no asset library)

**Step 12 — Clean up ApiSpecPanel** (`frontend/src/components/apispec/ApiSpecPanel.tsx`)
- Remove `ReplacementRulesEditor` import and usage
- Remove `AssetLibrary` toggle (moves to ContentRulesPanel)
- Keep: spec list, statusBadge, `GenerateSpecDialog`, `ImportExportControls`,
  `SpecEditor`, activate/deactivate/refine/delete buttons

---

## Files to create
- `src/IoTSpy.Api/Controllers/ContentRulesController.cs`
- `frontend/src/api/contentrules.ts`
- `frontend/src/hooks/useContentRules.ts`
- `frontend/src/components/contentrules/ContentRulesPanel.tsx`
- EF migration (name: `DecoupleContentRules`)

## Files to modify
- `src/IoTSpy.Core/Models/ContentReplacementRule.cs` — nullable FK, add Host
- `src/IoTSpy.Core/Interfaces/IApiSpecRepository.cs` — add GetStandaloneRulesForHostAsync
- `src/IoTSpy.Storage/Repositories/ApiSpecRepository.cs` — implement new method
- `src/IoTSpy.Manipulation/ApiSpec/ApiSpecMockService.cs` — merge standalone rules in ApplyMockAsync
- `frontend/src/types/api.ts` — CreateContentRuleRequest, host on ContentReplacementRule
- `frontend/src/components/manipulation/ManipulationPanel.tsx` — add Content Rules tab
- `frontend/src/components/apispec/ApiSpecPanel.tsx` — remove rules/assets

## Files NOT to change
- `ContentReplacer.cs` — no changes, already accepts a rule list
- `ReplacementPreviewService.cs` — no changes, already works rule-only
- `ReplacementRulesEditor.tsx` — reused as-is inside ContentRulesPanel (host comes from panel)
- `RulePreviewModal.tsx` — reused as-is (already updated with capture mode)
- `AssetLibrary.tsx` — reused as-is (already updated with delete-in-picker fix)

---

## Verification checklist
- [x] `dotnet test` — 608 tests, 0 failures
- [x] Existing spec-attached rules still fire (backwards compat — ApplyMockAsync merges both)
- [x] Standalone rule fires for correct host, not for other hosts (GetStandaloneRulesForHostAsync)
- [x] Content Rules tab visible in Manipulation panel
- [x] Can create a rule with just a host (no spec needed)
- [x] Asset library accessible and manageable from Content Rules tab (Rules + Assets sub-tabs)
- [x] Rule preview works (synthetic + capture modes) — overridePreview prop routes to /api/contentrules/{id}/preview
- [x] API Spec tab no longer shows rules or asset library
- [x] "Rules" tab renamed to "Traffic Rules" — eliminates name collision with Content Rules nested Rules sub-tab
- [x] Content Rules host gate removed — rules load immediately, host input is a live filter (no "Load" button)
- [x] "Assets" promoted to top-level Manipulation tab — no longer nested inside Content Rules
- [x] `ContentReplacementRule` TS type has `host?: string` field

## Implementation notes
- `RulePreviewModal` gained an `overridePreview` prop so ContentRulesPanel routes previews
  to `/api/contentrules/{id}/preview` instead of the spec-scoped apispec endpoint
- `ReplacementPreviewService` uses `GetRuleByIdAsync` when `specId == Guid.Empty` (new method)
- Migration `DecoupleContentRules` hand-written with raw SQL + `suppressTransaction: true` on
  the PRAGMA statements to avoid the EF Core SQLite runtime warning about non-transactional ops
- Also delivered in this session (Phase 22 UI fixes, pre-decoupling):
  - `ReplacementRulesEditor`: hostPattern + pathPattern form fields; scope shown on rule rows
  - `RulePreviewModal`: Synthetic / From Capture mode toggle
  - `AssetLibrary`: delete button always visible; upload always clickable; compact upload button
