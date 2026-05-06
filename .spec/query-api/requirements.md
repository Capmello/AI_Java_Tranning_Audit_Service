# Query API — Requirements

## Problem

The audit log service today supports only the write path (`POST /api/audit-events`). There is no production-quality way to read events back. A stub `GET /api/audit-events` exists but is undocumented, uses unstable offset pagination over a live append-only stream, and has no defined contract for filtering, sorting, time-range bounds, or response shape.

This blocks three operational use cases:
- **Compliance** — auditors cannot independently confirm or refute that an action occurred.
- **Incident response** — SREs cannot reconstruct an action timeline on a specific resource.
- **Security investigation** — analysts cannot reliably scan large historical result sets without losing or duplicating rows as new events arrive.

The Query API delivers a documented, filterable, deterministically paginated read endpoint that addresses these three needs.

## User Stories

### US-1 — Compliance officer confirms or refutes an action

> As a **Compliance officer**, I need to look up audit events by actor, action, resource, and time range so that I can confirm or refute a specific action during an audit.

**Acceptance Criteria**

- AC-1.1 `GET /api/audit-events` accepts query parameters: `actor`, `action`, `resource`, `resourceId`, `correlationId`, `fromUtc`, `toUtc`, `pageSize`, `cursor`, `sort`.
- AC-1.2 `fromUtc` and `toUtc` are **required**; the server rejects requests where `toUtc - fromUtc > 90 days` with `400 Bad Request` and a machine-readable error code (`window_too_large`).
- AC-1.3 All filters are AND-combined; omitted filters do not constrain the result.
- AC-1.4 `GET /api/audit-events/{id}` returns the single event by its server-assigned id, or `404 Not Found` if no such event exists.
- AC-1.5 Each returned item carries: `id`, `occurredAt` (UTC ISO-8601), `actor{id,type}`, `resource{id,type}`, `action`, `payload` (free-form metadata map). `correlationId` is **not** included in items (filter-only).
- AC-1.6 Empty result sets return `200 OK` with `{ "items": [], "nextCursor": null }` — never `404`.
- AC-1.7 Server timestamps remain authoritative; clients cannot influence ordering or values.

### US-2 — SRE reconstructs a resource timeline

> As an **SRE**, I need to retrieve all events for a specific resource ordered chronologically so that I can reconstruct the timeline of actions during an incident.

**Acceptance Criteria**

- AC-2.1 Filtering by `resource` alone, or `resource` + `resourceId`, returns every event matching that resource within the requested time window.
- AC-2.2 The endpoint accepts `sort=asc` or `sort=desc` (default `desc`); ASC delivers oldest-first for chronological reconstruction.
- AC-2.3 Sort order is stable: ties on `occurredAt` are broken by `id` in the same direction as the chosen sort.
- AC-2.4 Filtering by `correlationId` returns every event sharing that correlation id, enabling cross-resource trace reconstruction.
- AC-2.5 p95 latency < **500 ms** for any single-page request within a 90-day window with the documented filter set.
- AC-2.6 The endpoint emits structured logs at `Information` level including the filter shape (no PII), result count, and correlation id; failures log at `Error` and never silently swallow exceptions.

### US-3 — Security analyst paginates a large result set without loss or duplication

> As a **Security analyst**, I need to page through a large filtered result set so that I see every matching event exactly once even while new events are being written.

**Acceptance Criteria**

- AC-3.1 Pagination is **keyset-based** over `(occurredAt, id)`. Offset/page-number pagination is **not** supported.
- AC-3.2 The first request omits `cursor`; subsequent requests pass the `nextCursor` value from the previous response verbatim.
- AC-3.3 The response envelope is `{ "items": [...], "nextCursor": "<opaque-string>" | null }`. `nextCursor` is `null` when no further pages exist for the supplied filter+sort.
- AC-3.4 Within a fixed filter+sort, iterating until `nextCursor` is `null` yields **every** event whose `occurredAt` lies within the time window at request time, with **no duplicates** and **no skipped rows**, regardless of concurrent writes.
- AC-3.5 `pageSize` defaults to **25**, max **100**; values outside `[1, 100]` return `400 Bad Request`.
- AC-3.6 Cursors are bound to the original `sort` and filter set. Changing `sort`, `fromUtc`, `toUtc`, or any filter while reusing a cursor returns `400 Bad Request` (`cursor_mismatch`).
- AC-3.7 Cursors are opaque strings; clients must not parse them.

## Out of Scope

- **Authentication & authorization** — no auth model, role gating, or per-tenant scoping is defined in this spec.
- **Full-text or metadata search** — no free-text search and no querying inside the `payload`/metadata JSON.
- **Aggregations & analytics** — no counts, group-by, percentile, or time-bucket endpoints.
- **Bulk export** — no CSV / NDJSON streaming download endpoint.
- **Subscriptions / push** — no SSE, WebSocket, or webhook delivery of events.
- **Mutations through this API** — read-only; the append-only invariant is preserved.

## Open Questions

1. **AuthN/Z model and scoping.** Which authentication scheme (JWT? mTLS? API key?) gates the endpoint, and how do caller identity / roles map to row-level visibility (e.g. tenant filter, sensitive-action redaction)? Currently the endpoint is open; a follow-up spec must close this before any non-internal exposure.
2. **Rate limiting / quota per caller.** Should the GET endpoint enforce per-caller request and result-volume quotas to protect the database from accidental or malicious large scans? If so, what limits and on what identity dimension?
3. **Cursor encoding & integrity.** Will cursors be plain base64-encoded `(occurredAt, id, filter-hash)` tuples, or signed/encrypted tokens with a TTL? Plain encoding is simplest; signed tokens prevent tampering and stale-filter reuse.
4. **Retention vs. query-window interaction.** When the requested window predates the retention/archival cutoff, should the response include a hint (`x-retention-truncated` header or envelope field) so callers know results may be incomplete?
5. **`occurredAt` naming at the API.** Internal entity uses `Timestamp`; this spec exposes `occurredAt` at the HTTP boundary only. Confirm the rename stays at the controller mapping layer and does not propagate into the domain entity.
