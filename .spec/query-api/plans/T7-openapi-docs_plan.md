# T7 — Scalar / OpenAPI documentation

**Refs:** [requirements.md (all user stories — discoverability)](../requirements.md) · [design.md §9](../design.md#9-openapi-documentation-scalar)

**Dependencies:** **T5** (both endpoints exist with their final shapes and error codes).

## Scope

Make the Query API self-documenting via the already-installed Scalar UI by adding full OpenAPI annotations, request/response examples, and machine-readable error documentation.

**In:**
- Enable XML documentation generation for `AuditLogService.Api.csproj` (`<GenerateDocumentationFile>true</GenerateDocumentationFile>`, suppress CS1591 if the project has untyped files).
- Reference the generated XML file in `AddOpenApi()` configuration.
- XML doc comments on:
  - `AuditEventsController.Query` — purpose, every parameter, every response code with its `code` extension value.
  - `AuditEventsController.GetById` — purpose, parameter, 200, 404 with `event_not_found`.
  - `AuditEventsResponse`, `AuditEventItem`, `ActorRef`, `ResourceRef` — every property.
- `[ProducesResponseType<AuditEventsResponse>(StatusCodes.Status200OK)]` and `[ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]` (and `404` on `GetById`).
- Static example providers / `OpenApiOperationTransformer` (depending on the .NET 10 OpenAPI API surface) supplying:
  - One example query URL.
  - One example 200 envelope body.
  - One example 400 ProblemDetails body for each `code` from design.md §2.3.

**Out:**
- Generating client SDKs from the spec.
- Auth annotations (auth is out of scope per requirements §Out of Scope).

## Files touched

- `src/AuditLogService.Api/AuditLogService.Api.csproj` (XML doc generation flag)
- `src/AuditLogService.Api/Program.cs` (wire XML file into `AddOpenApi`)
- `src/AuditLogService.Api/Controllers/AuditEventsController.cs` (XML docs + `[ProducesResponseType]`)
- `src/AuditLogService.Api/Contracts/*.cs` (XML docs on each property)
- `src/AuditLogService.Api/OpenApi/QueryExamplesTransformer.cs` (new — operation transformer that injects examples)
- `tests/AuditLogService.Api.Tests/OpenApi/OpenApiSpecTests.cs` (new) — fetches the generated spec and asserts presence of error codes + examples.

## Implementation outline

1. Turn on XML doc generation; fix any CS1591 warnings on touched types only (don't sweep the whole codebase).
2. Add doc comments and `[ProducesResponseType]` attributes.
3. Implement an `IOpenApiDocumentTransformer` (.NET 10) or operation transformer that adds examples per operation.
4. Verify locally by running `dotnet run` and opening `/scalar` — confirm:
   - Both endpoints listed.
   - Every parameter described.
   - 400 / 404 panels list each `code` value.
   - Examples render for request and responses.

## Definition of Done

- [ ] `dotnet build` succeeds with no new warnings on the touched files.
- [ ] `dotnet test` passes.
- [ ] Test (`OpenApiSpecTests`): the generated OpenAPI document at runtime contains operations for both `/api/audit-events` and `/api/audit-events/{id}`.
- [ ] Test: each operation lists the expected response status codes (`200`, `400`, plus `404` for by-id).
- [ ] Test: the OpenAPI spec exposes a string occurrence of every `code` value from design.md §2.3 (`window_required`, `window_too_large`, `window_invalid`, `page_size_invalid`, `sort_invalid`, `cursor_invalid`, `cursor_mismatch`, `missing_resource`, `event_not_found`) — either in a description or example body.
- [ ] Manual: Scalar UI at `/scalar` renders both endpoints with examples and parameter descriptions.
- [ ] No runtime behaviour changes — only docs and metadata.

## Risks / notes

- .NET 10's built-in OpenAPI surface (`Microsoft.AspNetCore.OpenApi`) is the source the Scalar middleware consumes; do **not** add Swashbuckle alongside it.
- Keep examples short — one canonical good and one per error code. The OpenAPI document will be served on every request to Scalar; bloated examples slow page rendering.
