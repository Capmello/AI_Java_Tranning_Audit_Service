# T1 — Database migration: query indexes

**Refs:** [requirements.md AC-2.5](../requirements.md#us-2--sre-reconstructs-a-resource-timeline) · [design.md §5](../design.md#5-database--indexes)

**Dependencies:** none — foundation task.

## Scope

Add four B-tree indexes on `audit_events` to support keyset pagination and the documented filter set.

**In:**
- New EF Core migration `AddAuditEventQueryIndexes` adding the four indexes from design.md §5.1.
- Update `AuditDbContextModelSnapshot` accordingly (auto-generated).

**Out:**
- No code changes to repository, handler, controller, or DTOs.
- No query rewrite — repository continues to use offset pagination; the indexes serve the existing query plan plus future keyset queries from T3.

## Files touched

- `src/AuditLogService.Infrastructure/Migrations/<timestamp>_AddAuditEventQueryIndexes.cs` (new)
- `src/AuditLogService.Infrastructure/Migrations/<timestamp>_AddAuditEventQueryIndexes.Designer.cs` (new)
- `src/AuditLogService.Infrastructure/Migrations/AuditDbContextModelSnapshot.cs` (regenerated)
- `src/AuditLogService.Infrastructure/Persistence/Configurations/AuditEventConfiguration.cs` (add `HasIndex(...)` calls so the model is the source of truth)

## Implementation outline

1. Add the four `HasIndex(...)` declarations to `AuditEventConfiguration` with descending order on `Timestamp` and `Id`.
2. Run `dotnet ef migrations add AddAuditEventQueryIndexes --project src/AuditLogService.Infrastructure --startup-project src/AuditLogService.Api`.
3. Inspect generated SQL — must produce exactly four `CREATE INDEX` statements with the names from design.md §5.1.

## Definition of Done

- [ ] `dotnet build` succeeds (solution-wide).
- [ ] `dotnet ef migrations script <prev> AddAuditEventQueryIndexes` outputs four `CREATE INDEX` statements with the documented names and column lists.
- [ ] Running the API once against a clean DB applies the migration; `\d audit_events` (or `pg_indexes` query) shows all four new indexes.
- [ ] `dotnet test` — every existing test passes unmodified.
- [ ] Repository test: an `EXPLAIN (FORMAT JSON)` query filtered by `Resource + ResourceId` selects `ix_audit_events_resource_resourceid_timestamp_id` (string-match assertion against the plan).
- [ ] No changes to public API contracts.

## Risks / notes

- EF migrations wrap in a transaction, so `CREATE INDEX CONCURRENTLY` is **not** used. The audit_events table is small in dev/test; if production data already exists at scale, a follow-up SQL-only migration with `CONCURRENTLY` may be needed (out of scope here).
