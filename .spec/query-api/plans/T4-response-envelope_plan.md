# T4 — Envelope response, item mapper, ProblemDetails error codes

**Refs:** [requirements.md AC-1.5, AC-1.6, AC-3.3](../requirements.md) · [design.md §2.1, §2.3, §3.3, §7](../design.md#2-api-contracts)

**Dependencies:** **T3** (handler returns the data shape this task wraps).

## Scope

Reshape the HTTP response from a flat array into the documented envelope, project items into the nested API shape, and route every validation failure through ProblemDetails with a machine-readable `code` extension.

**In:**
- New API DTOs: `AuditEventItem`, `ActorRef`, `ResourceRef`, `AuditEventsResponse` (in `src/AuditLogService.Api/Contracts/`).
- New mapper: `AuditEventItemMapper` static class in `src/AuditLogService.Api/Mapping/`.
  - `actor.id ← AuditEvent.Actor`; `actor.type ← Metadata["actor.type"] ?? "unknown"`.
  - `resource.id ← ResourceId ?? Resource`; `resource.type ← Resource`.
  - `payload ← Metadata.Where(k != "actor.type")`.
  - `correlationId` excluded from the item.
- Controller `Query` returns `AuditEventsResponse { items, nextCursor }`. Drop the `X-Next-Cursor` header from T3.
- Add `ProblemDetailsFactory`-style helper that builds ProblemDetails with `extensions["code"] = <kebab>` for every code in design.md §2.3 (excluding `event_not_found`, which lands in T5).
- Wire validation failures from T3's handler / controller binding into the helper. ASP.NET model-binding errors continue to use stock ProblemDetails — only add the `code` extension.
- camelCase confirmed in `AddControllers().AddJsonOptions(...)` if not already the default.

**Out:**
- Single-event endpoint and its 404 (T5).
- OpenAPI/Scalar annotations & examples (T7).
- Concurrent-write keyset tests, latency SLO tests (T6).

## Files touched

- `src/AuditLogService.Api/Contracts/AuditEventItem.cs` (new)
- `src/AuditLogService.Api/Contracts/ActorRef.cs` (new)
- `src/AuditLogService.Api/Contracts/ResourceRef.cs` (new)
- `src/AuditLogService.Api/Contracts/AuditEventsResponse.cs` (new)
- `src/AuditLogService.Api/Mapping/AuditEventItemMapper.cs` (new)
- `src/AuditLogService.Api/Errors/QueryProblemDetails.cs` (new — code → ProblemDetails factory)
- `src/AuditLogService.Api/Controllers/AuditEventsController.cs` (response shape + ProblemDetails)
- `src/AuditLogService.Api/Program.cs` (verify camelCase JSON option)
- `tests/AuditLogService.Api.Tests/...` — update existing controller tests to the envelope and add per-code 400 tests.

## Implementation outline

1. Add API DTOs and mapper; unit-test the mapper in isolation (no HTTP).
2. Replace `return Ok(items)` in the controller with `return Ok(new AuditEventsResponse(items.Select(map).ToList(), nextCursor))`.
3. Wire each validation branch from T3 to call the new ProblemDetails helper.
4. Remove the `X-Next-Cursor` interim header.
5. Update existing controller tests: assert envelope shape; assert each `code` value for the documented 400 cases.

## Definition of Done

- [ ] `dotnet build` succeeds.
- [ ] `dotnet test` passes.
- [ ] Controller test: happy-path `GET /api/audit-events?…` returns `200` with body `{ items: [...], nextCursor: null|string }`.
- [ ] Controller test: each of `window_required`, `window_too_large`, `window_invalid`, `page_size_invalid`, `sort_invalid`, `cursor_invalid`, `cursor_mismatch`, `missing_resource` returns `400` with `application/problem+json` and `extensions.code` matching the table in design.md §2.3.
- [ ] Mapper unit test: `correlationId` is **never** present in the item. `actor.type` uses `"unknown"` when metadata key absent. `resource.id` falls back to `resource` when `resourceId` is null.
- [ ] Empty-result test: filter that matches zero rows returns `200` with `items: []` and `nextCursor: null`.
- [ ] JSON casing test: serialised body uses `occurredAt`, `nextCursor`, `resourceId` (camelCase), not Pascal.
- [ ] No `X-Next-Cursor` header in the response (interim header removed).

## Risks / notes

- ASP.NET's stock 400 from `[ApiController]` model binding emits ProblemDetails without our `code` extension. Either (a) intercept via `ConfigureApiBehaviorOptions.InvalidModelStateResponseFactory` or (b) move binding-driven validation into manual checks inside the action. (a) is preferred; document the choice in the PR.
