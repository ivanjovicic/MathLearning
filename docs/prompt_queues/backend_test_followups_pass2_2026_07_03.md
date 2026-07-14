# Backend Test Follow-up Queue — Pass 2 — 2026-07-03

Source: `../BACKEND_TEST_COVERAGE_AUDIT_2026_07_03_PASS2.md`  
Previous detailed queue: `backend_test_followups_2026_07_03.md`

> BACKEND-TEST-036 was already assigned by parallel coverage work. New residual prompts from this pass start at 042 to avoid ambiguous evidence IDs and leave room for other in-flight allocations.

## Status overrides for previous queue

| ID | Current status | Evidence |
|---|---|---|
| BACKEND-TEST-024 | Runtime-fixed / Needs validation | Maintenance DI, read-only stats, shared worker service and 4 positive admin tests. `.ai/runs/2026-07-03-BACKEND-TEST-024-evidence.md`. |
| BACKEND-TEST-028 | Runtime-fixed / Needs validation | Shared safe pagination helper, analytics/bug migration and 15 boundary cases. `.ai/runs/2026-07-03-BACKEND-TEST-028-evidence.md`. |
| BACKEND-TEST-029 | Implemented / Needs validation | 7 analytics/recommendation HTTP contract cases. `.ai/runs/2026-07-03-BACKEND-TEST-029-evidence.md`. |
| BACKEND-TEST-030 | Runtime-fixed / Needs validation | Stable safe explanation not-found responses and 9 endpoint cases. `.ai/runs/2026-07-03-BACKEND-TEST-030-evidence.md`. |
| BACKEND-TEST-035 | Implemented / Needs validation | 3 direct `TestAuthHandler` contract tests. `.ai/runs/2026-07-03-BACKEND-TEST-035-evidence.md`. |

## Canonical ownership notes

| Test row | Canonical owner | Note |
|---|---|---|
| BACKEND-TEST-042 | maintenance operational workstream | Shared maintenance lock/audit work lives on the ops side; this row keeps the test-side regression gate. |
| BACKEND-TEST-043 | BE-PERF-014 | Force-refresh and cost-bound checks mirror the explanation-cache guard work. |
| BACKEND-TEST-045 | analytics pagination fixup workstream | This row covers the analytics side of the pagination fix and must preserve BACKEND-TEST-028 bounds. |
| BACKEND-TEST-046 | test coverage inventory only | No direct BE-PERF counterpart; keep this row as the page-based endpoint migration audit. |
| BACKEND-TEST-047 | test coverage inventory only | No direct BE-PERF counterpart; keep this row as the privileged-route authorization audit. |

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
Canonical owner: maintenance operational workstream; this row is the test-side gate for the shared maintenance lock/audit contract.
Linked to: BACKEND-LATEST-QUEUE-002
Primary evidence rule: one shared maintenance implementation across HTTP/background/CLI paths; this row contributes only the regression gate.
Run mode: runtime hardening + PostgreSQL integration tests

### Problem

The singleton `SemaphoreSlim` prevents overlapping rebuilds only within one process. Multiple replicas, a CLI tool or another worker can still execute maintenance concurrently. Manual rebuilds lack durable actor/correlation audit, and per-index exception messages are returned in the admin response.

### Required work

1. Add a PostgreSQL advisory lock or durable lease shared across replicas/tools.
2. Define lock timeout and stable busy response.
3. Persist actor, correlation id, start/end, result counts and safe outcome codes.
4. Keep detailed exception data only in protected logs.
5. Make CLI/background/API paths share the same lock contract.

### Required tests

- two independent service instances race and only one acquires the lock;
- concurrent request returns the documented busy result without running maintenance SQL;
- abandoned connection releases the lock;
- actor/correlation audit is persisted;
- raw SQL/connection exception text is absent from response/audit summary;
- cancellation releases the lock;
- background and HTTP paths cannot overlap.

PostgreSQL execution is mandatory.

---

## BACKEND-TEST-043 — Explanation input abuse and cost guard

Priority: P1  
Canonical owner: BE-PERF-014; this row validates the force-refresh and cost-bound contract.
Linked to: BACKEND-LATEST-QUEUE-002
Primary runtime evidence: canonical BE-PERF-014 implementation log; this row adds contract-bound validation only.
Run mode: validators + endpoint policy + tests

### Problem

Potentially expensive/unbounded inputs remain: answer fields, topic/subtopic, grade, positive problem-id semantics, allowed language/difficulty and repeated generation requests.

### Required work

1. Define contract-backed maximum lengths.
2. Require positive problem ids when present.
3. Bound grade and validate language/difficulty.
4. Add per-user rate/cost limits for generate and mistake-analysis.
5. Ensure force-refresh cannot bypass cost policy.
6. Reject invalid input before service/cache/AI calls.

### Required tests

- exact maximum and maximum+1 for every field;
- zero/negative ids and grade boundaries;
- unsupported language/difficulty;
- invalid request produces zero service calls;
- stable 429 with no expensive work;
- force-refresh obeys policy;
- provider failures remain generic and traceable.

Cross-repo sync is required if accepted values or limits change.

---

## BACKEND-TEST-044 — Deterministic maintenance scheduler tests

Priority: P1/P2  
Run mode: hosted-service refactor + clock tests

### Problem

The worker reads `DateTime.UtcNow` directly and owns a 03:00 loop that cannot be tested without waiting.

### Required work

1. Inject `TimeProvider` and a delay/scheduler abstraction.
2. Extract one scheduled iteration.
3. Define exactly-at-03:00, after-03:00, restart and cancellation behavior.
4. Preserve the distributed lock contract from BACKEND-TEST-042.

### Required tests

- before/exactly/after 03:00 scheduling;
- one tick invokes one rebuild;
- cancellation during delay and rebuild;
- failure does not spin in a tight loop;
- next run uses correct next UTC calendar day.

---

## BACKEND-TEST-045 — Database-level or cursor analytics pagination

Priority: P1/P2  
Canonical owner: analytics pagination fixup workstream; preserve BACKEND-TEST-028 bounds while moving the paging implementation.
Linked to: BACKEND-TEST-028, BACKEND-LATEST-QUEUE-002
Supersedes: endpoint-only bounded prefix slicing for analytics pagination once the canonical implementation lands.
Run mode: service/query refactor + performance tests

### Problem

Overflow is bounded, but endpoints still request a bounded prefix and slice in memory. Page 100 at size 100 can materialize 10,000 rows.

### Required work

1. Add safe skip/take or cursor service methods.
2. Push ordering, projection and paging into EF/PostgreSQL.
3. Define stable tie-break ordering.
4. Prefer cursor pagination where growth warrants it.
5. Preserve or version the response contract.

### Required tests

- SQL returns only requested rows;
- deterministic ordering with ties;
- no duplicates/gaps across adjacent pages;
- user isolation;
- query/materialized-row budget;
- cancellation reaches EF;
- invalid cursor has stable error;
- SQLite semantics plus PostgreSQL plan/integration evidence.

---

## BACKEND-TEST-046 — Remaining page-based endpoint migration audit

Priority: P1/P2  
Canonical owner: test coverage inventory only; this row stays on the test side and has no BE-PERF counterpart.
Run mode: static inventory + focused implementation tests

### Problem

`PaginationBounds` currently protects analytics and bug reports. Other route/service files may still perform direct multiplication, `Skip` or unbounded offsets.

### Required work

1. Inventory every page/pageSize/offset/skip/take/limit input.
2. Classify as bounded, helper candidate, cursor candidate or internal-only.
3. Apply the shared helper or cursor abstraction.
4. Record intentional exceptions.

### Required tests

- `int.MinValue`, negative, zero, normal and `int.MaxValue` matrix;
- no overflow/500;
- bounded service/query arguments;
- stable far-page response;
- no cross-user leakage;
- inventory test prevents new unbounded parameters.

---

## BACKEND-TEST-047 — Automated privileged-route authorization audit

Priority: P1  
Canonical owner: test coverage inventory only; this row stays on the test side and has no BE-PERF counterpart.
Run mode: endpoint metadata inventory test

### Problem

Bug and maintenance routes previously relied on comments/group names rather than exact policy metadata. Similar drift can recur on admin, logs, observability, authoring or support routes.

### Required work

1. Define canonical public/auth/content-author/admin route classification.
2. Enumerate `RouteEndpoint` metadata.
3. Fail when a privileged route has generic/no/wrong auth.
4. Fail when a new route is unclassified.
5. Keep intentional public routes explicitly allowlisted.

### Required tests

- `/api/admin/*` has accepted privileged policy;
- maintenance/logs/idempotency observability/global bug management require exact admin policy;
- authoring mutations require content-author/admin policy;
- public routes are explicit and minimal;
- test auth does not alter metadata conclusions;
- unclassified route failure message is actionable.

## Validation order

```text
dotnet test tests/MathLearning.Tests/MathLearning.Tests.csproj --filter "MaintenanceEndpoint|AnalyticsEndpoint|ExplanationEndpoint|TestAuthHandlerTests|PaginationBounds|ExtremePagination|BugReportServicePagination"
dotnet build MathLearning.slnx -c Release
```

After focused validation, run the full coverage workflow before marking any status Validated.
