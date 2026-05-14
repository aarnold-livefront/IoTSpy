# ADR 0002 — Audit log tiered retention

**Date:** 2026-05-14  
**Status:** Accepted  
**Deciders:** Annalise  
**Closes:** CODE-REVIEW-FINDINGS #46

---

## Context

`AuditEntries` has a write-once UPDATE trigger (`AuditWriteOnceTrigger` migration) that prevents tampering. However, `DataRetentionService` deletes old audit rows via `ExecuteDeleteAsync`, which bypasses the trigger protection goal. Adding a BEFORE DELETE trigger would conflict with retention and leave audit logs growing unboundedly.

## Decision

**Tiered retention:** recent audit entries stay in `AuditEntries` (fast, queryable). Older entries are moved to an `AuditArchive` table before deletion from the main table.

```
AuditEntries  ──(age > AuditRetentionDays)──►  AuditArchive  ──(age > AuditArchivePurgeDays)──►  deleted
```

### New configuration knobs (in `DataRetention` section)

| Key | Default | Meaning |
|---|---|---|
| `AuditRetentionDays` | `0` | Days to keep entries in `AuditEntries` before archiving. `0` = never archive. |
| `AuditArchivePurgeDays` | `0` | Days to keep entries in `AuditArchive` before hard deletion. `0` = keep forever. |

### Retention service behaviour

Each pass:
1. If `AuditRetentionDays > 0`: copy rows older than cutoff from `AuditEntries` to `AuditArchive`, then delete them from `AuditEntries`.
2. If `AuditArchivePurgeDays > 0`: delete rows older than cutoff from `AuditArchive`.

### Manual admin actions

New endpoints under `POST /api/admin/audit/archive` and `DELETE /api/admin/audit/archive` let admins trigger archiving and archive purging on demand, mirroring the capture/packet purge endpoints already in the admin UI.

### No DELETE trigger on AuditEntries

A BEFORE DELETE trigger is not added. The protection is the two-step archive process: any code path that deletes from `AuditEntries` is expected to archive first. The DELETE pathway is internal to `AuditRepository.ArchiveOlderThanAsync` only; the admin UI purge card does not expose a direct delete-without-archive action.

## Alternatives considered

1. **Immutable archive + allow DELETE** — more complex; requires an external sink or append-only table with separate backup strategy.
2. **Stop pruning audit rows** — leads to unbounded DB growth in long-running deployments.
4. **DELETE-protect entirely** — blocks the retention service; requires SQLite workarounds with no clean escape hatch.

## Consequences

- New `AuditArchive` table with `ArchivedAt` column; one EF Core migration.
- `IAuditRepository` gains `ArchiveOlderThanAsync` and `PurgeArchiveOlderThanAsync`.
- `AdminController` gains two new endpoints and an audit log section in `GetStats`.
- `DatabaseTab` gains an "Audit Log" card with archive/purge controls.
- Default `AuditRetentionDays=0` means zero breaking change for existing deployments.
