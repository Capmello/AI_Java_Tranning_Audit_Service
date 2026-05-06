# T3 - Query handler and cursor flow

## Goal
Implement the application-layer orchestration for cursor validation, repository invocation, next-cursor generation, and structured logging.

## Refs
- Requirements: [requirements.md](../../../.spec/query-api/requirements.md) - AC-2.6, AC-3.2, AC-3.3, AC-3.6, AC-3.7
- Design: [design.md](../../../.spec/query-api/design.md) - sections 4.1, 4.2, 6.2, 7 step 7, 8, 10, 12.1, 13 step 5 and step 6

## Scope
- Rewrite `GetAuditEventsHandler` around the keyset repository result.
- Decode and validate the cursor in the handler.
- Recompute and compare the filter hash and requested sort.
- Build `nextCursor` from the returned page boundary.
- Add structured logging for query shape, result count, and cursor diagnostics without logging raw PII values.
- Add or update application tests for the handler.

## Dependencies
- Depends on: T1, T2
- Unblocks: T4, T6, T7

## Definition of Done
- `GetAuditEventsHandler` rejects malformed cursors as `cursor_invalid`.
- `GetAuditEventsHandler` rejects reused cursors with changed filters or changed sort as `cursor_mismatch`.
- A successful handler call returns both the event DTOs and the next cursor information required by the controller layer.
- Information-level logs include filter shape, window, page size, sort, cursor presence, result count, and duration.
- Debug logs cover cursor decode and hash comparison outcomes.
- Application tests prove:
  - mismatch detection when a filter changes between pages,
  - mismatch detection when sort changes between pages,
  - next-cursor generation from the last returned row,
  - logging does not emit raw `actor` or `correlationId` values in production-safe paths.
- `dotnet build AuditLogService.slnx` passes.
- `dotnet test AuditLogService.slnx` passes.

## Safe PR boundary
This PR ends at application behavior. HTTP query parameter binding, ProblemDetails payloads, and OpenAPI annotations are handled later.
