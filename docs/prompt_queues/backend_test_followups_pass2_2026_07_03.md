# Backend Test Follow-up Queue — Pass 2 — 2026-07-03

Source: `../BACKEND_TEST_COVERAGE_AUDIT_2026_07_03_PASS2.md`  
Previous detailed queue: `backend_test_followups_2026_07_03.md`

> Prompt IDs 036–041 were already occupied by parallel backend coverage work. New residual prompts from this pass use 042–047 to preserve one-problem/one-ID traceability.

## Status overrides for previous queue

| ID | Current status | Evidence |
|---|---|---|
| BACKEND-TEST-024 | Runtime-fixed / Needs validation | Maintenance DI, read-only stats, shared worker service and 4 positive admin tests. `.ai/runs/2026-07-03-BACKEND-TEST-024-evidence.md`. |
| BACKEND-TEST-028 | Runtime-fixed / Needs validation | Shared safe pagination helper, analytics/bug migration and 15 boundary cases. `.ai/runs/2026-07-03-BACKEND-TEST-028-evidence.md`. |
| BACKEND-TEST-029 | Implemented / Needs validation | 5 analytics/recommendation HTTP contract tests. `.ai/runs/2026-07-03-BACKEND-TEST-029-evidence.md`. |
| BACKEND-TEST-030 | Runtime-fixed / Needs validation | Stable safe explanation not-found responses and 9 endpoint cases. `.ai/runs/2026-07-03-BACKEND-TEST-030-evidence.md`. |
| BACKEND-TEST-035 | Implemented / Needs validation | 3 direct `TestAuthHandler` contract tests; obvious anonymous suites already migrated. `.ai/runs/2026-07-03-BACKEND-TEST-035-evidence.md`. |

## Still-open highest priority from previous queue

- BACKEND-TEST-022 — durable quiz/offline analytics ingest delivery;
- BACKEND-TEST-023 — multi-instance outbox claim/dead-letter safety;
- BACKEND-TEST-025 — bug-report input/screenshot safety;
- BACKEND-TEST-026 — public operational surface minimization;
- BACKEND-TEST-027 — unwired question endpoint decision;
- BACKEND-TEST-031 — weakness scheduler durability/backpressure;
- BACKEND-TEST-032 — PostgreSQL provider-specific integration lane;
- BACKEND-TEST-033 — P0 mutation cancellation/rollback matrix;
- BACKEND-TEST-034 — legacy alias parity/deprecation.

---

## BACKEND-TEST-042 — Distributed maintenance lock, audit and safe errors

Priority: P1  
Run mode: runtime hardening + PostgreSQL integration tests

### Problem

The new singleton `SemaphoreSlim` prevents overlapping rebuilds only within one API process. Multiple replicas, a CLI maintenance tool and another worker can still execute index maintenance concurrently. Manual rebuilds are not durably audited by actor/correlation id, and per-index exception messages are returned in the admin response.

### Required work

1. Add a PostgreSQL advisory lock or durable lease shared across replicas/tools.
2. Define lock timeout and `409 Conflict`/accepted behavior for a concurrent manual request.
3. Persist an operator audit row containing actor user id, correlation id, start/end, result counts and safe failure codes.
4. Store detailed exception data only in protected logs; return bounded safe error codes/messages.
5. Make the CLI/background/API paths use the same lock contract.

### Required tests

- two independent service instances race; one acquires the distributed lock;
- second request returns the documented busy response without running SQL;
- abandoned session/connection releases the advisory lock;
- actor and correlation id are captured for manual rebuild;
- raw connection/SQL exception text is absent from HTTP response/audit summary;
- cancellation releases lock and records cancellation outcome;
- background and HTTP paths cannot overlap.

PostgreSQL execution is mandatory.

---

## BACKEND-TEST-043 — Explanation input abuse and cost guard

Priority: P1  
Run mode: validators + endpoint policy + tests

### Problem

Current validators cover core required fields, but several potentially expensive or unbounded inputs remain:

- generate `StudentAnswer` and `ExpectedAnswer`;
- mistake `ExpectedAnswer`, `Topic`, `Subtopic`;
- grade range;
- positive `ProblemId` semantics;
- allowed language/difficulty values;
- repeated expensive generate/mistake requests.

### Required work

1. Define maximum lengths based on actual mobile/content contracts.
2. Require positive problem ids when present.
3. Bound grade and normalize/validate language and difficulty.
4. Add per-user rate/cost limits for generate and mistake-analysis routes.
5. Ensure cache hits and force-refresh have separate cost policy.
6. Reject invalid input before service/cache/AI calls.

### Required tests

- exact max and max+1 for every text field;
- zero/negative/overflow problem id and grade boundaries;
- unsupported language/difficulty;
- validator failure produces zero service calls;
- rate limit returns stable 429 and no expensive work;
- force-refresh cannot bypass rate/cost policy;
- internal AI/provider errors remain generic and traceable.

Cross-repo contract sync required if accepted values or limits affect mobile.

---

## BACKEND-TEST-044 — Deterministic maintenance scheduler tests

Priority: P1/P2  
Run mode: hosted-service refactor + clock tests

### Problem

`IndexMaintenanceBackgroundService` reads `DateTime.UtcNow` directly and contains its own 03:00 scheduling loop. The schedule cannot be tested without waiting, and restart-at-boundary behavior is not locked down.

### Required work

1. Inject `TimeProvider` and an awaitable scheduler/delay abstraction.
2. Move one scheduled iteration into a directly testable method.
3. Define behavior at exactly 03:00, just after 03:00, UTC restart and cancellation.
4. Keep the distributed non-overlap contract from BACKEND-TEST-042.

### Required tests

- startup before 03:00 schedules same day;
- startup exactly at 03:00 has documented behavior;
- startup after 03:00 schedules next day;
- one tick invokes one rebuild;
- cancellation during delay exits without rebuild;
- cancellation during rebuild propagates safely;
- rebuild failure does not spin or immediately retry in a tight loop;
- next run remains next-calendar-day correct.

---

## BACKEND-TEST-045 — Database-level or cursor analytics pagination

Priority: P1/P2  
Run mode: service/query refactor + performance tests

### Problem

Overflow is now bounded, but analytics endpoints still ask the service for all rows up to `page * pageSize` and then call `Skip` in memory. Page 100 at size 100 can materialize 10,000 rows for one response.

### Required work

1. Add service methods accepting safe skip/take or cursor tokens.
2. Push ordering, skip/take and projection into EF/PostgreSQL.
3. Define stable ordering and tie-break keys.
4. Prefer cursor pagination for high-growth recommendation/weakness history if ordering permits.
5. Keep the current response contract or version it explicitly.

### Required tests

- page/cursor returns only requested rows from SQL;
- deterministic ordering with equal weakness/priority values;
- no duplicates or gaps across adjacent pages;
- user isolation remains enforced;
- query-count and materialized-row budget;
- cancellation reaches EF query;
- invalid/expired cursor returns stable error;
- SQLite semantic tests plus PostgreSQL query-plan/integration evidence.

---

## BACKEND-TEST-046 — Remaining page-based endpoint migration audit

Priority: P1/P2  
Run mode: static inventory + focused implementation tests

### Problem

`PaginationBounds` is applied to analytics and bug reports only. Other endpoint/service files may still perform direct multiplication, `Skip` or unbounded offset calculations.

### Required work

1. Inventory every `page`, `pageSize`, `offset`, `skip`, `take`, `limit` input across API and admin-facing services.
2. Classify each as already bounded, shared-helper candidate, cursor candidate or internal-only.
3. Apply `PaginationBounds` or a cursor abstraction where appropriate.
4. Record intentional exceptions.

### Required tests

- parameter matrix with `int.MinValue`, -1, 0, normal, max and `int.MaxValue`;
- no arithmetic overflow/500;
- bounded service/query arguments;
- stable empty far-page response;
- no cross-user leakage;
- route inventory test prevents new unbounded page parameters.

---

## BACKEND-TEST-047 — Automated privileged-route authorization audit

Priority: P1  
Run mode: endpoint metadata inventory test

### Problem

Bug and maintenance routes previously relied on comments/group names rather than executable admin policy. Similar drift can reappear on newly added `/api/admin`, maintenance, logs, observability, authoring or support routes.

### Required work

1. Define a canonical allowlist of public, authenticated, content-author and admin route patterns.
2. Build an endpoint metadata test that enumerates `RouteEndpoint` instances.
3. Fail when a privileged route has only generic auth, no auth or the wrong policy.
4. Fail when a new route is not classified.
5. Keep deliberate public health/auth routes explicitly allowlisted.

### Required tests

- every `/api/admin/*` route has an accepted admin/content-author policy;
- maintenance, logs, idempotency observability and global bug management require exact admin policy;
- authoring mutation routes require content-author/admin policy;
- public routes are explicit and minimal;
- test-only auth scheme does not change metadata conclusions;
- adding an unclassified route fails with a useful message.

## Validation order

```text
dotnet test tests/MathLearning.Tests/MathLearning.Tests.csproj --filter "MaintenanceEndpoint|AnalyticsEndpoint|ExplanationEndpoint|TestAuthHandlerTests|PaginationBounds|ExtremePagination|BugReportServicePagination"
dotnet build MathLearning.slnx -c Release
```

After focused validation, run the full coverage workflow before marking any status above as Validated.
