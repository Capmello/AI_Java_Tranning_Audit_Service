# T6 — Integration test suite (Testcontainers Postgres)

**Refs:** [requirements.md AC-2.5, AC-3.4, AC-3.6](../requirements.md) · [design.md §12](../design.md#12-testing-strategy)

**Dependencies:** **T5** (full endpoint surface in place; tests exercise list + by-id).

## Scope

Land the full integration-test matrix from design.md §12.2 against a real Postgres instance, including the AC-3.4 concurrency test, the AC-2.5 latency SLO check, and the AC-3.6 cursor-mismatch enforcement.

**In:**
- Add `Testcontainers.PostgreSql` (free, MIT) to the API test project.
- Shared `PostgresFixture` (xUnit collection fixture) that boots one container per test class, applies migrations, exposes a `WebApplicationFactory<Program>` wired to it via `ConfigureWebHost`.
- Test list:
  - `Query_pages_through_full_set_no_dup_no_loss` (250 rows, page=25, walk to null).
  - `Query_pages_correctly_under_concurrent_writes` (seed 100, page 25, between page 1 and 2 write 10 new events, walk; assert original 100 returned exactly once).
  - `Query_window_too_large_returns_400` / `Query_window_required_returns_400` / `Query_window_invalid_returns_400`.
  - `Query_pageSize_out_of_range_returns_400`.
  - `Query_sort_invalid_returns_400`.
  - `Query_missing_resource_returns_400` (resourceId without resource).
  - `Query_cursor_invalid_returns_400` (mangled base64).
  - `Query_cursor_mismatch_returns_400` (actor filter mutated between pages).
  - `Query_sort_asc_returns_oldest_first_stable` (assert order on 50 ties).
  - `Query_filter_by_correlationId_returns_only_traced_events`.
  - `Query_correlationId_not_in_response_items`.
  - `Query_empty_match_returns_200_empty_envelope`.
  - `GetById_returns_envelope_with_one_item`.
  - `GetById_unknown_returns_404_event_not_found`.
  - `Query_p95_latency_under_500ms` — seed 10 000 rows (smaller than design.md's 100 000 to keep CI fast; document the divergence), run 50 representative queries, assert p95 < 500 ms.
  - `Repository_uses_resource_resourceid_index` — `EXPLAIN (FORMAT JSON)` plan contains `ix_audit_events_resource_resourceid_timestamp_id` for a `(resource, resourceId)` query.
  - One similar `EXPLAIN` test per remaining hot-path index (4 total).

**Out:**
- Sub-second profiling beyond p95 (no per-query budgets).
- Load testing (separate concern; not in CI).

## Files touched

- `tests/AuditLogService.Api.Tests/AuditLogService.Api.Tests.csproj` (add Testcontainers.PostgreSql package)
- `tests/AuditLogService.Api.Tests/Fixtures/PostgresFixture.cs` (new)
- `tests/AuditLogService.Api.Tests/Query/QueryEndpointTests.cs` (new)
- `tests/AuditLogService.Api.Tests/Query/CursorPaginationTests.cs` (new)
- `tests/AuditLogService.Api.Tests/Query/IndexPlanTests.cs` (new)
- `tests/AuditLogService.Api.Tests/Query/LatencyBudgetTests.cs` (new)

## Implementation outline

1. Add the package and fixture; gate on Docker availability with a `[Trait("Category","Integration")]` so the suite skips cleanly on machines without Docker.
2. Helper `Seed(N, opts)` to bulk-insert audit events directly via `AuditDbContext` (bypassing the controller for speed).
3. For the concurrency test, use `Task.Run` to insert mid-pagination after a deterministic await point.
4. For latency, warm up with 5 queries before measuring; record durations into a list and compute p95.
5. EXPLAIN tests use `db.Database.SqlQueryRaw<string>("EXPLAIN (FORMAT JSON) ...")` and assert substring presence.

## Definition of Done

- [ ] `dotnet test` (full suite) passes locally with Docker running.
- [ ] All test names from "In" are present and green.
- [ ] CI workflow runs the integration tier (or has a documented opt-out trait if CI lacks Docker).
- [ ] `Query_pages_correctly_under_concurrent_writes` reliably yields 100 distinct ids over at least 20 consecutive runs (no flake on local machine).
- [ ] `Query_p95_latency_under_500ms` passes on a developer laptop; if it would be flaky on CI hardware, mark with a clear `[Trait("Category","Performance")]` and exclude from the default CI run.
- [ ] EXPLAIN tests assert the expected index name appears in the plan for each of the four hot-path queries.
- [ ] No production code changes — this PR is tests-only (any minor seam additions, e.g. `internal` exposure for tests, must be documented in the PR description).

## Risks / notes

- Testcontainers needs Docker. AGENTS.md doesn't enumerate CI infra — confirm the runner has Docker; if not, this task scope expands to wire a Postgres service in CI.
- The 100 k-row seed in design.md §12.2 was aspirational. We use 10 k for CI tractability and document the trade-off; the SLO target stays 500 ms but at lower data volume. A follow-up could add a nightly perf job at full scale.
