# T2 - Query indexes and repository keyset pagination

## Goal
Move persistence from offset paging to deterministic keyset paging and add the indexes needed to keep the read path fast.

## Refs
- Requirements: [requirements.md](../../../.spec/query-api/requirements.md) - AC-1.3, AC-2.1, AC-2.2, AC-2.3, AC-3.1, AC-3.3, AC-3.4
- Design: [design.md](../../../.spec/query-api/design.md) - sections 5.1, 5.2, 5.3, 6.1, 11 E1, E4, E9, E10, 12.3, 13 step 3 and step 4

## Scope
- Add the `AddAuditEventQueryIndexes` migration and update the EF snapshot.
- Rewrite `AuditEventRepository.QueryAsync` to:
  - enforce the time window,
  - apply exact-match filters,
  - apply the keyset anchor over `(Timestamp, Id)`,
  - support both ascending and descending order,
  - fetch `pageSize + 1` rows to detect `nextCursor`.
- Update repository tests to cover the new query behavior.

## Dependencies
- Depends on: T1
- Unblocks: T3, T4, T6, T7

## Definition of Done
- A new EF Core migration adds the four query indexes defined in the design, and the model snapshot reflects them.
- `AuditEventRepository.QueryAsync` returns an `AuditEventsPage` and does not use offset or page-number logic.
- Repository behavior is covered by infrastructure tests for:
  - descending order with `Timestamp` then `Id` tie-break,
  - ascending order with the same tie-break,
  - `resource` and `resourceId` exact-match filtering,
  - `correlationId` exact-match filtering,
  - no empty trailing page when the last page size equals `pageSize`.
- Existing append-only write behavior remains unchanged.
- `dotnet build AuditLogService.slnx` passes.
- `dotnet test AuditLogService.slnx` passes.

## Safe PR boundary
This task stops at repository and migration behavior. Controller contracts, ProblemDetails mapping, and end-to-end pagination walkthroughs stay out of this PR.
