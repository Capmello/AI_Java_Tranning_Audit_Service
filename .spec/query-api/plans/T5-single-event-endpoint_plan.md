# T5 — Single-event endpoint `GET /api/audit-events/{id}`

**Refs:** [requirements.md AC-1.4](../requirements.md#us-1--compliance-officer-confirms-or-refutes-an-action) · [design.md §2.2, §6.2](../design.md#22-get-apiaudit-eventsid)

**Dependencies:** **T4** (envelope shape + ProblemDetails factory).

## Scope

Add the by-id lookup endpoint that returns the same envelope shape with a single-element `items` array, or 404 with `code = event_not_found`.

**In:**
- New repository method `IAuditEventRepository.GetByIdAsync(Guid id, CancellationToken) → Task<AuditEvent?>`.
- New MediatR query + handler: `GetAuditEventByIdQuery(Guid Id) : IRequest<AuditEventDto?>` and `GetAuditEventByIdHandler`.
- New controller action `GetById` at `[HttpGet("{id:guid}")]`.
  - Returns `200` with `AuditEventsResponse { items: [<one item>], nextCursor: null }`.
  - Returns `404` ProblemDetails with `extensions.code = "event_not_found"` when the row is absent.
- Reuse `AuditEventItemMapper` from T4.

**Out:**
- Bulk delete or update endpoints (read-only API).
- OpenAPI/Scalar annotations (T7).

## Files touched

- `src/AuditLogService.Domain/Repositories/IAuditEventRepository.cs` (add `GetByIdAsync`)
- `src/AuditLogService.Infrastructure/Persistence/Repositories/AuditEventRepository.cs` (impl)
- `src/AuditLogService.Application/Queries/GetAuditEventById/GetAuditEventByIdQuery.cs` (new)
- `src/AuditLogService.Application/Queries/GetAuditEventById/GetAuditEventByIdHandler.cs` (new)
- `src/AuditLogService.Api/Controllers/AuditEventsController.cs` (add `GetById`)
- `src/AuditLogService.Api/Errors/QueryProblemDetails.cs` (add `event_not_found` factory)
- `tests/AuditLogService.Api.Tests/...` (new controller tests)
- `tests/AuditLogService.Application.Tests/...` (handler unit tests)

## Implementation outline

1. Add `GetByIdAsync` using `FirstOrDefaultAsync(e => e.Id == id)` with `.AsNoTracking()`.
2. Add the query/handler — handler returns `null` when not found; controller translates `null` → 404 ProblemDetails.
3. Controller action mirrors the envelope shape exactly to keep client deserialisation uniform.

## Definition of Done

- [ ] `dotnet build` succeeds.
- [ ] `dotnet test` passes.
- [ ] Controller test: `GET /api/audit-events/{existingId}` returns `200` with `{ items: [<one item>], nextCursor: null }`. Item has the nested `actor`, `resource`, `payload` shape from T4 mapper.
- [ ] Controller test: `GET /api/audit-events/{unknownGuid}` returns `404` with `application/problem+json`, `extensions.code = "event_not_found"`.
- [ ] Controller test: `GET /api/audit-events/not-a-guid` returns `400` from the route constraint (no controller code path executed).
- [ ] Item never includes `correlationId`.
- [ ] Handler unit test: returns the existing event mapped to `AuditEventDto`; returns `null` when not present.

## Risks / notes

- Single-element envelope is a deliberate choice (per Round 3 clarification) so clients can use one deserialiser for both list and by-id paths.
