# BACKEND-COVERAGE-EXPANSION-001 Evidence

Prompt ID: BACKEND-COVERAGE-EXPANSION-001
Queue: `docs/prompt_queues/backend_test_coverage.md` (ad-hoc coverage expansion)
Agent/tool: ChatGPT via GitHub connector and GitHub Actions
Model provider: OpenAI
Model name/id: GPT-5.6 Thinking
Run mode: test coverage expansion + validation
Started from queue status: ad-hoc user request
Relevant prior mistakes read: BACKEND-MISTAKE-EVIDENCE-001, BACKEND-MISTAKE-VALIDATION-001, BACKEND-MISTAKE-VALIDATION-002, BACKEND-MISTAKE-QUEUE-001

## Goal

Identify high-value missing MathLearning backend tests, add as much safe coverage as practical without changing mobile contracts or business behavior, validate the new package, collect a repository-wide coverage baseline, and publish the work to `main`.

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

### Existing test-project blockers repaired

- `src/MathLearning.Api/ProgramPartial.cs` — exposes the generated top-level `Program` type for `WebApplicationFactory<Program>`; no runtime behavior changed.
- `tests/MathLearning.Tests/GlobalTestAliases.cs` — preserves the current idempotency-ledger status name used by older tests.
- `tests/MathLearning.Tests/Services/SyncServiceTests.cs` — uses explicit global domain namespace references to avoid collision with `MathLearning.Tests.Domain`.

### Evidence

- `.ai/runs/2026-07-11-BACKEND-COVERAGE-EXPANSION-001-evidence.md`

A temporary diagnostics workflow was used only on the feature branch and deleted before merge.

## Coverage added

The focused package contains **107 executable tests** across 12 test classes covering:

1. local return URL validation and malformed/external inputs;
2. category/question delete-impact messages;
3. endpoint `userId` claim extraction;
4. streak continuation, freezes and resets;
5. correlation ID middleware;
6. safe client-error trace/correlation metadata;
7. retry-policy success, transient failures, cancellation, exhaustion and retry-after behavior;
8. `ApiResult<T>` and `Retry-After` parsing;
9. email, token, secret and multi-line log redaction;
10. BKT caching, update math and clamping;
11. admin question validation boundaries and LaTeX errors;
12. question-editor default shape and independent option collections.

## Executable validation

GitHub Actions branch validation:

- workflow: `Coverage Expansion Diagnostics`
- run id: `29145382740`
- validated branch head: `80c7dc2cc1bcd5e79cad8428c812cd195a594458`
- Release build: successful
- focused tests: **107 passed, 0 failed, 0 skipped**
- coverage report generation: successful
- diagnostic artifact id: `8246596984`

Focused command equivalent:

```text
dotnet test tests/MathLearning.Tests/MathLearning.Tests.csproj \
  -c Release --no-build \
  --filter "FullyQualifiedName~ReturnUrlSanitizerTests|FullyQualifiedName~DeleteImpactFormatterTests|FullyQualifiedName~EndpointUserTests|FullyQualifiedName~StreakRollerTests|FullyQualifiedName~SafeClientErrorResponseTests|FullyQualifiedName~CorrelationIdMiddlewareTests|FullyQualifiedName~RetryPolicyTests|FullyQualifiedName~ApiResultTests|FullyQualifiedName~LogOutputRedactorTests|FullyQualifiedName~BktServiceTests|FullyQualifiedName~AdminQuestionValidationAdditionalTests|FullyQualifiedName~QuestionEditorModelTests"
```

## Repository-wide baseline

The complete suite was also executed to collect honest baseline evidence:

- Passed: **960**
- Failed: **35**
- Skipped: **0**
- Total: **995**

Measured coverage after this package:

- Line coverage: **56.9%** — 16,572 / 29,088
- Branch coverage: **41.8%** — 3,271 / 7,816
- Assemblies: 7
- Classes: 537
- Files: 311

Representative results:

- `ReturnUrlSanitizer`: 100% line/branch
- `DeleteImpactFormatter`: 100% line/branch
- `QuestionEditorModel`: 100% line
- `QuestionEditorValidation`: 100% line, 95% branch
- `StreakRoller`: 100% line/branch

No claim is made about the percentage delta from a comparable starting baseline because no checked ReportGenerator artifact was linked to the starting SHA.

## Remaining full-suite failures

The focused 107-test package is green. The remaining 35 repository-wide failures are existing infrastructure/contract issues, dominated by relational integration setup against SQLite where PostgreSQL-specific default SQL causes:

```text
SQLite Error 1: near "AT": syntax error
```

Other existing failures include stale safe-error text expectations, an equation-step expectation, an observability route-metadata assertion and an admin API error assertion.

## Standard Database Validation workflow

The standard workflow now passes restore and build after the test-project repairs, but still fails before its normal test step during schema-from-zero migration.

Confirmed pre-existing migration-chain defect:

- `20260309091241_AddCosmeticSystem` creates foreign keys with raw PostgreSQL SQL and provider-generated names.
- `20260624133144_AlignCosmeticsMobileDataModel` attempts to drop hard-coded EF constraint names.
- PostgreSQL returns `42704` because `FK_user_avatar_configs_UserProfiles_UserId` does not exist under that name on a clean database.

The historical migration repair was intentionally kept out of this coverage package because it needs separate clean-database and already-upgraded-database proof.

## Mistakes observed

- BACKEND-MISTAKE-VALIDATION-001 — committed tests had accumulated without current executable validation.
- BACKEND-MISTAKE-VALIDATION-002 — SQLite execution was being treated as relational confidence even though PostgreSQL-specific model SQL prevents setup.
- BACKEND-MISTAKE-EVIDENCE-001 — coverage must be supported by retained ReportGenerator artifacts.

## Recommended follow-ups

1. Repair the cosmetics migration FK-name mismatch and validate clean plus upgraded PostgreSQL databases.
2. Make relational test fixtures provider-aware or move provider-sensitive suites to an explicit PostgreSQL fixture.
3. Reconcile stale safe-error assertions with the canonical public error contract.
4. Add tests for leaderboard ranking/scoring utilities, school aggregation, screenshot storage and offline bundle processing.

## Completion

**92%** — focused package and build validated; capped because the repository-wide suite and standard schema workflow remain red for documented pre-existing reasons.

## Publication

- Pull request: `#4` — `Expand MathLearning backend test coverage`
- Squash merge to `main`: `5f3cca6e743c66e2d17a303990071a0ed7238ed2`
- Evidence finalization commit: recorded by the commit containing this update.

## Cross-repo sync

Not required. No mobile request/response payload or retry/conflict contract changed.
