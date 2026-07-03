# BACKEND-TEST-015 Evidence

Prompt ID: BACKEND-TEST-015
Queue: `docs/prompt_queues/backend_test_coverage.md`
Agent/tool: ChatGPT via GitHub connector
Model provider: OpenAI
Model name/id: GPT-5.5 Thinking
Model mode/settings: reasoning, repository-editing
Client/IDE: ChatGPT web
Run mode: relational HTTP integration tests
Token budget: medium
Actual context: real `/auth/refresh` concurrent rotation against file-backed SQLite and EF concurrency-token SQL behavior
Started from queue status: new risk discovered during coverage reconciliation
Local collision check: existing auth refresh tests validated InMemory concurrency and sequential endpoint reuse; no relational HTTP refresh race test found
Relevant prior mistakes read: BACKEND-MISTAKE-AUTH-001, BACKEND-MISTAKE-EVIDENCE-001, BACKEND-MISTAKE-VALIDATION-001
How this run avoids prior mistakes: invoke the real endpoint, coordinate both requests before database write without sleeps, use separate request scopes/DbContexts, and avoid claiming PostgreSQL equivalence
Elapsed time: unknown-not-recorded
Phase time breakdown: unknown-not-recorded

## Backend regression guardrails

Historical bug class protected: `auth-user-scope`, `schema-migration-drift`
Why this change can reintroduce it: EF InMemory concurrency tests do not prove relational update predicates, transaction rollback of the losing child token, or actual endpoint exception mapping
Files inspected: AuthEndpoints refresh handler, CustomWebApplicationFactory, existing auth refresh concurrency/regression tests, RefreshToken EF concurrency configuration
Tests/validation planned: real HTTP login, two coordinated refresh requests, one 200/one 401, exactly one active descendant and no third token
Contract/schema/docs touched: test-only plus queue/evidence
Residual risk if validation cannot run: SQLite test compile/runtime and provider-specific PostgreSQL concurrency remain unproven

## Files inspected

- `src/MathLearning.Api/Endpoints/AuthEndpoints.cs`
- `src/MathLearning.Api/Startup/TestAccountSeeder.cs`
- `tests/MathLearning.Tests/Endpoints/AuthRefreshConcurrencyTests.cs`
- `tests/MathLearning.Tests/Helpers/CustomWebApplicationFactory.cs`
- `.ai/runs/2026-07-01-BACKEND2-CRIT-002-evidence.md`

## Files changed

- `tests/MathLearning.Tests/Endpoints/AuthRefreshRelationalConcurrencyTests.cs`
- `docs/prompt_queues/backend_test_coverage.md`
- `.ai/runs/2026-07-03-BACKEND-TEST-015-evidence.md`

## Commands run

- GitHub repository search and direct file inspection
- GitHub fetch of committed test file for static review
- GitHub combined-status lookup for commit `110f7d60aa0a0f353cb500e18843e19f07ca2d17`

## What was done

- Added a real HTTP test that logs in through `/api/auth/login` and races two `/auth/refresh` calls using the same active token.
- Replaced the API test database with file-backed SQLite while retaining the repository's normal WebApplicationFactory, Identity, JWT, endpoint, and seeding behavior.
- Added a `SaveChangesInterceptor` coordinator that activates only for refresh-token rotation saves.
- Guaranteed both requests load the original active token before either relational save, without sleeps or probabilistic timing.
- Allowed the first writer to complete before releasing the second, forcing the second UPDATE through the `RevokedAt` concurrency predicate.
- Asserted exactly one `200 OK` and one `401 Unauthorized` with the safe public error message.
- Asserted the losing rotation does not leave its newly added refresh token behind: two total tokens, one active descendant, original token revoked.
- Added BACKEND-TEST-015 to the central test queue with the exact focused validation command and PostgreSQL follow-up.

## What was missed

- No executable .NET test result was available in this connector environment.
- PostgreSQL-specific concurrency behavior remains pending the database-validation workflow.
- Refresh-token model length drift under BACKEND-TEST-012 remains unresolved and independent from this concurrency test.

## Validation run

- Static source-to-endpoint review.
- Confirmed the endpoint catches `DbUpdateConcurrencyException` and maps the losing rotation to `401 Unauthorized`.
- Confirmed the custom factory follows the existing service-replacement and test-account seeding patterns.
- Confirmed each concurrent HTTP request receives its own scoped `ApiDbContext` while sharing the same SQLite database.
- Confirmed the coordinator only blocks contexts with a modified `RefreshToken.RevokedAt` property, so login and test seeding are not intercepted.
- GitHub combined statuses for the test commit: none returned.

## Validation not run

- `dotnet test tests/MathLearning.Tests/MathLearning.Tests.csproj --filter "FullyQualifiedName~AuthRefreshRelationalConcurrencyTests"` — not run because the execution environment has no .NET SDK/repository checkout.
- Release build — not run for the same reason.
- PostgreSQL database-validation workflow — no workflow status was exposed for the checked commit.

## Waste categories

- connector execution limitation

## Mistakes observed

- none

## Where time/context was wasted

- The existing auth concurrency evidence had to be re-read to distinguish InMemory coverage from relational endpoint coverage.

## Why waste happened

- The original concurrency test correctly protected fast behavior but its provider limitation was not represented as a separate test-coverage queue item.

## What the next agent should avoid

- Do not replace the coordinator with delays.
- Do not test only direct EF saves; keep the real HTTP endpoint and response mapping in scope.
- Do not claim SQLite proves PostgreSQL concurrency behavior.
- Do not mark BACKEND-TEST-015 Done until the focused test executes successfully.

## Docs/rules updated to prevent repeat

- Added BACKEND-TEST-015 with a clear distinction between InMemory fast tests and relational endpoint proof.
- Recorded the exact losing-token rollback invariant.

## Queue updated

- BACKEND-TEST-015: Implemented / Needs validation.

## New optimized prompt added

- none

## Follow-up prompt

- Run the focused relational auth test and existing `AuthRefreshConcurrencyTests|AuthRefreshEndpointRegressionTests` filter. Then run the PostgreSQL-backed database validation workflow and record the provider-specific result.

## Completion %

- 88%

## Residual risk

- The new test is committed but not compiled or executed in this environment.
- SQLite validates relational concurrency-token and transaction behavior, but PostgreSQL remains the production authority.
- The separate 64-vs-128 refresh-token model drift can still break schema-from-zero or generated-token persistence.

## Commit SHAs

- `0b6abd2a31a7c6f2a3edbdad2250155561ae7e91` — start evidence
- `110f7d60aa0a0f353cb500e18843e19f07ca2d17` — relational HTTP refresh race test
- `e06dae4ac77b650b0ce87ab2fe90d729979cb7f5` — queue registration and validation command

## Cross-repo sync

Cross-repo sync: not applicable; existing backend auth contract is unchanged.
Mobile docs touched: none
