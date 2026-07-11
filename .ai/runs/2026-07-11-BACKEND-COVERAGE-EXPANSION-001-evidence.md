# BACKEND-COVERAGE-EXPANSION-001 Evidence

Prompt ID: BACKEND-COVERAGE-EXPANSION-001
Queue: `docs/prompt_queues/backend_test_coverage.md` (ad-hoc coverage expansion)
Agent/tool: ChatGPT via GitHub connector and GitHub Actions
Model provider: OpenAI
Model name/id: GPT-5.6 Thinking
Model mode/settings: reasoning, repository editing
Client/IDE: ChatGPT connector session
Run mode: test coverage expansion + validation
Token budget: high
Actual context: backend source/test inventory, pure logic, middleware, endpoint and service coverage gaps
Started from queue status: ad-hoc user request
Local collision check: central queue and latest follow-up queue inspected before allocating this prompt ID
Relevant prior mistakes read: BACKEND-MISTAKE-EVIDENCE-001, BACKEND-MISTAKE-VALIDATION-001, BACKEND-MISTAKE-VALIDATION-002, BACKEND-MISTAKE-QUEUE-001
How this run avoids prior mistakes: evidence was created before tests; additions target previously uncovered deterministic behavior; focused tests were executed in GitHub Actions; no full-suite or schema-green claim is made where evidence remains red
Elapsed time: unknown-not-recorded
Phase time breakdown: unknown-not-recorded

## Goal

Identify high-value missing backend tests, add as much safe coverage as practical without changing mobile contracts or business behavior, validate the new package, collect a repository-wide coverage baseline, and publish the work to `main`.

## Files inspected

- `AGENTS.md`
- `docs/prompt_queues/backend_test_coverage.md`
- `docs/ai/learning/MISTAKE_LEDGER.md`
- `tests/MathLearning.Tests/MathLearning.Tests.csproj`
- existing tests for admin validation, BKT, retry, API results and log redaction
- source files corresponding to every added test package
- `.github/workflows/database-validation.yml`
- `scripts/db/validate-schema.ps1`
- relevant migration chain and test infrastructure after executable failures exposed them

## Files changed

### Test files added

- `tests/MathLearning.Tests/Admin/DeleteImpactFormatterTests.cs`
- `tests/MathLearning.Tests/Admin/QuestionEditorModelTests.cs`
- `tests/MathLearning.Tests/Admin/ReturnUrlSanitizerTests.cs`
- `tests/MathLearning.Tests/Endpoints/EndpointUserTests.cs`
- `tests/MathLearning.Tests/Middleware/CorrelationIdMiddlewareTests.cs`
- `tests/MathLearning.Tests/Middleware/SafeClientErrorResponseTests.cs`
- `tests/MathLearning.Tests/Services/AdminQuestionValidationAdditionalTests.cs`
- `tests/MathLearning.Tests/Services/StreakRollerTests.cs`

### Existing tests expanded

- `tests/MathLearning.Tests/Models/ApiResultTests.cs`
- `tests/MathLearning.Tests/Services/BktServiceTests.cs`
- `tests/MathLearning.Tests/Services/LogOutputRedactorTests.cs`
- `tests/MathLearning.Tests/Services/RetryPolicyTests.cs`

### Test infrastructure repairs required to compile the existing suite

- `src/MathLearning.Api/ProgramPartial.cs` — exposes the generated top-level `Program` type for `WebApplicationFactory<Program>` integration tests; no runtime behavior changed.
- `tests/MathLearning.Tests/GlobalTestAliases.cs` — preserves the current idempotency-ledger status name used by older tests.
- `tests/MathLearning.Tests/Services/SyncServiceTests.cs` — uses explicit global domain namespace references to avoid collision with `MathLearning.Tests.Domain`.

### Evidence

- `.ai/runs/2026-07-11-BACKEND-COVERAGE-EXPANSION-001-evidence.md`

A temporary diagnostics workflow was used only on the feature branch to obtain complete build/test/coverage artifacts and was deleted before merge.

## Coverage added

The focused package covers 12 test classes and **107 executable test cases** across:

1. local return-URL validation, malformed/external inputs and query/fragment behavior;
2. category/question delete-impact messages;
3. authenticated endpoint `userId` claim extraction and missing/duplicate claims;
4. streak continuation, freeze consumption, insufficient-freeze reset and date boundaries;
5. correlation-id request/response behavior and generated identifiers;
6. safe client-error trace/correlation metadata and exception logging;
7. retry-policy success, transient timeout, task cancellation, EF transient error, disabled DB retry, exhaustion, request cancellation and retry-after metadata;
8. `ApiResult<T>` success/failure/rate-limit contracts and integer/date `Retry-After` parsing;
9. email, bearer token, secret assignment and multi-line log redaction;
10. BKT parameter caching, defaults, correct/incorrect update math and clamping;
11. admin question validation nulls, unknown type, option shape, exact limits, hint/step limits and LaTeX errors;
12. question-editor default-option independence and initial shape.

## Commands / workflows run

GitHub Actions validation for branch head `80c7dc2cc1bcd5e79cad8428c812cd195a594458`:

- workflow: `Coverage Expansion Diagnostics`
- run id: `29145382740`
- build: successful
- focused command equivalent:

```text
dotnet test tests/MathLearning.Tests/MathLearning.Tests.csproj \
  -c Release --no-build \
  --filter "FullyQualifiedName~ReturnUrlSanitizerTests|FullyQualifiedName~DeleteImpactFormatterTests|FullyQualifiedName~EndpointUserTests|FullyQualifiedName~StreakRollerTests|FullyQualifiedName~SafeClientErrorResponseTests|FullyQualifiedName~CorrelationIdMiddlewareTests|FullyQualifiedName~RetryPolicyTests|FullyQualifiedName~ApiResultTests|FullyQualifiedName~LogOutputRedactorTests|FullyQualifiedName~BktServiceTests|FullyQualifiedName~AdminQuestionValidationAdditionalTests|FullyQualifiedName~QuestionEditorModelTests"
```

- full coverage command equivalent:

```text
dotnet test tests/MathLearning.Tests/MathLearning.Tests.csproj \
  -c Release --no-build \
  --logger "trx;LogFileName=mathlearning-tests.trx" \
  --results-directory artifacts/test-results \
  --collect:"XPlat Code Coverage" \
  --settings tests/MathLearning.Tests/coverage.runsettings
```

## Validation results

### Focused new/expanded package

- Passed: **107**
- Failed: **0**
- Skipped: **0**
- Total: **107**

### Repository-wide suite

- Passed: **960**
- Failed: **35**
- Skipped: **0**
- Total: **995**

The full-suite failures are not attributed to the focused coverage package. The dominant existing failure group is relational integration setup against SQLite, where PostgreSQL-specific default SQL causes `SQLite Error 1: near "AT": syntax error` during `EnsureCreated`. Other existing failures include stale safe-error message expectations, an equation-step expectation, an observability route-metadata test and an admin API error assertion.

### Measured coverage baseline after this package

- Line coverage: **56.9%** — 16,572 / 29,088 coverable lines
- Branch coverage: **41.8%** — 3,271 / 7,816 branches
- Assemblies: 7
- Classes: 537
- Files: 311

Representative resulting class coverage:

- `ReturnUrlSanitizer`: 100% line/branch
- `DeleteImpactFormatter`: 100% line/branch
- `QuestionEditorModel`: 100% line
- `QuestionEditorValidation`: 100% line, 95% branch
- `StreakRoller`: 100% line/branch
- `RefreshTokenService`: 100% line/branch from combined existing coverage
- `SrsService`: 100% line, 93.7% branch from combined existing coverage

## Standard workflow status

The repository `Database Validation` workflow now passes restore and build after the test-infrastructure repairs, but it still fails before the normal test step during schema-from-zero migration.

Confirmed pre-existing migration-chain defect:

- `20260309091241_AddCosmeticSystem` creates foreign keys through raw PostgreSQL SQL, resulting in provider-generated constraint names.
- `20260624133144_AlignCosmeticsMobileDataModel` later attempts to drop hard-coded EF constraint names.
- PostgreSQL returns `42704` because `FK_user_avatar_configs_UserProfiles_UserId` does not exist under that name on a clean database.

This migration repair was deliberately not mixed into the coverage package because it requires a separate historical-migration safety review for clean databases and already-upgraded databases.

## What was done

- Audited existing tests before adding coverage to avoid blind duplication.
- Added high-density deterministic tests with minimal external dependencies.
- Triggered executable build/test/coverage runs through GitHub Actions because local checkout/.NET execution was unavailable.
- Repaired three existing test-project compilation blockers exposed by the first executable build.
- Corrected two initial test expectations after executable evidence showed the actual documented behavior.
- Collected TRX, Cobertura, JSON and HTML/Markdown coverage artifacts.
- Removed the temporary diagnostics workflow before merge.

## What was missed / intentionally deferred

- The full suite is not green because 35 existing tests remain blocked or stale.
- The clean PostgreSQL migration chain is not green because of the cosmetics FK naming mismatch.
- No claim is made that 56.9% is an increase from a previously measured comparable baseline; the repository did not have a checked baseline artifact linked to the starting SHA in this run.
- Large untested services such as school leaderboard aggregation, local screenshot storage, offline bundle processing and several UI components remain future coverage opportunities.

## Waste categories

- connector-only repository access;
- no local GitHub CLI;
- no outbound DNS for clone;
- standard workflow truncates logs before compiler details;
- schema failure prevented the standard workflow from reaching tests.

## Mistakes observed

- BACKEND-MISTAKE-VALIDATION-001 — many committed tests had not been executed against the current test project.
- BACKEND-MISTAKE-VALIDATION-002 — test-provider behavior was being treated as relational proof despite PostgreSQL-specific model SQL breaking SQLite setup.
- BACKEND-MISTAKE-EVIDENCE-001 — a coverage percentage should not be claimed without retaining the ReportGenerator artifact.

## Where time/context was wasted

- Initial workflow runs were needed to expose pre-existing test-project compilation blockers.
- A temporary diagnostics workflow was required because the standard schema gate stops before tests and its connector log response truncated the compiler errors.

## Why waste happened

- The execution environment cannot clone GitHub and has no `gh` CLI.
- The repository's standard workflow couples schema-from-zero, full tests and coverage serially, so one migration defect hides later validation.

## What the next agent should avoid

- Do not weaken or skip the schema gate merely to obtain a green badge.
- Do not mark the full test suite validated from the 107-test focused result.
- Do not bulk-edit 31 SQLite tests individually; fix the provider-aware test model/factory or move provider-sensitive tests to the PostgreSQL lane.
- Do not repair the historical cosmetics migration without proving both clean-database and already-upgraded-database behavior.

## Queue / follow-up

Recommended next packages:

1. repair the cosmetics migration FK-name mismatch and run schema-from-zero plus idempotent migration generation;
2. make relational test fixtures provider-aware so PostgreSQL-only default SQL does not break SQLite `EnsureCreated`, or move those suites to explicit PostgreSQL fixtures;
3. reconcile stale generic-error assertions with the canonical safe-error contract;
4. add coverage for leaderboard ranking/scoring utilities, school aggregation, screenshot storage and offline bundle behavior after provider infrastructure is stable.

## Completion %

- **92%**

The focused coverage package and build are validated. Completion is capped because the repository-wide suite and standard schema workflow are not fully green.

## Commit SHAs

Key commits include:

- `00e3e5d32a2941f0c4ad525979092bc9e0b841bd` — start evidence;
- `1733081c854001119e52dd7ead1f7996565d0cd0` — expose top-level Program for integration tests;
- `11ee3d16bb5802346cc51003aff3769b166a91d0` — fix sync-test domain namespace references;
- `5056fa8370056bd94b5f008e81492d1a085b817a` — correct return-URL contract expectation;
- `d73fe4f4db85fd40f62ec74bec742d2c6d77d4f3` — correct combined token-redaction expectation;
- `49dd91666e51f3e87bd59130206d94f6c33e09a4` — remove temporary diagnostics workflow;
- `36091819e7b55beaa5e16306c4ee6438299bca71` — retain only required test alias.

Final merge SHA: pending.

## Cross-repo sync

Not required. No mobile request/response payload or retry/conflict contract changed.
