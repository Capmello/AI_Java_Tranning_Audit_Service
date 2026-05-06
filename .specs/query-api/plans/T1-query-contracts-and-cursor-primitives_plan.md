# T1 - Query contracts and cursor primitives

## Goal
Introduce the core query model changes needed for keyset pagination without changing the HTTP surface yet.

## Refs
- Requirements: [requirements.md](../../../.spec/query-api/requirements.md) - AC-1.1, AC-3.1, AC-3.2, AC-3.5, AC-3.6, AC-3.7
- Design: [design.md](../../../.spec/query-api/design.md) - sections 3.3, 3.4, 4.1, 4.2, 10, 13 steps 1, 2, and 5

## Scope
- Add `SortDirection`.
- Add `AuditEventsCursor` and `AuditEventsPage`.
- Rewrite `AuditEventFilter` from page-based pagination to cursor-based pagination.
- Update `GetAuditEventsQuery` and repository contracts to carry `resourceId`, `sort`, and cursor fields.
- Add cursor encoding/decoding and canonical filter-hash helpers in the Application layer.
- Add unit tests for cursor round-trip and hash determinism.

## Dependencies
- Depends on: none
- Unblocks: T2, T3, T4, T6, T7

## Definition of Done
- `AuditEventFilter` no longer exposes page-number pagination and includes `ResourceId`, `Sort`, `CursorTs`, and `CursorId`.
- `SortDirection`, `AuditEventsCursor`, and `AuditEventsPage` exist in the intended layers and are used by the query path contracts.
- A single helper path exists to encode/decode the opaque cursor and to compute the canonical filter hash described in the design.
- Application unit tests prove:
  - cursor encode/decode round-trips without loss,
  - the same filter set always produces the same hash,
  - changing any bound filter or time window changes the hash,
  - canonicalisation is insensitive to property iteration order.
- `dotnet build AuditLogService.slnx` passes.
- `dotnet test AuditLogService.slnx` passes.

## Safe PR boundary
This task is complete when shared contracts and low-level cursor primitives are in place, with no requirement yet to expose the new HTTP contract.
