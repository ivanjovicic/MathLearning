# MathLearning Backend Test Coverage Strategy

Last aligned: 2026-07-02  
Status: active test strategy

## Goal

Build high-confidence coverage around the backend behaviors that can lose user progress, duplicate rewards, leak another user's data, or break the Flutter contract.

Coverage percentage is a signal, not the goal. The goal is verified behavior on critical branches.

## Priority order

### P0 — authoritative mutations

Target first:

- authenticated user scope;
- refresh-token rotation/reuse/revoke;
- idempotency state machines;
- quiz answer settlement;
- SRS update;
- economy spend/reward claims;
- Daily Run chest claim;
- season Daily Run settlement;
- cosmetics item/fragment grants;
- offline batch replay.

Expected coverage:

- first success;
- exact replay;
- conflicting replay;
- concurrent calls;
- domain failure/rollback;
- different-user isolation;
- client payload cannot override server authority.

### P1 — security and privacy boundaries

- safe error responses;
- admin/monitoring authorization;
- public identity allowlists;
- avatar/file upload validation;
- cross-user reads and writes;
- refresh-token metadata and expiry.

### P1 — persistence truth

- EF unique indexes and relational constraints using SQLite/PostgreSQL where InMemory is insufficient;
- transaction rollback;
- response snapshot includes the newly persisted mutation;
- stored idempotency replay body equals the first response;
- migration-from-zero and startup schema guard.

### P2 — bounded reads and resilience

- limit/range/period/scope validation;
- malformed and extreme input;
- cancellation propagation;
- retry behavior;
- cache fallback;
- empty database behavior.

## Test layers

| Layer | Use for |
|---|---|
| Pure unit | deterministic helpers, calculators, validators, token/idempotency primitives |
| EF InMemory | service state transitions that do not depend on relational behavior |
| SQLite relational | unique indexes, transactions, FK behavior, concurrency assumptions |
| WebApplicationFactory | auth, routes, serialization, endpoint contracts, user isolation |
| PostgreSQL CI | migrations, provider-specific constraints, schema and startup guard |

Do not use EF InMemory as proof of a unique index or transaction guarantee.

## Critical coverage targets

Targets apply after a measured baseline exists:

- critical mutation services: 90% line and 80% branch;
- auth/idempotency/economy helpers: 90% line;
- endpoint contract classes: every P0 success/error/replay/user-scope branch represented;
- repository overall: raise gradually from measured baseline; do not lower thresholds to make CI green.

## Coverage gate rollout

1. Collect coverage artifacts without a threshold.
2. Record baseline by assembly and critical namespace.
3. Exclude migrations, generated files, test assembly, and designer files.
4. Add a non-blocking summary.
5. Set a floor slightly below the stable baseline.
6. Add stricter thresholds for critical namespaces.
7. Require changed-code coverage for new runtime work when tooling is available.

## Test quality rules

A good test must:

- state one business invariant;
- fail before the target bug fix or missing behavior;
- assert durable outcome, not only HTTP status;
- verify no unintended mutation on failure;
- use unique users/operation IDs;
- avoid order dependence;
- avoid sleeps and real clock boundaries where possible;
- show why the case is security/data-loss relevant.

Avoid:

- duplicate tests with different names;
- assertions only on `success=true`;
- shared fixed users when isolation is important;
- InMemory tests for database constraints;
- broad snapshot assertions that hide the exact invariant;
- increasing coverage with trivial property tests while critical branches remain uncovered.

## Required CI evidence

Every CI test run should retain:

- TRX test result;
- Cobertura coverage file;
- JSON coverage file;
- migration validation result;
- startup readiness smoke result.

## First implemented package — BACKEND-TEST-CORE-001

Added coverage for:

- Daily Run cosmetics settlement helper branches;
- Daily Run fragment grant server-authority/user-isolation boundary;
- economy idempotency state transitions and conflicting keys;
- refresh-token generation, expiry, validation, and idempotent revocation.

Validation remains pending until GitHub Actions or a local .NET run provides executable evidence.
