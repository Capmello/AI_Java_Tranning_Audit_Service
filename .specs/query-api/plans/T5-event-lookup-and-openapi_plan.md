# T5 - Event lookup and OpenAPI documentation

## Goal
Add the single-event lookup endpoint and finish the public API documentation for the Query API.

## Refs
- Requirements: [requirements.md](../../../.spec/query-api/requirements.md) - AC-1.4, AC-1.5, AC-1.6
- Design: [design.md](../../../.spec/query-api/design.md) - sections 2.2, 2.3, 6.2, 6.3, 9, 10, 11 E11, 12.2, 13 step 6, step 9, and step 10

## Scope
- Add `GetAuditEventByIdQuery` and handler.
- Add `GET /api/audit-events/{id}` returning the single-item envelope shape.
- Return `404` with `extensions.code = "event_not_found"` when the id does not exist.
- Add XML docs, response annotations, and Scalar/OpenAPI wiring for both query endpoints.
- Add integration tests for the new endpoint and response codes.

## Dependencies
- Depends on: T4
- Unblocks: T7

## Definition of Done
- `GET /api/audit-events/{id}` exists with a `{id:guid}` route and returns the same item shape used by the collection endpoint inside a single-item envelope.
- Unknown ids return RFC 7807 `ProblemDetails` with `extensions.code = "event_not_found"`.
- Existing write endpoints remain unchanged.
- Generated OpenAPI metadata documents:
  - both GET endpoints,
  - 200, 400, and 404 response types,
  - the documented machine-readable error codes.
- API integration tests cover:
  - found event returns a one-item envelope,
  - unknown id returns `404 event_not_found`.
- `dotnet build AuditLogService.slnx` passes.
- `dotnet test AuditLogService.slnx` passes.

## Safe PR boundary
This task is intentionally limited to by-id lookup and documentation. Large pagination walkthroughs and performance verification stay for later PRs.
