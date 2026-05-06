# T7 - Performance and index verification

## Goal
Validate that the finished Query API meets the hot-path index expectations and the documented latency budget.

## Refs
- Requirements: [requirements.md](../../../.spec/query-api/requirements.md) - AC-2.5, AC-2.6
- Design: [design.md](../../../.spec/query-api/design.md) - sections 5.1, 5.2, 8, 12.2, 12.3, 13 step 12 and step 13

## Scope
- Add repository-level `EXPLAIN ANALYZE` checks for the hot-path queries.
- Add a repeatable performance test or benchmark harness for representative single-page queries over a large seeded dataset.
- Perform final manual smoke verification against the running API and Scalar.
- Tune logging or query shape only if required to satisfy the measured results.

## Dependencies
- Depends on: T2, T3, T4, T5, T6
- Unblocks: release readiness for the Query API feature

## Definition of Done
- There is an automated check for the four hot-path query shapes that asserts the intended index family is selected in the query plan.
- There is a documented, repeatable performance verification that measures representative single-page queries against a large seeded dataset.
- The measured result demonstrates p95 latency under 500 ms on the agreed local verification profile, or the task is not complete.
- Final manual smoke verification confirms:
  - the API starts successfully with the configured environment,
  - `/scalar` documents both GET endpoints,
  - pagination can be walked manually with a seeded dataset.
- Logging for successful queries remains structured and PII-safe.
- `dotnet build AuditLogService.slnx` passes.
- `dotnet test AuditLogService.slnx` passes.

## Safe PR boundary
This is the final hardening PR. It should not introduce new functional surface area unless required to fix issues discovered by performance or plan-verification work.
