# BACKEND-TEST-012 Evidence

Prompt ID: BACKEND-TEST-012
Queue: docs/prompt_queues/backend_test_coverage.md
Agent/tool: Codex CLI
Model provider: unknown-not-exposed
Model name/id: unknown-not-exposed
Model mode/settings: unknown-not-exposed
Client/IDE: Codex CLI
Run mode: implementation
Token budget: unknown-not-exposed
Actual context: low
Started from queue status: Confirmed drift / Needs safe patch
Local collision check: git status already dirty with unrelated user/agent changes; avoid-path is MathLearning.Admin and unrelated in-flight backend files
Relevant prior mistakes read:
- BACKEND-MISTAKE-EVIDENCE-001
- BACKEND-MISTAKE-VALIDATION-001
- BACKEND-MISTAKE-AUTH-001
How this run avoids prior mistakes:
- inspect generator, EF mapping, snapshot and existing migration before editing
- avoid creating a redundant migration because the schema migration to 128 already exists
- add a focused regression test and run the narrowest useful test command before claiming queue progress
Elapsed time: unknown-not-recorded
Phase time breakdown: unknown-not-recorded

## Files inspected

- AGENTS.md
- docs/AGENT_SHARED_OPERATING_STANDARD.md
- docs/AGENT_RUN_LOG_ENFORCEMENT.md
- docs/BUGFIX_PATTERN_GUARDRAILS.md
- docs/ai/learning/MISTAKE_LEDGER.md
- .ai/RUN_LOG_TEMPLATE.md
- .ai/runs/README.md
- docs/prompt_queues/backend_test_coverage.md
- docs/prompt_queues/backend_latest_commit_followups_2026_07_11.md
- src/MathLearning.Infrastructure/Services/RefreshTokenService.cs
- src/MathLearning.Domain/Entities/RefreshToken.cs
- src/MathLearning.Infrastructure/Persistance/ApiDbContext.cs
- src/MathLearning.Infrastructure/Migrations/Api/ApiDbContextModelSnapshot.cs
- src/MathLearning.Infrastructure/Migrations/Api/20260210114958_IncreaseRefreshTokenLength.cs
- tests/MathLearning.Tests/Services/RefreshTokenServiceSecurityTests.cs
- tests/MathLearning.Tests/Endpoints/AuthRefreshRelationalConcurrencyTests.cs

## Files changed

- `.ai/runs/2026-07-14-BACKEND-TEST-012-evidence.md`
- `docs/prompt_queues/backend_test_coverage.md`
- `docs/prompt_queues/backend_latest_commit_followups_2026_07_11.md`
- `src/MathLearning.Infrastructure/Services/RefreshTokenService.cs`
- `src/MathLearning.Infrastructure/Persistance/ApiDbContext.cs`
- `src/MathLearning.Infrastructure/Migrations/Api/ApiDbContextModelSnapshot.cs`
- `tests/MathLearning.Tests/Services/RefreshTokenServiceSecurityTests.cs`

## Commands run

- `rg -n "BACKEND-TEST-012|refresh-token|RefreshToken" docs src tests`
- `dotnet test tests/MathLearning.Tests/MathLearning.Tests.csproj --filter "FullyQualifiedName~RefreshTokenServiceSecurityTests"` (timed out on first attempt)
- `dotnet build tests/MathLearning.Tests/MathLearning.Tests.csproj -c Release --no-restore` (timed out)
- `dotnet test tests/MathLearning.Tests/MathLearning.Tests.csproj -c Release --no-build --filter "FullyQualifiedName~RefreshTokenServiceSecurityTests"`
- `dotnet ef migrations has-pending-model-changes --project src/MathLearning.Infrastructure/MathLearning.Infrastructure.csproj --startup-project src/MathLearning.Api/MathLearning.Api.csproj --context ApiDbContext`
- `dotnet ef migrations has-pending-model-changes --project src/MathLearning.Infrastructure/MathLearning.Infrastructure.csproj --startup-project src/MathLearning.Api/MathLearning.Api.csproj --context ApiDbContext --no-build`
- `python scripts/validate_agent_evidence.py`
- `git diff --check -- .ai/runs/2026-07-14-BACKEND-TEST-012-evidence.md docs/prompt_queues/backend_test_coverage.md docs/prompt_queues/backend_latest_commit_followups_2026_07_11.md src/MathLearning.Infrastructure/Services/RefreshTokenService.cs src/MathLearning.Infrastructure/Persistance/ApiDbContext.cs src/MathLearning.Infrastructure/Migrations/Api/ApiDbContextModelSnapshot.cs tests/MathLearning.Tests/Services/RefreshTokenServiceSecurityTests.cs`
- `git rev-parse HEAD`

## What was done

- Confirmed the exact drift: generated refresh tokens are 88-character Base64 strings, the historical migration already widened the database column to 128, but the live EF mapping and snapshot still declared 64.
- Aligned `ApiDbContext` and `ApiDbContextModelSnapshot` to the existing 128-character schema target without adding a redundant migration.
- Clarified the generator comment so it describes the actual Base64 token shape instead of a hex-length assumption.
- Added a focused regression test that verifies generated tokens fit the configured EF max length and that the configured length is 128.
- Marked `BACKEND-TEST-012` as validated in the central queue and removed it from the latest follow-up execution order.

## What was missed

- No new commit was created because the repository already had unrelated in-flight changes and this run did not isolate a clean commit boundary.
- No broader auth endpoint or relational refresh-rotation suite was rerun because this prompt only owned the generator/model/snapshot drift.

## Validation run

- `dotnet test tests/MathLearning.Tests/MathLearning.Tests.csproj -c Release --no-build --filter "FullyQualifiedName~RefreshTokenServiceSecurityTests"` — passed (8 passed, 0 failed, 0 skipped).
- `dotnet ef migrations has-pending-model-changes --project src/MathLearning.Infrastructure/MathLearning.Infrastructure.csproj --startup-project src/MathLearning.Api/MathLearning.Api.csproj --context ApiDbContext --no-build` — passed (`No changes have been made to the model since the last migration.`).
- `git diff --check -- .ai/runs/2026-07-14-BACKEND-TEST-012-evidence.md docs/prompt_queues/backend_test_coverage.md docs/prompt_queues/backend_latest_commit_followups_2026_07_11.md src/MathLearning.Infrastructure/Services/RefreshTokenService.cs src/MathLearning.Infrastructure/Persistance/ApiDbContext.cs src/MathLearning.Infrastructure/Migrations/Api/ApiDbContextModelSnapshot.cs tests/MathLearning.Tests/Services/RefreshTokenServiceSecurityTests.cs` — passed with LF-to-CRLF warnings only.

## Validation not run

- `dotnet build tests/MathLearning.Tests/MathLearning.Tests.csproj -c Release --no-restore` — not run to completion; command timed out in this environment and was replaced by the narrower passing test command.
- `python scripts/validate_agent_evidence.py` — ran and failed due large pre-existing repository-wide queue/run-log evidence debt outside this prompt's scope.
- CI: No GitHub Actions evidence found via connector.

## Waste categories

- Initial command timeout while the test project/build warmed up.
- Repository-wide evidence validator failures unrelated to this prompt.

## Mistakes observed

- none

## Where time/context was wasted

- On the first `dotnet test`/`dotnet build` attempts that timed out before the narrower no-build validation was used.
- On repository-wide evidence findings unrelated to refresh-token drift.

## Why waste happened

- The local environment needed a more targeted no-build validation path for a small metadata fix.
- The required evidence validator scans legacy queue/run-log debt across the repo, not only this prompt's touched files.

## What the next agent should avoid

- Do not add a second refresh-token length migration; the schema widening already exists.
- Do not treat `BACKEND-TEST-012` as an at-rest token security fix; raw token storage is still owned by `BACKEND-API-DB-007`.
- Prefer a narrow no-build validation command first when the owned change is metadata/test only.

## Docs/rules updated to prevent repeat

- `docs/prompt_queues/backend_test_coverage.md`
- `docs/prompt_queues/backend_latest_commit_followups_2026_07_11.md`

## Queue updated

- `BACKEND-TEST-012` marked `Validated` in `docs/prompt_queues/backend_test_coverage.md`.
- Latest follow-up execution order no longer lists `BACKEND-TEST-012` as pending.

## New optimized prompt added

- none

## Follow-up prompt

- `BACKEND-TEST-032`

## Completion %

- 95%

## Residual risk

- Refresh tokens are still stored as reusable raw bearer secrets at rest; that risk remains with `BACKEND-API-DB-007` even though the 64/88/128 length drift is now closed.

## Commit SHA

- `ae603c29afc9fa3cba19b5122c1f0cf0f67516c1` (current HEAD; no new commit created in this run)
