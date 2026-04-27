## Project map
Core audit logging domain responsible for immutable event recording and querying.
- PostgreSQL (via EF Core migrations)
- Event storage (append-only table)
- Retention + archival background job
- Domain unit tests
- API contract tests
- .Net 10
- Visual Studio 2026
- Serilog and Event logging

## Invariants

These rules are strict and must never be violated:
- **Server-controlled timestamp**
  - `timestamp` is always set by the server
  - Client-provided timestamps are ignored/rejected
- ** Always check that solution can be built and run**
- ** Always run tests after applying changes**
- ** Do not change tests to bypass failures**
- ** Use only freeware nuget packages, avoid any commercial**
- ** New features should be implemented in separate branch. Naming rule is feature/feature-description, for example: feature/add-logging, feature/add-database
- ** Bugs should be fixed in separate branch. Naming rule is bug/bug-description, for example: bug/fix-jwt-token, bug/fix-migrations
- ** New branches are created only in case current selected branch is main
- ** New features and bug fixes should be covered with unit and integrations tests
- ** Use .env files for secrets, connections strings other settings. Do not hardcode them in code or expose.
- ** .env files should be separated by image and have meaningful name. For example: postgress.env, myproject.env, grafana.env etc.
- ** Default settings for external images (for example, postgress) should be stored in env files. 
- ** .env files should be stored in separate folder. This folder and it's content should be included in solution.
- ** Functionality should be logged to simplify debug. Multiple levels (Debug, Information, Error) should be used accordinally
- ** Logs should not contain sensative information for PROD enviroment.
- ** Stacktrace should not be exposed to end user in PROD enviroment
---

## Architecture rules

### 1. DDD-first approach
- Domain layer has no dependencies on frameworks
- Business rules live in the domain model

### 2. Clean architecture layering
- Domain → Application → Infrastructure → API
- Dependencies only point inward

### 3. Persistence rules
- Schema changes only via migrations
- Data model optimized for append-only writes

### 4. Read/write separation
- Write model: append-only audit event store
- Read model: optimized queries for filtering/search
- CQRS-style separation allowed when needed

### 5. Time consistency
- All timestamps are UTC
- Server is the single source of truth for time

### 6. Observability
- Every write must be traceable (logging + correlation ID recommended)
- Failures in event ingestion must not be silent

### 7. Testing strategy
- Domain logic fully covered by unit tests
- Critical API flows covered by integration tests