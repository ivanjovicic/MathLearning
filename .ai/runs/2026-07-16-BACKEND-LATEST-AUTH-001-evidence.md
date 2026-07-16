# BACKEND-LATEST-AUTH-001 Evidence

Prompt ID: BACKEND-LATEST-AUTH-001
Queue: docs/prompt_queues/backend_latest_commit_followups_2026_07_01.md
Agent/tool: Codex / functions.exec_command
Model provider: unknown-not-exposed
Model name/id: unknown-not-exposed
Model mode/settings: unknown-not-exposed
Client/IDE: unknown-not-exposed
Run mode: implementation/test
Token budget: medium
Actual context: provider-aware auth verification for refresh-token and registration flows
Started from queue status: Prompt-ready after BACKEND-LATEST-EVIDENCE-001
Local collision check: no existing 2026-07-16 BACKEND-LATEST-AUTH-001 run log found
Relevant prior mistakes read:
- BACKEND-MISTAKE-AUDIT-001
- BACKEND-MISTAKE-EVIDENCE-001
- BACKEND-MISTAKE-VALIDATION-001
- BACKEND-MISTAKE-XREPO-001
How this run avoids prior mistakes:
- add a relational-provider test for the existing refresh-token race and preserve the mobile registration coverage instead of claiming InMemory-only proof as provider proof.
Elapsed time: unknown-not-recorded
Phase time breakdown: unknown-not-recorded

## Files inspected

- docs/BACKEND_SECOND_PASS_RISK_PREVENTION_RULES.md
- src/MathLearning.Api/Endpoints/AuthEndpoints.cs
- src/MathLearning.Infrastructure/Persistance/ApiDbContext.cs
- src/MathLearning.Infrastructure/Services/RefreshTokenService.cs
- src/MathLearning.Domain/Entities/RefreshToken.cs
- tests/MathLearning.Tests/Endpoints/AuthRefreshConcurrencyTests.cs
- tests/MathLearning.Tests/Endpoints/AuthMobileRegistrationAtomicityTests.cs
- tests/MathLearning.Tests/Helpers/CustomWebApplicationFactory.cs
- tests/MathLearning.Tests/Helpers/TestDbContextFactory.cs

## Files changed

- tests/MathLearning.Tests/Endpoints/AuthRefreshConcurrencyTests.cs
- .ai/runs/2026-07-16-BACKEND-LATEST-AUTH-001-evidence.md
- docs/prompt_queues/backend_latest_commit_followups_2026_07_01.md

## Commands run

- dotnet test tests/MathLearning.Tests/MathLearning.Tests.csproj --filter "AuthRefreshConcurrencyTests"
- dotnet test tests/MathLearning.Tests/MathLearning.Tests.csproj --filter "AuthRefreshConcurrencyTests|AuthRefreshEndpointRegressionTests|AuthMobileRegistrationAtomicityTests"

## What was done

- Inspected the existing refresh-token and mobile-registration auth tests against the second-pass provider-risk rules.
- Added a SQLite relational-provider variant of the refresh-token concurrency test using a minimal relational DbContext so the race is proven outside InMemory-only behavior.
- Confirmed the mobile registration atomicity tests already cover the rollback and compensating cleanup paths separately.

## What was missed

- No additional relational-provider registration test was added because the existing atomicity coverage already exercises the rollback and cleanup branches, which was enough to close the queue prompt without broadening scope.

## Validation run

- dotnet test tests/MathLearning.Tests/MathLearning.Tests.csproj --filter "AuthRefreshConcurrencyTests" — passed
- dotnet test tests/MathLearning.Tests/MathLearning.Tests.csproj --filter "AuthRefreshConcurrencyTests|AuthRefreshEndpointRegressionTests|AuthMobileRegistrationAtomicityTests" — passed

## Validation not run

- none

## Waste categories

- test rework
- provider gap investigation

## Mistakes observed

- none

## Where time/context was wasted

- The first SQLite attempt used the full `ApiDbContext`, which failed on PostgreSQL-specific DDL before the relational proof could run.

## Why waste happened

- The full backend model contains provider-specific schema that SQLite cannot execute verbatim.

## What the next agent should avoid

- Do not use the full `ApiDbContext` for SQLite relational smoke tests when the goal is only to prove refresh-token concurrency semantics.

## Docs/rules updated to prevent repeat

- docs/prompt_queues/backend_latest_commit_followups_2026_07_01.md

## Queue updated

- docs/prompt_queues/backend_latest_commit_followups_2026_07_01.md

## New optimized prompt added

- none

## Follow-up prompt

- none

## Completion %

- 95%

## Residual risk

- the relational proof uses a minimal SQLite context rather than the full production `ApiDbContext`, so provider coverage is strong but not a full schema-compatibility smoke test.

## Commit SHA

- uncommitted
