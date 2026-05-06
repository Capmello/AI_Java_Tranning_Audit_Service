# T4 - Query endpoint HTTP contract

## Goal
Expose the new collection query API contract on `GET /api/audit-events` with the nested response shape and explicit validation behavior.

## Refs
- Requirements: [requirements.md](../../../.spec/query-api/requirements.md) - AC-1.1, AC-1.2, AC-1.5, AC-1.6, AC-2.2, AC-2.4, AC-3.3, AC-3.5
- Design: [design.md](../../../.spec/query-api/design.md) - sections 2.1, 2.3, 3.3, 6.3, 7, 10, 11 E5, E6, E8, E12, 12.2

## Scope
- Add API response models: `AuditEventItem`, `ActorRef`, `ResourceRef`, and `AuditEventsResponse`.
- Add `AuditEventItemMapper`.
- Rewrite `AuditEventsController.Query` to bind:
  - `actor`,
  - `action`,
  - `resource`,
  - `resourceId`,
  - `correlationId`,
  - `fromUtc`,
  - `toUtc`,
  - `pageSize`,
  - `cursor`,
  - `sort`.
- Return RFC 7807 `ProblemDetails` with the documented `extensions.code` values for request validation failures.
- Update API integration tests for the new envelope shape and validation outcomes.

## Dependencies
- Depends on: T1, T2, T3
- Unblocks: T5, T6, T7

## Definition of Done
- `GET /api/audit-events` no longer returns a flat `List<AuditEventDto>` and instead returns `{ items, nextCursor }`.
- Each response item contains only `id`, `occurredAt`, `actor`, `resource`, `action`, and `payload`.
- `correlationId` remains filter-only and is not present in response items.
- The controller returns `400` with the correct machine-readable code for:
  - missing `fromUtc` or `toUtc`,
  - invalid time window order,
  - time window larger than 90 days,
  - invalid `pageSize`,
  - invalid `sort`,
  - `resourceId` without `resource`.
- API integration tests cover:
  - happy-path envelope response,
  - empty envelope response,
  - validation failures listed above,
  - correlation-id filtering without correlation-id projection in response items.
- `dotnet build AuditLogService.slnx` passes.
- `dotnet test AuditLogService.slnx` passes.

## Safe PR boundary
This PR is only for the collection endpoint contract. Single-event lookup and API documentation stay separate.
