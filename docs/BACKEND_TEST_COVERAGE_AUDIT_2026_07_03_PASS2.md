# Backend Test Coverage Audit — Pass 2 — 2026-07-03

> This pass continues `BACKEND_TEST_COVERAGE_AUDIT_2026_07_03.md`. It distinguishes committed implementation from executable validation. No new test is considered validated until `dotnet test` or a checked GitHub Actions run succeeds.

Repo: `ivanjovicic/MathLearning`  
Run evidence: `.ai/runs/2026-07-03-BACKEND-TEST-AUDIT-003-evidence.md`  
Prompt status/addendum: `prompt_queues/backend_test_followups_pass2_2026_07_03.md`

## Executive result

This pass added **38 new executable test cases** across previously weak backend surfaces:

- maintenance positive admin/read-only contracts;
- analytics/recommendation HTTP contracts;
- explanation validation and safe-error contracts;
- pagination overflow/bounds;
- direct test-auth infrastructure.

It also made three runtime hardening changes:

1. maintenance operations are injectable and shared by HTTP/background paths;
2. `GET /api/maintenance/index-stats` is read-only rather than invoking rebuild/analyze;
3. analytics and bug-report pagination use bounded checked arithmetic while preserving prior page-size semantics.

## New coverage packages

### BACKEND-TEST-024 — Maintenance DI/read-only semantics

Runtime changes:

- introduced `IIndexMaintenanceService`;
- registered one singleton implementation shared by endpoint and hosted worker;
- split `GetIndexStatisticsAsync` from `RebuildCorruptedIndexesAsync`;
- GET routes no longer execute `REINDEX` or `ANALYZE`;
- cancellation tokens flow through endpoint, service and Npgsql calls;
- in-process rebuilds share a `SemaphoreSlim` guard;
- identifiers are quoted before `REINDEX`/`ANALYZE`;
- hosted service uses DI instead of constructing a separate service.

New executable cases: 4

- admin statistics calls only read-only statistics;
- admin health returns stable counts and calls only health read;
- admin rebuild invokes mutation exactly once;
- rebuild report with item errors returns `success=false` while preserving the report.

Residual risk: local guard is not distributed, real PostgreSQL rebuild behavior is untested, operator audit is missing and detailed item errors still need safer projection. Follow-up: BACKEND-TEST-042.

### BACKEND-TEST-029 — Analytics/recommendation HTTP contracts

New executable cases: 7

- three explicit anonymous route denials before service invocation;
- authenticated claim determines user and forged query `userId` is ignored;
- weakness paging/shape/cancellation contract;
- details and recommendation page/shape contracts;
- unexpected service error returns generic 500 with trace id and no raw message.

Residual risk: the service still fetches a bounded prefix and endpoints slice in memory; database/cursor pagination and PostgreSQL query budgets remain BACKEND-TEST-045.

### BACKEND-TEST-030 — Explanation validation and safe errors

Runtime change:

- raw `KeyNotFoundException.Message` is no longer returned;
- stored and referenced misses have stable public messages.

New executable cases: 9

- anonymous denial;
- blank language defaults to `en`;
- invalid generate and mistake requests short-circuit before service;
- generate/mistake/stored not-found cases do not leak raw text;
- valid mistake-analysis response/cancellation contract;
- unexpected exception uses generic global error response.

Residual risk: answer/topic fields, grade/problem-id ranges and expensive-route cost controls remain BACKEND-TEST-043.

### BACKEND-TEST-035 — Test authentication infrastructure

New executable cases: 3

- no headers preserve authenticated `test-user` compatibility;
- `X-Test-Anonymous: true` returns no authentication result;
- explicit user and comma-separated roles create expected claims.

Residual risk: repository-wide privileged-route classification remains BACKEND-TEST-047.

### BACKEND-TEST-028 — Pagination overflow and extreme values

Runtime changes:

- added shared `PaginationBounds.Normalize` and `PaginationWindow`;
- validated configuration plus checked arithmetic;
- default maximum page 1,000; analytics maximum page 100;
- analytics preserves prior clamp-to-range page-size behavior;
- invalid bug page size preserves established defaults: mine 50, admin list 20;
- bug endpoint and service both normalize for defense-in-depth.

New executable cases: 15

- helper matrix for minimum/zero/normal/maximum integers;
- custom analytics cap;
- invalid helper configuration;
- analytics extreme endpoint;
- user/admin bug extreme endpoints;
- direct bug-service extreme calls.

Residual risk: offset paging can remain expensive; analytics database-level paging is BACKEND-TEST-045 and remaining endpoint inventory is BACKEND-TEST-046.

## Coverage status after pass 2

### Strongest areas

- authoritative economy/cosmetics/season mutations and idempotency state machines;
- offline replay/timestamp/rollback behavior;
- auth refresh/registration relational packages awaiting execution;
- public identity and avatar privacy;
- monitoring/log authorization and redaction;
- question authoring, proxy trust and bounded reads;
- maintenance authorization plus positive read-only contracts;
- analytics/explanation HTTP contracts;
- shared pagination boundary behavior.

### Highest remaining P0/P1 gaps

1. BACKEND-TEST-022 — durable analytics ingest handoff.
2. BACKEND-TEST-023 — multi-instance outbox claiming/dead-letter behavior.
3. BACKEND-TEST-032 — PostgreSQL provider-specific locking/serialization/constraints.
4. BACKEND-TEST-012 — refresh-token 64/88/128 drift.
5. BACKEND-TEST-013 — missing operation identity decision.
6. BACKEND-TEST-025 — bug-report input/screenshot/orphan-file safety.
7. BACKEND-TEST-026 — public operational-detail minimization.
8. BACKEND-TEST-031 — weakness scheduler durability/backpressure.
9. BACKEND-TEST-033 — P0 cancellation/rollback matrix.
10. BACKEND-TEST-034 — legacy alias parity/retirement.

## New prompt-ready findings

Detailed prompts are in `backend_test_followups_pass2_2026_07_03.md`:

- BACKEND-TEST-042 — distributed maintenance lock, operator audit and safe errors;
- BACKEND-TEST-043 — explanation abuse/cost/input bounds;
- BACKEND-TEST-044 — deterministic maintenance scheduler tests;
- BACKEND-TEST-045 — database/cursor analytics pagination;
- BACKEND-TEST-046 — remaining page-based endpoint inventory;
- BACKEND-TEST-047 — privileged-route authorization metadata audit.

BACKEND-TEST-036 was already assigned by parallel coverage work. The pass-2 residual range starts at 042 to avoid ambiguous evidence IDs.

## Validation status

No executable .NET checkout or completed GitHub Actions run was available.

```text
dotnet test tests/MathLearning.Tests/MathLearning.Tests.csproj --filter "MaintenanceEndpoint|AnalyticsEndpoint|ExplanationEndpoint|TestAuthHandlerTests|PaginationBounds|ExtremePagination|BugReportServicePagination"
dotnet build MathLearning.slnx -c Release
```

Then run the full coverage workflow and inspect the ReportGenerator summary.

## Honest conclusion

The project is materially better protected at HTTP, authorization, pagination and maintenance boundaries. The highest-risk remaining work is still distributed/transactional: durable ingest, outbox concurrency, PostgreSQL provider behavior, refresh-token schema drift and operation identity.
