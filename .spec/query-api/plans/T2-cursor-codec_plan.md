# T2 — Cursor codec & helper types

**Refs:** [requirements.md AC-3.1, AC-3.2, AC-3.3, AC-3.6, AC-3.7](../requirements.md#us-3--security-analyst-paginates-a-large-result-set-without-loss-or-duplication) · [design.md §4](../design.md#4-cursor-design)

**Dependencies:** none — pure utility, no other code touched.

## Scope

Introduce the cursor encoding/decoding utility and the helper types it needs. No repository, handler, or controller wiring.

**In:**
- `SortDirection` enum (`Asc`, `Desc`) in Domain.
- `AuditEventsCursor` record in Application: `(DateTime Ts, Guid Id, SortDirection Sort, string FilterHash)`.
- `AuditEventsCursorCodec` static class: `Encode(cursor) → string`, `TryDecode(raw, out cursor, out errorCode)`.
- Canonical filter string + hash helper: `BuildFilterHash(AuditEventFilter f) → string` returning the first 16 hex chars of SHA-256 over the canonical string from design.md §4.1.
- Unit tests for round-trip, determinism, and tamper detection.

**Out:**
- `AuditEventFilter` shape change (T3 owns it). For T2 the hash helper accepts an interim parameter object or the existing filter — see implementation outline.
- Any wiring into MediatR handlers or controllers.

## Files touched

- `src/AuditLogService.Domain/SortDirection.cs` (new)
- `src/AuditLogService.Application/Querying/AuditEventsCursor.cs` (new)
- `src/AuditLogService.Application/Querying/AuditEventsCursorCodec.cs` (new)
- `src/AuditLogService.Application/Querying/CanonicalFilterString.cs` (new) — pure function for the canonical key string
- `tests/AuditLogService.Application.Tests/Querying/AuditEventsCursorCodecTests.cs` (new)
- `tests/AuditLogService.Application.Tests/Querying/CanonicalFilterStringTests.cs` (new)

## Implementation outline

1. `CanonicalFilterString.Build(actor, action, resource, resourceId, correlationId, fromUtc, toUtc)` returns the deterministic string per design.md §4.1. Accept primitives, **not** the filter record — that decouples T2 from T3's filter rewrite.
2. `AuditEventsCursorCodec.Encode` serialises `AuditEventsCursor` to compact JSON (no whitespace, ISO-8601 UTC for `ts`, lowercase `sort`), then Base64Url.
3. `TryDecode` returns `false` with `errorCode = "cursor_invalid"` on base64/JSON/schema failure.
4. Hash compare is the caller's responsibility (T3) — codec only round-trips the value.

## Definition of Done

- [ ] `dotnet build` succeeds.
- [ ] Unit test: encode → decode round-trip preserves all four fields exactly (parameterised over 10+ cursor values).
- [ ] Unit test: same canonical filter inputs in any property order produce identical hash.
- [ ] Unit test: any single field change in canonical inputs produces a different hash.
- [ ] Unit test: malformed base64 → `TryDecode` returns `false` with `cursor_invalid`.
- [ ] Unit test: valid base64 but malformed JSON → `cursor_invalid`.
- [ ] Unit test: valid JSON but missing required field → `cursor_invalid`.
- [ ] Unit test: cursor JSON contains no whitespace and `sort` is lowercase (snapshot a sample value).
- [ ] No public API surface change observable from the API project.
- [ ] `dotnet test` — all existing tests still pass.

## Risks / notes

- Lock the JSON property names (`ts`, `id`, `sort`, `h`) and the field order via a hand-rolled writer or a `JsonSerializerOptions` with `PropertyNamingPolicy = SnakeCaseLower` only if it produces those exact names — easier to write the JSON directly.
- Treat the codec as security-irrelevant: per design.md §4.3 it is **not** an integrity boundary, only a misuse guard.
