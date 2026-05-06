# T6 - Pagination correctness integration

## Goal
Prove the collection endpoint can be paged end-to-end without loss or duplication, including under concurrent writes.

## Refs
- Requirements: [requirements.md](../../../.spec/query-api/requirements.md) - AC-2.2, AC-2.3, AC-3.2, AC-3.3, AC-3.4, AC-3.6
- Design: [design.md](../../../.spec/query-api/design.md) - sections 4.2, 11 E1, E2, E4, E7, E9, E10, 12.2, 13 step 11

## Scope
- Expand API integration coverage for multi-page traversal.
- Add a concurrent-write pagination test.
- Add sort stability tests for ascending order and timestamp ties.
- Add cursor mismatch API tests that exercise the full HTTP path.

## Dependencies
- Depends on: T4
- Independent of: T5
- Unblocks: T7

## Definition of Done
- Integration tests demonstrate that iterating with `nextCursor` until `null` returns each seeded event exactly once for a fixed filter and sort.
- A concurrent-write test inserts new events between page requests and proves the original result set is still returned without duplicates or skipped rows.
- Integration tests cover:
  - ascending chronological traversal,
  - stable tie-break behavior on equal timestamps,
  - cursor mismatch after mutating a bound filter,
  - cursor mismatch after mutating sort.
- The tests are deterministic and pass reliably on repeated local runs.
- `dotnet build AuditLogService.slnx` passes.
- `dotnet test AuditLogService.slnx` passes.

## Safe PR boundary
This PR focuses only on pagination correctness and regression safety through integration tests. Index-plan checks and latency budget verification are separate.
