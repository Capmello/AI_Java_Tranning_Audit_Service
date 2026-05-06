# T3 — Keyset pagination contract & repository/handler wiring

**Refs:** [requirements.md AC-1.1, AC-1.2, AC-1.3, AC-2.1, AC-2.2, AC-2.3, AC-2.4, AC-3.1, AC-3.2, AC-3.4, AC-3.5, AC-3.6](../requirements.md) · [design.md §3.4, §6](../design.md#3-data-model)

**Dependencies:** **T1** (indexes — required for the new query to be performant), **T2** (cursor codec).

## Scope

Replace offset pagination with keyset across the Domain, Application, and Infrastructure layers. The HTTP response **stays a flat `IReadOnlyList<AuditEventDto>`** in this task — the envelope reshape is T4's job. Controller absorbs new query parameters but continues to return the flat array.

**In:**
- Rewrite `AuditEventFilter` per design.md §3.4 (drop `Page`, add `ResourceId`, `Sort`, `CursorTs`, `CursorId`; `FromUtc`/`ToUtc` become non-nullable required).
- New `AuditEventsPage` record in Domain: `(IReadOnlyList<AuditEvent> Events, DateTime? NextCursorTs, Guid? NextCursorId)`.
- Change `IAuditEventRepository.QueryAsync` to return `AuditEventsPage`.
- Reimplement `AuditEventRepository.QueryAsync` with the keyset query from design.md §6.1 (`Take(pageSize+1)` strategy).
- Rewrite `GetAuditEventsQuery` (drop `Page`, add `Cursor`, `Sort`, `ResourceId`) and `GetAuditEventsHandler`:
  - Decode `Cursor` via T2 codec; validate filter-hash equality → `cursor_mismatch`.
  - Validate window required + 90-day cap + `pageSize ∈ [1,100]` + `sort` whitelist + `resourceId⇒resource`.
  - Build new cursor from `AuditEventsPage` boundary using current filter hash.
  - Return `(items: IReadOnlyList<AuditEventDto>, nextCursor: string?)` via a small internal result record.
- Update controller `Query` action: bind `cursor` (string), `sort` (string), `resourceId` (string), drop `page`. Map handler result back to flat list response (envelope reshape deferred to T4) — but include `nextCursor` as an HTTP response header `X-Next-Cursor` so callers can paginate before T4 lands.
- Update existing tests touching `AuditEventFilter` / `GetAuditEventsQuery` shape.

**Out:**
- Envelope response body (T4).
- Item mapper to nested API shape (T4).
- ProblemDetails `code` extensions wiring (T4 — for now, validation errors return plain 400).
- Single-event endpoint (T5).

## Files touched

- `src/AuditLogService.Domain/Repositories/AuditEventFilter.cs` (rewrite)
- `src/AuditLogService.Domain/Repositories/IAuditEventRepository.cs` (signature)
- `src/AuditLogService.Domain/Repositories/AuditEventsPage.cs` (new)
- `src/AuditLogService.Infrastructure/Persistence/Repositories/AuditEventRepository.cs` (rewrite `QueryAsync`)
- `src/AuditLogService.Application/Queries/GetAuditEvents/GetAuditEventsQuery.cs` (rewrite)
- `src/AuditLogService.Application/Queries/GetAuditEvents/GetAuditEventsHandler.cs` (rewrite)
- `src/AuditLogService.Application/Queries/GetAuditEvents/GetAuditEventsResult.cs` (new — internal `(items, nextCursor)`)
- `src/AuditLogService.Api/Controllers/AuditEventsController.cs` (rebind `Query` parameters; emit `X-Next-Cursor` header)
- `tests/AuditLogService.Application.Tests/...` — update existing handler/repo tests to the new contract (no new keyset-semantic tests yet — those are T6).

## Implementation outline

1. Rewrite filter + page records + repository signature → solution will not build until repository impl is updated.
2. Implement repository keyset query exactly as design.md §6.1.
3. Rewrite handler: validate → decode cursor → call repo → map to DTOs → encode next cursor.
4. Adjust controller binding; `cursor`/`sort`/`resourceId` are simple `[FromQuery] string?`.
5. Sweep existing unit tests for the changed signatures; update them mechanically to use `Sort = SortDirection.Desc` and explicit window bounds.

## Definition of Done

- [ ] `dotnet build` succeeds across all projects.
- [ ] `dotnet test` — full suite passes.
- [ ] Unit test (Application): handler with no cursor returns flat list and a non-null `X-Next-Cursor` when more pages exist; null when exhausted.
- [ ] Unit test (Application): handler with a cursor whose `h` differs from the recomputed hash returns a `cursor_mismatch` failure (assert exception type or result enum, whichever the implementation uses).
- [ ] Unit test (Application): missing `fromUtc`/`toUtc` returns `window_required`; window > 90 d returns `window_too_large`.
- [ ] Unit test (Infrastructure / Repository): seeded 30 events, `pageSize = 10`, walk via `nextCursor` until null → all 30 distinct ids returned exactly once. Runs against real Postgres (Testcontainers) — keep in T3 as the minimum keyset proof; deeper concurrency tests come in T6.
- [ ] Controller `GET /api/audit-events?fromUtc=…&toUtc=…&pageSize=2` returns 200 with a flat JSON array and an `X-Next-Cursor` header on the first page.
- [ ] Removing `?page=` and adding `?cursor=…&sort=asc` works end-to-end.
- [ ] No new public API contract beyond what is described above; envelope/error-code reshape remains for T4.

## Risks / notes

- This is the largest task. Land T1 + T2 first; review T3 with focus on the keyset `WHERE` clause and the `Take(pageSize+1)` boundary.
- The interim `X-Next-Cursor` header is throwaway — T4 removes it when the envelope ships. Document this in the PR description.
