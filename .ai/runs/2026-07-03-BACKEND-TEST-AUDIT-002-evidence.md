# BACKEND-TEST-AUDIT-002 Evidence

Prompt ID: BACKEND-TEST-AUDIT-002
Queue: `docs/prompt_queues/backend_test_coverage.md`
Agent/tool: ChatGPT via GitHub connector
Model provider: OpenAI
Model name/id: GPT-5.5 Thinking
Run mode: coverage audit + targeted test implementation + prompt creation
Started from queue status: user-requested broad backend coverage review

## Goal

Map critical backend flows to existing executable test evidence, identify the highest-risk uncovered branches, implement the strongest safe test packages possible through the connector, and add prompt-ready follow-ups for every material gap found.

## Relevant prior mistakes read

- BACKEND-MISTAKE-EVIDENCE-001
- BACKEND-MISTAKE-AUDIT-001
- BACKEND-MISTAKE-VALIDATION-001
- BACKEND-MISTAKE-XREPO-001
- BACKEND-MISTAKE-AUTH-001

## How this run avoided prior mistakes

- Run evidence was created before test/runtime edits.
- Validated evidence, implemented-unvalidated tests and static findings are separated explicitly.
- No new test/build pass is claimed without executable evidence.
- Relational guarantees use SQLite tests and retain PostgreSQL follow-up authority.
- Contract/mobile sync is recorded in prompt evidence.
- Newly discovered recurring mistake patterns were added to the mistake ledger.

## Audit inputs

- current endpoint inventory and route compatibility audit;
- backend test coverage strategy and central queue;
- existing validated run evidence;
- auth, quiz/offline, ingest, idempotency, economy, cosmetics, health, monitoring, maintenance, bug, analytics, explanations, outbox and scheduler code;
- test authentication/factory infrastructure;
- coverage settings and database-validation workflow.

## Files added

- `docs/BACKEND_TEST_COVERAGE_AUDIT_2026_07_03.md`
- `docs/prompt_queues/backend_test_followups_2026_07_03.md`
- `tests/MathLearning.Tests/Services/QuizAttemptIngestServiceRelationalTests.cs`
- `tests/MathLearning.Tests/Endpoints/BugEndpointAuthorizationTests.cs`
- `tests/MathLearning.Tests/Endpoints/MaintenanceEndpointAuthorizationTests.cs`
- `tests/MathLearning.Tests/GlobalUsings.TestInfrastructure.cs`
- `.ai/runs/2026-07-03-BACKEND-TEST-011-evidence.md`
- `.ai/runs/2026-07-03-BACKEND-TEST-019-evidence.md`
- `.ai/runs/2026-07-03-BACKEND-TEST-020-evidence.md`
- `.ai/runs/2026-07-03-BACKEND-TEST-021-evidence.md`

## Files updated

- `src/MathLearning.Api/Endpoints/BugEndpoints.cs`
- `src/MathLearning.Api/Endpoints/MaintenanceEndpoints.cs`
- `tests/MathLearning.Tests/Helpers/TestAuthHandler.cs`
- `tests/MathLearning.Tests/Endpoints/MonitoringLogAuthorizationTests.cs`
- `.github/workflows/database-validation.yml`
- `docs/API_ENDPOINT_INVENTORY.md`
- `docs/DOCS_INDEX.md`
- `docs/prompt_queues/backend_test_coverage.md`
- `docs/ai/learning/MISTAKE_LEDGER.md`
- this run log.

## New tests implemented

Twelve new test methods:

- five relational `QuizAttemptIngestService` tests;
- four bug endpoint authorization/ownership/paging tests;
- three maintenance authorization/metadata tests.

Existing monitoring anonymous tests were strengthened to exercise a true anonymous principal rather than the default test user.

## Runtime problems fixed

1. Bug management list/detail/update routes now require the explicit admin policy; report/mine routes explicitly require authentication.
2. All maintenance routes now require the explicit admin policy.
3. Test auth now supports explicit anonymous requests, preventing false anonymous coverage.

## Coverage tooling improved

The database-validation workflow now:

- retains TRX/Cobertura/JSON;
- generates ReportGenerator HTML and merged Cobertura reports;
- publishes a Markdown coverage summary to the GitHub job summary;
- uploads generated reports even when tests fail;
- intentionally does not impose an arbitrary threshold before the baseline is measured.

## Prompt-ready gaps added

Created detailed prompts BACKEND-TEST-022 through BACKEND-TEST-035 for:

1. durable/idempotent quiz-attempt ingest delivery;
2. multi-instance-safe outbox claiming/dead-letter behavior;
3. maintenance DI/read-only semantics/positive admin coverage;
4. bug input and screenshot storage safety;
5. public health/metrics/monitoring minimization;
6. unwired question endpoint decision;
7. pagination overflow/extreme offsets;
8. analytics/recommendation endpoint contracts;
9. explanation endpoint validation/safe errors/cancellation;
10. weakness scheduler durability/dedup/backpressure;
11. PostgreSQL provider-specific integration matrix;
12. P0 mutation cancellation/rollback matrix;
13. legacy alias parity/deprecation;
14. authorization-test anonymous accuracy audit.

## New mistakes recorded

- `BACKEND-MISTAKE-AUTH-002` — privileged endpoints protected only by generic auth.
- `BACKEND-MISTAKE-IDEM-001` — authoritative commit before non-durable downstream ingest.
- `BACKEND-MISTAKE-VALIDATION-002` — no-header test requests silently authenticated, producing false anonymous coverage.

## Coverage verdict

Strong/validated or heavily implemented:

- idempotency and settlement paths;
- offline timestamp/replay safety;
- safe errors;
- identity/privacy and avatar safety;
- read bounds;
- proxy/authoring/adaptive/job/admin-seed/version-race protection;
- relational refresh, registration, transaction-helper and offline rollback tests awaiting execution.

Materially under-covered or still architecture-dependent:

- durable analytics ingest handoff;
- outbox multi-instance behavior;
- PostgreSQL provider-specific locking/serialization;
- refresh-token EF model drift;
- missing operation identity decision;
- public operational diagnostics;
- maintenance positive/read-only behavior;
- bug input/storage compensation;
- analytics and explanation HTTP contracts;
- scheduler durability;
- consistent P0 cancellation matrix;
- legacy alias parity.

Numeric overall line/branch coverage remains unknown until the updated workflow successfully publishes the first report.

## Validation run

- GitHub code/file inspection.
- Static consistency review of new tests, policies, docs, workflow paths and referenced service APIs.
- Queue, endpoint inventory, docs index and mistake ledger reconciliation.

## Validation not run

- `dotnet build` — unavailable because the execution environment has no repository checkout/.NET SDK.
- `dotnet test` — same limitation.
- GitHub Actions workflow — no completed status was available during implementation.
- PostgreSQL provider tests — not run.

## Required focused validation

```text
dotnet test tests/MathLearning.Tests/MathLearning.Tests.csproj --filter "FullyQualifiedName~QuizAttemptIngestServiceRelationalTests|FullyQualifiedName~BugEndpointAuthorizationTests|FullyQualifiedName~MaintenanceEndpointAuthorizationTests|FullyQualifiedName~MonitoringLogAuthorizationTests"

dotnet build MathLearning.slnx -c Release
```

Then execute the full coverage workflow or equivalent command:

```text
dotnet test tests/MathLearning.Tests/MathLearning.Tests.csproj -c Release --logger "trx;LogFileName=mathlearning-tests.trx" --results-directory artifacts/test-results --collect:"XPlat Code Coverage" --settings tests/MathLearning.Tests/coverage.runsettings
```

## Residual risk

- Newly committed tests and runtime fixes remain unvalidated by execution.
- The coverage workflow may require a small ReportGenerator path/report-type adjustment after first execution.
- P0 durable ingest, outbox concurrency, PostgreSQL locking, refresh-token drift and operation-identity decisions remain open.
- The broad audit is not proof that every backend branch is covered; the first numeric report is still required.

## Completion

88%

## Key commit SHAs

- `0282d1c95041fb2ceb1e5a573b057825020be079` — audit evidence start
- `e6eead3051c45d1d103042ecf5c927af17f3b90e` — ingest relational tests
- `2da42f6e44c89fd83b97b630a0a168504870655c` — bug endpoint auth fix
- `6d48b8d607ce9882d21c5a99f09190444bed04e5` — bug authorization tests with explicit anonymous branch
- `1cc603125758bb22c506ea3d77799033a33ebd04` — maintenance admin-policy fix
- `ec3dbc31a49c8555a6f330f46bdd814c1c3c27ac` — maintenance authorization tests with explicit anonymous branch
- `d62ae7d747d2dd4144ec0ca24bd73d8ac285c1fc` — explicit anonymous test-auth infrastructure
- `50799dd67def5ec08689e9dc8046b32675d109b7` — monitoring anonymous-test correction
- `3784a38048fd9772770f589c79ad3f0e0bd5205d` — coverage summary workflow
- `d0f6bf3548328f8586b17acb0bb83da50e3483a7` — coverage audit report
- `836d9ab016615d8e68aa55787bab2f7c3e5e35fa` — follow-up prompt queue
- `9a9f4b696b479d530ed50e0fb433e03dade6af0d` — mistake ledger
- `f455ee127ac62b3b5c08aad16ee54860088d8cfb` — endpoint inventory
- `3a97eaa2c1ecaeb4274bf0ac36d84403ab8ed589` — docs index
- `c431804ef5bbc2c86d6f8b4da9ec2ab41eec75c7` — central queue reconciliation

## Cross-repo sync

No mobile payload shape changed in the implemented fixes. Bug report submission was already handler-authenticated; middleware now enforces it explicitly. Maintenance is backend/admin only. BACKEND-TEST-013 and BACKEND-TEST-022 require explicit mobile sync if HTTP mutation semantics change.
