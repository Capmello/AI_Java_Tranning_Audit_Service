# Query API — Design

This document specifies **how** to implement the Query API described in [requirements.md](./requirements.md). It covers contracts, data model, cursor mechanics, persistence, edge cases, integrations, and tests.

---

## 1. Overview

The Query API adds two read endpoints on the existing `AuditEventsController`:

| Method | Route | Purpose | Story |
|---|---|---|---|
| GET | `/api/audit-events` | Paginated keyset query over audit events | US-1, US-2, US-3 |
| GET | `/api/audit-events/{id}` | Single event lookup | US-1 |

Both operate on the existing `AuditEvent` entity. The internal `AuditEventDto` stays flat; the controller maps to a nested HTTP shape via a dedicated mapper. Pagination is keyset over `(Timestamp DESC|ASC, Id DESC|ASC)` with an opaque cursor that carries a hash of the bound filter set so the server can detect AC-3.6 mismatches.

---

## 2. API Contracts

### 2.1 GET /api/audit-events

**Query parameters** (all camelCase on the wire):

| Name | Type | Required | Notes |
|---|---|---|---|
| `actor` | string | no | exact match, max 256 chars |
| `action` | string | no | exact match, max 256 chars |
| `resource` | string | no | exact match, max 256 chars |
| `resourceId` | string | no | exact match, max 256 chars; if set without `resource` → `400 missing_resource` |
| `correlationId` | string | no | exact match, max 128 chars; filter-only — not echoed in items |
| `fromUtc` | ISO-8601 UTC | **yes** | inclusive lower bound |
| `toUtc` | ISO-8601 UTC | **yes** | inclusive upper bound; must be ≥ `fromUtc`; window ≤ 90 d |
| `pageSize` | int | no | default `25`, range `[1, 100]` |
| `cursor` | string | no | opaque, returned by previous response |
| `sort` | enum | no | `asc` \| `desc`, default `desc` |

**200 response body**:
```json
{
  "items": [
    {
      "id": "5d7f8b6e-…",
      "occurredAt": "2026-04-17T11:02:14Z",
      "actor":    { "id": "u_42",         "type": "user" },
      "resource": { "id": "order/9f3b…",  "type": "order" },
      "action": "order.refunded",
      "payload": { "reason": "duplicate" }
    }
  ],
  "nextCursor": "eyJ0cyI6IjIwMjYtMDQtMTdUMTE6MDI6MTRaIiwi…"
}
```
- `nextCursor` is `null` when the result has no further pages.
- Empty results → `items: []`, `nextCursor: null` (AC-1.6).

**Item-shape mapping rules**:
- `actor.id` ← `AuditEvent.Actor`; `actor.type` ← `payload["actor.type"]` if present, else `"unknown"`.
- `resource.id` ← `AuditEvent.ResourceId` (falls back to `Resource` if `ResourceId` null); `resource.type` ← `AuditEvent.Resource`.
- `payload` ← `Metadata` minus the reserved key `"actor.type"`.
- `correlationId` is **not** projected (filter-only, AC-1.5).

### 2.2 GET /api/audit-events/{id}

**Path parameter**: `id` — `Guid` of an audit event.

**200 response body** (envelope with single-element items, per the design decision):
```json
{
  "items": [ { /* nested item, same shape as 2.1 */ } ],
  "nextCursor": null
}
```
**404 response**: standard ProblemDetails with `extensions.code = "event_not_found"`.

### 2.3 Error model — RFC 7807 ProblemDetails

ASP.NET `ProblemDetails` is already registered in `Program.cs:25`. All 4xx responses use it. Machine-readable codes go into the `extensions["code"]` field:

| HTTP | code | Trigger |
|---|---|---|
| 400 | `window_required` | `fromUtc` or `toUtc` missing |
| 400 | `window_too_large` | `toUtc - fromUtc > 90 days` |
| 400 | `window_invalid` | `toUtc < fromUtc` |
| 400 | `page_size_invalid` | `pageSize` outside `[1,100]` |
| 400 | `sort_invalid` | `sort` not in `{asc,desc}` |
| 400 | `cursor_invalid` | base64 / JSON decode failure or schema mismatch |
| 400 | `cursor_mismatch` | filter-hash in cursor ≠ server-recomputed hash (AC-3.6) |
| 400 | `missing_resource` | `resourceId` set without `resource` |
| 404 | `event_not_found` | `{id}` not present |

---

## 3. Data Model

### 3.1 Domain entity (unchanged)

`src/AuditLogService.Domain/Entities/AuditEvent.cs` — no schema change. Append-only invariant from prior work is preserved.

### 3.2 Internal DTO (unchanged)

`src/AuditLogService.Application/DTOs/AuditEventDto.cs` — flat record. Reused by both write and read paths.

### 3.3 New types

| Type | Project | Purpose |
|---|---|---|
| `AuditEventItem` (record) | API | Nested HTTP item: `Id`, `OccurredAt`, `ActorRef`, `ResourceRef`, `Action`, `Payload` |
| `ActorRef` (record) | API | `{ Id, Type }` |
| `ResourceRef` (record) | API | `{ Id, Type }` |
| `AuditEventsResponse` (record) | API | `{ Items, NextCursor }` envelope |
| `AuditEventsCursor` (record) | Application | `{ Ts, Id, Sort, FilterHash }` — internal, never serialized |
| `AuditEventsPage` (record) | Domain | `{ Events, NextCursorTs, NextCursorId }` returned by repository |
| `AuditEventItemMapper` (static class) | API (`/Mapping`) | `AuditEventDto → AuditEventItem` projection |

### 3.4 Filter record (replaces offset)

`AuditEventFilter` is rewritten:

```csharp
public sealed record AuditEventFilter(
    string? Actor,
    string? Action,
    string? Resource,
    string? ResourceId,
    string? CorrelationId,
    DateTime FromUtc,        // required
    DateTime ToUtc,          // required
    int PageSize,
    SortDirection Sort,      // new enum: Asc | Desc
    DateTime? CursorTs,      // null on first page
    Guid? CursorId);         // null on first page
```

`Page` is removed.

---

## 4. Cursor Design

### 4.1 Encoding

Cursor = `Base64Url( JSON({ ts, id, sort, h }) )` where:
- `ts` — RFC 3339 UTC timestamp of the last item on the page
- `id` — `Guid` of the last item on the page
- `sort` — `"asc"` or `"desc"`
- `h` — first 16 hex chars of `SHA256(canonicalFilterString)`

**Canonical filter string** (deterministic; null → empty):
```
actor=<v>;action=<v>;resource=<v>;resourceId=<v>;correlationId=<v>;from=<isoUtc>;to=<isoUtc>
```
Lower-cased keys, semicolon-separated, values URL-unescaped, no whitespace.

### 4.2 Integrity & mismatch detection (AC-3.6 strict)

On every paginated request with a `cursor` present:
1. Base64-decode → JSON-deserialize → validate schema. Failure → `400 cursor_invalid`.
2. Recompute `h_server` from the current request's filters + window.
3. If `h_server != cursor.h` or `cursor.sort != request.sort` → `400 cursor_mismatch`.
4. Otherwise use `cursor.ts`/`cursor.id` as the keyset anchor and proceed.

### 4.3 No TTL, no signing

Per Round 1 decision, cursor is plain base64(JSON) with a hash field — no HMAC, no expiration. The hash protects against accidental reuse with mutated filters; it is **not** a security boundary against tampering. This is acceptable because (a) the endpoint is open per requirements and (b) all reachable rows are already determined by the filter set, so a tampered cursor at worst yields a different valid window of public data.

---

## 5. Database & Indexes

### 5.1 New migration: `AddAuditEventQueryIndexes`

`src/AuditLogService.Infrastructure/Migrations/<timestamp>_AddAuditEventQueryIndexes.cs` adds four B-tree indexes on `audit_events`:

| Index name | Columns |
|---|---|
| `ix_audit_events_timestamp_id` | `(timestamp DESC, id DESC)` |
| `ix_audit_events_resource_resourceid_timestamp_id` | `(resource, resource_id, timestamp DESC, id DESC)` |
| `ix_audit_events_actor_timestamp_id` | `(actor, timestamp DESC, id DESC)` |
| `ix_audit_events_correlationid_timestamp_id` | `(correlation_id, timestamp DESC, id DESC)` |

DESC is the dominant ordering; PostgreSQL also serves ASC scans efficiently from a DESC composite index when the planner reverses traversal.

### 5.2 Index selection

The query planner picks the index based on the most selective filter. The `(timestamp,id)` index is the fallback when no filter except the time window is supplied; the other three are tuned for US-1/US-2/AC-2.4 hot paths.

### 5.3 Migration rules

- Generated and applied via EF Core (`Program.cs:42` already calls `MigrateAsync` on startup).
- No data backfill needed — append-only table; index build is online (`CREATE INDEX CONCURRENTLY` is **not** used here because EF migrations wrap in a transaction; if the table grows large, switch to a SQL-only migration with `CONCURRENTLY` outside a tx).

---

## 6. Query Implementation

### 6.1 Repository (`AuditEventRepository.QueryAsync`)

Rewrite to:

```csharp
public async Task<AuditEventsPage> QueryAsync(AuditEventFilter f, CancellationToken ct)
{
    var q = db.AuditEvents.AsNoTracking()
        .Where(e => e.Timestamp >= f.FromUtc && e.Timestamp <= f.ToUtc);

    if (f.Actor         is not null) q = q.Where(e => e.Actor == f.Actor);
    if (f.Action        is not null) q = q.Where(e => e.Action == f.Action);
    if (f.Resource      is not null) q = q.Where(e => e.Resource == f.Resource);
    if (f.ResourceId    is not null) q = q.Where(e => e.ResourceId == f.ResourceId);
    if (f.CorrelationId is not null) q = q.Where(e => e.CorrelationId == f.CorrelationId);

    // Keyset anchor
    if (f.CursorTs is not null && f.CursorId is not null)
    {
        var ts = f.CursorTs.Value; var id = f.CursorId.Value;
        q = f.Sort == SortDirection.Desc
            ? q.Where(e => e.Timestamp <  ts || (e.Timestamp == ts && e.Id < id))
            : q.Where(e => e.Timestamp >  ts || (e.Timestamp == ts && e.Id > id));
    }

    q = f.Sort == SortDirection.Desc
        ? q.OrderByDescending(e => e.Timestamp).ThenByDescending(e => e.Id)
        : q.OrderBy(e => e.Timestamp).ThenBy(e => e.Id);

    // Take N+1 to detect more pages without an extra COUNT
    var rows = await q.Take(f.PageSize + 1).ToListAsync(ct);

    var hasMore = rows.Count > f.PageSize;
    if (hasMore) rows.RemoveAt(rows.Count - 1);

    return new AuditEventsPage(
        Events: rows,
        NextCursorTs: hasMore ? rows[^1].Timestamp : null,
        NextCursorId: hasMore ? rows[^1].Id : null);
}
```

Key choices:
- `.AsNoTracking()` — read-only path.
- `Take(pageSize + 1)` — avoids a separate `COUNT(*)` to determine `nextCursor`.
- All predicates compose into a single SQL query; EF translates the disjunctive keyset clause to a parameterized `WHERE`.

### 6.2 Application handler

`GetAuditEventsHandler` becomes responsible for:
1. Parsing/validating the cursor (base64+JSON+hash check).
2. Recomputing the canonical filter hash and comparing.
3. Calling the repository.
4. Building `nextCursor` from `AuditEventsPage` + filter hash.
5. Mapping `AuditEvent` → `AuditEventDto` (existing logic).

`GetAuditEventByIdHandler` (new) — direct `FirstOrDefaultAsync(e => e.Id == id)`; returns `null` → controller emits 404.

### 6.3 Controller layer

`AuditEventsController.Query` rewritten to:
- Bind new query parameters (drop `page`, add `cursor`, `sort`, `resourceId`).
- Send `GetAuditEventsQuery` via MediatR.
- Map `IReadOnlyList<AuditEventDto>` → `AuditEventsResponse` via `AuditEventItemMapper`.

`AuditEventsController.GetById` (new) — `[HttpGet("{id:guid}")]`, returns the envelope-with-one-item shape per the design decision.

---

## 7. Validation & Error Handling

Validation order (return on first failure, all 400 with ProblemDetails + `code` extension):

1. `fromUtc`, `toUtc` present and parseable → else `window_required`.
2. `toUtc >= fromUtc` → else `window_invalid`.
3. `toUtc - fromUtc <= 90 days` → else `window_too_large`.
4. `pageSize ∈ [1,100]` → else `page_size_invalid`.
5. `sort ∈ {asc,desc}` (case-insensitive) → else `sort_invalid`.
6. `resourceId` set ⇒ `resource` set → else `missing_resource`.
7. If `cursor` present: decode, validate schema → else `cursor_invalid`; then hash-compare → else `cursor_mismatch`.

Validation lives in the controller (DataAnnotations + small custom checks) plus the handler (cursor decoding/hash). ASP.NET's automatic ModelState → ProblemDetails handling covers (1)–(6) when annotations are sufficient; (7) is hand-rolled in the handler since the cursor is opaque.

---

## 8. Logging & Observability

Per AC-2.6, `GetAuditEventsHandler` logs at:

- **Information** on each query: `Filter shape (actor/action/resource/resourceId/correlationId masked-or-bool), window, pageSize, sort, hasCursor, resultCount, durationMs`. **No raw filter values** for `actor` / `correlationId` in production logs (PII risk per AGENTS.md).
- **Debug** on cursor decode/hash success and miss.
- **Error** on unhandled exceptions; `ProblemDetails` already strips stack traces in non-Development per `Program.cs:46-48`.

Correlation id flows from `CorrelationIdMiddleware` (existing) into `LogContext`.

---

## 9. OpenAPI Documentation (Scalar)

Scalar is already mounted at `/scalar` via `MapScalarApiReference()` in `Program.cs:55`. Add full annotations on the controller actions:

- XML doc comments on action methods, parameters, and DTO properties — enable `<GenerateDocumentationFile>true</GenerateDocumentationFile>` in `AuditLogService.Api.csproj` if not already on, and reference the XML file in `AddOpenApi()`.
- `[ProducesResponseType<AuditEventsResponse>(200)]`, `[ProducesResponseType<ProblemDetails>(400)]`, `[ProducesResponseType<ProblemDetails>(404)]`.
- Examples: provide `IExampleProvider`-style static examples for request URLs and the 200/400 bodies (Scalar surfaces these via the OpenAPI spec).
- Document each `code` value from §2.3 in the action's XML `<remarks>`.

---

## 10. Integrations / Touchpoints

| Component | Change |
|---|---|
| `AuditEventsController` | Rewrite `Query`; add `GetById`; bind new params |
| `GetAuditEventsQuery` (MediatR) | Replace `Page` with `Cursor`, add `Sort`, `ResourceId`; return `AuditEventsResponse` |
| `GetAuditEventsHandler` | Add cursor decode + hash check + `nextCursor` build |
| `GetAuditEventByIdQuery` + handler (new) | Single-event lookup |
| `IAuditEventRepository.QueryAsync` | Signature change → returns `AuditEventsPage` |
| `AuditEventRepository` | Implement keyset query (§6.1) |
| `AuditEventFilter` | New shape (§3.4) |
| Migration | `AddAuditEventQueryIndexes` (§5.1) |
| `AuditEventItemMapper` (new) | Flat DTO → nested API item |
| Tests | New keyset/contract tests (§12) |

No changes to `RecordAuditEventCommand`, the append-only trigger, retention job, or PII redactor.

---

## 11. Edge Cases

| # | Case | Behavior |
|---|---|---|
| E1 | Two events with identical `Timestamp` | Tie-broken by `Id` in the same direction as `sort`. Cursor anchors on both fields; no row visited twice. |
| E2 | New event written between page N and page N+1 | Keyset clause `Timestamp < cursor.ts OR (== AND Id < cursor.id)` excludes the freshly inserted row from already-served slices. AC-3.4 holds. |
| E3 | New event with timestamp older than `cursor.ts` (clock skew theoretically possible — but server is single source of truth, so cannot happen) | Documented as not possible per architecture rule §5 in AGENTS.md. No defensive code. |
| E4 | Last page exactly fills `pageSize` | `Take(pageSize+1)` returns exactly `pageSize` rows → `nextCursor = null`. No empty trailing page. |
| E5 | `fromUtc == toUtc` | Allowed; window of zero duration, single-instant query. |
| E6 | Zero matches | `200`, `items: []`, `nextCursor: null`. Never 404. |
| E7 | `cursor` from a different filter (AC-3.6) | Hash mismatch → `400 cursor_mismatch`. |
| E8 | Cursor base64 valid but JSON malformed | `400 cursor_invalid`. |
| E9 | `id` in `cursor` is parseable but the row was retention-deleted | Keyset clause still works (it's a comparison, not a join). Caller advances past the deleted boundary correctly. |
| E10 | `pageSize=1` | Valid; degenerate but correct. |
| E11 | Single-event lookup with malformed Guid | ASP.NET routing returns 400 by route constraint (`{id:guid}`). |
| E12 | Resource filter contains `%` / `_` | Exact-match (`==`), not LIKE. No wildcard semantics. |

---

## 12. Testing Strategy

Test harness: `WebApplicationFactory<Program>` + Testcontainers PostgreSQL (one container per test class, schema migrated on first connect). Existing `tests/AuditLogService.Api.Tests` should be inspected and aligned with this harness.

### 12.1 Unit tests (`AuditLogService.Application.Tests`)

- Cursor encoder round-trips (encode → decode → equal).
- Filter-hash determinism (same filter set → same hash; different sets → different).
- Hash insensitive to property iteration order (canonicalisation works).
- `GetAuditEventsHandler` rejects `cursor_mismatch` when filter changes between calls.
- Validation matrix from §7 — one test per `code`.

### 12.2 Integration tests (`AuditLogService.Api.Tests`)

| Test | Verifies |
|---|---|
| `Query_returns_200_with_envelope` | Happy path shape |
| `Query_pages_through_full_set_no_dup_no_loss` | AC-3.4 — seed 250 rows, page=25, walk until `nextCursor==null`, assert all 250 distinct ids |
| `Query_pages_correctly_under_concurrent_writes` | AC-3.4 — seed 100, page 25, between page 1 and 2 insert 10 new events, walk; original 100 still all returned exactly once |
| `Query_window_too_large_returns_400_window_too_large` | AC-1.2 |
| `Query_missing_window_returns_400_window_required` | AC-1.2 |
| `Query_cursor_mismatch_returns_400` | AC-3.6 — mutate `actor` filter on second page |
| `Query_sort_asc_returns_oldest_first_stable` | AC-2.2, AC-2.3 |
| `Query_filter_by_correlationId_returns_only_traced_events` | AC-2.4 |
| `Query_empty_match_returns_200_empty_envelope` | AC-1.6 |
| `Query_pageSize_out_of_range_returns_400` | AC-3.5 |
| `Query_correlationId_not_in_response_items` | AC-1.5 |
| `GetById_returns_envelope_with_one_item` | §2.2 |
| `GetById_unknown_returns_404_event_not_found` | §2.3 |
| `Query_p95_latency_under_500ms_for_seeded_dataset` | AC-2.5 — seed 100k rows, run 50 representative queries, measure p95 |

### 12.3 Index sanity

A repository-level test runs `EXPLAIN ANALYZE` on each of the four hot-path queries and asserts the corresponding index from §5.1 is selected (string-match on the plan output).

---

## 13. Implementation Plan (sequential)

1. Add `SortDirection` enum, `AuditEventsPage`, `AuditEventsCursor` records (Domain/Application).
2. Rewrite `AuditEventFilter` (§3.4); update `IAuditEventRepository` signature.
3. Add migration `AddAuditEventQueryIndexes` (§5.1); apply locally; verify with `\d audit_events`.
4. Implement keyset query in `AuditEventRepository` (§6.1).
5. Implement cursor encode/decode + filter-hash helper in Application layer.
6. Rewrite `GetAuditEventsHandler`; add `GetAuditEventByIdHandler`.
7. Add API DTOs (`AuditEventItem`, `ActorRef`, `ResourceRef`, `AuditEventsResponse`).
8. Add `AuditEventItemMapper` in `src/AuditLogService.Api/Mapping/`.
9. Rewrite `AuditEventsController.Query`; add `GetById`. Wire ProblemDetails codes.
10. Annotate controller actions for Scalar; enable XML doc generation if not already on.
11. Write unit + integration tests (§12.1, §12.2); ensure all green.
12. Run full test suite (`dotnet test`) — all existing tests must still pass.
13. Manual smoke: `dotnet run` + Scalar UI at `/scalar`; walk pagination on a seeded dataset.

---

## 14. Out of Scope (mirrors requirements)

AuthN/Z, full-text search, aggregations/analytics, bulk export, push subscriptions. The Open Questions in [requirements.md](./requirements.md#open-questions) (rate limiting, cursor TTL, retention truncation hint, occurredAt naming) are explicitly **not** addressed by this design.
