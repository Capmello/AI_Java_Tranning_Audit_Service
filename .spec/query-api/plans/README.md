# Query API — Implementation Tasks

Each task is one safe commit / PR. Land in dependency order.

| # | Task | Depends on | Size |
|---|---|---|---|
| [T1](./T1-database-indexes_plan.md) | Database migration: query indexes | — | S |
| [T2](./T2-cursor-codec_plan.md) | Cursor codec & helper types | — | S |
| [T3](./T3-keyset-pagination_plan.md) | Keyset pagination contract & repository/handler wiring | T1, T2 | L |
| [T4](./T4-response-envelope_plan.md) | Envelope response, item mapper, ProblemDetails error codes | T3 | M |
| [T5](./T5-single-event-endpoint_plan.md) | Single-event endpoint `GET /api/audit-events/{id}` | T4 | S |
| [T6](./T6-integration-tests_plan.md) | Integration test suite (Testcontainers Postgres) | T5 | M |
| [T7](./T7-openapi-docs_plan.md) | Scalar / OpenAPI documentation | T5 | S |

## Dependency graph

```
        T1 ──┐
             ├──▶ T3 ──▶ T4 ──▶ T5 ──┬──▶ T6
        T2 ──┘                        └──▶ T7
```

T1 and T2 are independent and can land in either order. T6 and T7 are independent and can land in parallel after T5.

## References

- [requirements.md](../requirements.md)
- [design.md](../design.md)
