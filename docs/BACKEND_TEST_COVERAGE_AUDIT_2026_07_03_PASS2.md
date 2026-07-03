# Backend Test Coverage Audit — Pass 2 — 2026-07-03

> This pass continues `BACKEND_TEST_COVERAGE_AUDIT_2026_07_03.md`. It distinguishes committed implementation from executable validation. No new test is considered validated until `dotnet test` or a checked GitHub Actions run succeeds.

Repo: `ivanjovicic/MathLearning`  
Run evidence: `.ai/runs/2026-07-03-BACKEND-TEST-AUDIT-003-evidence.md`  
Prompt status/addendum: `prompt_queues/backend_test_followups_pass2_2026_07_03.md`

## Executive result

This pass added **36 new executable test cases** across four previously weak backend surfaces:

- maintenance positive admin/read-only contracts;
- analytics/recommendation HTTP contracts;
- explanation validation and safe-error contracts;
- pagination overflow/bounds and direct test-auth infrastructure.

It also made three runtime hardening changes:

1. maintenance operations are injectable and shared by HTTP/background paths;
2. `GET /api/maintenance/index-stats` is now read-only rather than invoking rebuild/analyze;
3. analytics and bug-report pagination now use bounded checked arithmetic.

## New coverage packages

### BACKEND-TEST-024 — Maintenance DI/read-only semantics

Runtime changes:

- introduced `IIndexMaintenanceService`;
- registered a singleton implementation shared by endpoint and hosted worker;
- split `GetIndexStatisticsAsync` from `RebuildCorruptedIndexesAsync`;
- GET routes no longer execute `REINDEX` or `ANALYZE`;
- cancellation tokens flow through endpoint, service, Npgsql open/read/execute calls;
- in-process rebuilds share a `SemaphoreSlim` non-overlap guard;
- identifiers used in `REINDEX`/`ANALYZE` are quoted with `NpgsqlCommandBuilder`;
- hosted service uses DI instead of constructing a separate service instance.

New endpoint tests: 4

- admin statistics route calls only read-only statistics;
- admin health route returns stable counts and calls only health read;
- admin rebuild invokes mutation exactly once;
- rebuild report with item errors returns `success=false` while retaining the report.

Existing anonymous/non-admin/metadata tests remain.

Residual risk:

- semaphore prevents overlap only within one process, not across multiple replicas;
- positive real PostgreSQL `REINDEX CONCURRENTLY` behavior is not exercised;
- operator actor/correlation audit is not yet persisted;
- item error strings are still returned to admins.

### BACKEND-TEST-029 — Analytics/recommendation HTTP contracts

New endpoint tests: 5

- explicit anonymous denial before service invocation;
- authenticated claim determines analytics user, while forged query `userId` is ignored;
- weakness paging normalization and stable response shape;
- details and recommendations page slicing/field contracts;
- unexpected service error returns generic global 500 with trace id and no raw message.

The fake service also records cancellation-token forwarding and requested take counts.

Residual risk:

- service interfaces still fetch all rows up to `page * pageSize` and slice in memory;
- bounded page 100 limits damage but cursor/true database paging would scale better;
- PostgreSQL query shape and performance remain unmeasured.

### BACKEND-TEST-030 — Explanation validation and safe errors

Runtime change:

- `KeyNotFoundException.Message` is no longer returned by generate/mistake routes;
- stored and referenced problem misses now return stable public messages.

New endpoint test cases: 9

- anonymous denial for all three routes;
- blank language defaults to `en`;
- invalid generate request short-circuits before service;
- invalid mistake answer short-circuits before service;
- generate and mistake not-found messages do not leak raw exception text;
- stored problem not-found is stable and safe;
- valid mistake-analysis response shape and cancellation-token delegation;
- unexpected exception uses generic global error response.

Residual risk:

- generate request does not visibly bound student/expected answer fields;
- mistake request does not visibly bound topic/subtopic/expected answer;
- grade and positive problem-id ranges are not enforced;
- expensive generate/mistake routes need explicit rate/cost policy.

### BACKEND-TEST-035 — Test authentication infrastructure

New direct tests: 3

- no headers preserve the historical authenticated `test-user` compatibility behavior;
- `X-Test-Anonymous: true` returns no authentication result;
- explicit user and comma-separated roles create the expected claims.

This makes future 401-vs-403 security tests independent from endpoint side effects.

### BACKEND-TEST-028 — Pagination overflow and extreme values

Runtime changes:

- added shared `PaginationBounds.Normalize` and `PaginationWindow` in Application;
- uses validated configuration and checked arithmetic;
- default max page is 1,000;
- analytics uses a stricter max page of 100;
- bug endpoint and `BugReportService` both normalize paging for defense-in-depth.

New test cases: 15

- helper matrix covers `int.MinValue`, zero, normal pages and `int.MaxValue`;
- custom analytics cap produces safe skip/fetch values;
- invalid helper configuration throws;
- analytics `int.MaxValue` query is capped at page 100/page size 50/take 5,000;
- user and admin bug routes cap at page 1,000/page size 100;
- direct bug service calls with `int.MaxValue` cannot overflow.

Residual risk:

- high pages remain offset-based and can still be slower than cursor paging;
- only analytics and bug-report surfaces were migrated in this pass;
- remaining page-based endpoints should be searched and migrated to the shared helper.

## Coverage status after pass 2

### Strongest areas

- authoritative economy/cosmetics/season mutations and idempotency state machines;
- offline replay/timestamp/rollback behavior;
- auth refresh/registration relational concurrency packages awaiting execution;
- public identity and avatar privacy;
- monitoring/log authorization and redaction;
- question authoring, proxy trust and bounded read inputs;
- maintenance authorization plus positive read-only contracts;
- analytics/explanation HTTP contracts;
- shared pagination boundary behavior.

### Highest remaining P0/P1 gaps

1. **BACKEND-TEST-022** — durable/idempotent analytics ingest handoff after authoritative answer commit.
2. **BACKEND-TEST-023** — multi-instance outbox claim/lease/dead-letter semantics.
3. **BACKEND-TEST-032** — PostgreSQL provider-specific locking, serialization and unique-constraint tests.
4. **BACKEND-TEST-012** — refresh-token EF model/snapshot length drift, 64 vs generated 88 vs DB 128.
5. **BACKEND-TEST-013** — missing operation identity contract for canonical retryable mutations.
6. **BACKEND-TEST-025** — bug-report input/screenshot validation and orphan-file compensation.
7. **BACKEND-TEST-026** — public health/metrics/schema/job information minimization.
8. **BACKEND-TEST-031** — weakness scheduler durability, deduplication and backpressure.
9. **BACKEND-TEST-033** — cancellation/rollback matrix for all canonical P0 mutations.
10. **BACKEND-TEST-034** — legacy alias parity and retirement.

## New prompt-ready findings from this pass

Detailed prompts are in `backend_test_followups_pass2_2026_07_03.md`:

- BACKEND-TEST-036 — distributed maintenance lock, operator audit and safe error projection;
- BACKEND-TEST-037 — explanation abuse/cost/input-bound hardening;
- BACKEND-TEST-038 — deterministic maintenance scheduler clock/restart tests;
- BACKEND-TEST-039 — database-level/cursor analytics pagination;
- BACKEND-TEST-040 — remaining page-based endpoint inventory and migration;
- BACKEND-TEST-041 — automated privileged-route authorization metadata audit.

## Validation status

No executable .NET checkout or completed GitHub Actions run was available in this connector session.

Required focused validation:

```text
dotnet test tests/MathLearning.Tests/MathLearning.Tests.csproj --filter "MaintenanceEndpoint|AnalyticsEndpoint|ExplanationEndpoint|TestAuthHandlerTests|PaginationBounds|ExtremePagination|BugReportServicePagination"
dotnet build MathLearning.slnx -c Release
```

Then run the full suite with coverage and inspect the ReportGenerator job summary.

## Honest conclusion

The project is materially better protected at HTTP, authorization, pagination and maintenance boundaries, but the most dangerous distributed/transactional risks remain durable ingest, outbox concurrency, PostgreSQL provider behavior, refresh-token schema drift and missing operation identity. These should remain ahead of cosmetic coverage-percentage work.
