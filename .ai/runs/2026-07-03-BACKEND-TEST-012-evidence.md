# BACKEND-TEST-012 Evidence

Prompt ID: BACKEND-TEST-012
Queue: `docs/prompt_queues/backend_test_coverage.md`
Agent/tool: ChatGPT via GitHub connector
Model provider: OpenAI
Model name/id: GPT-5.5 Thinking
Model mode/settings: reasoning, repository-editing
Client/IDE: ChatGPT web
Run mode: bugfix investigation
Token budget: medium
Actual context: refresh-token generator, EF model metadata, migration history, model snapshot, relational persistence safety
Started from queue status: Ready / P0-P1
Local collision check: current main and latest backend test queue inspected; BACKEND-TEST-014 and BACKEND-TEST-015 do not overlap the model/snapshot correction
Relevant prior mistakes read: BACKEND-MISTAKE-AUTH-001, BACKEND-MISTAKE-EVIDENCE-001, BACKEND-MISTAKE-VALIDATION-001
How this run avoids prior mistakes: evidence created before edits; existing migration is treated as source of intended schema truth; no redundant migration or unsafe full-file rewrite; no passing build/test claim without executable evidence
Elapsed time: unknown-not-recorded
Phase time breakdown: unknown-not-recorded

## Backend regression guardrails

Historical bug class protected: `schema-migration-drift`, `auth-user-scope`, `compiler-warning-cleanliness`
Why this change can reintroduce it: changing only the generator, model, snapshot, or migration leaves schema-from-zero and production-upgraded databases inconsistent
Files inspected: refresh token service/tests, EF model configuration, model snapshot, existing length migration, test DbContext factory, mistake ledger and coverage queue
Tests/validation planned: EF metadata regression test, generator-to-model fit assertion, SQLite relational persistence test, schema-from-zero validation command, focused auth test command
Contract/schema/docs touched: queue and evidence only in this run; runtime model was intentionally not rewritten unsafely
Residual risk if validation cannot run: generated-token persistence and future migration behavior remain at risk until the targeted patch is applied and validated

## Files inspected

- `docs/prompt_queues/backend_test_coverage.md`
- `docs/ai/learning/MISTAKE_LEDGER.md`
- `src/MathLearning.Infrastructure/Persistance/ApiDbContext.cs`
- `src/MathLearning.Infrastructure/Migrations/Api/ApiDbContextModelSnapshot.cs`
- `src/MathLearning.Infrastructure/Migrations/Api/20260210114958_IncreaseRefreshTokenLength.cs`
- `src/MathLearning.Infrastructure/Services/RefreshTokenService.cs`
- `tests/MathLearning.Tests/Services/RefreshTokenServiceSecurityTests.cs`
- `tests/MathLearning.Tests/Helpers/TestDbContextFactory.cs`

## Files changed

- `docs/prompt_queues/backend_test_coverage.md`
- `.ai/runs/2026-07-03-BACKEND-TEST-012-evidence.md`

## Commands run

- GitHub repository search and direct file inspection
- targeted line reads for `ApiDbContext` and `ApiDbContextModelSnapshot`
- direct repository checkout attempt in the execution container; blocked by GitHub DNS/network access

## What was done

- Confirmed `RefreshTokenService.GenerateRefreshToken()` emits Base64 for 64 cryptographic random bytes, which is 88 characters.
- Confirmed migration `20260210114958_IncreaseRefreshTokenLength` widened `RefreshTokens.Token` from 64 to 128.
- Confirmed the current runtime EF fluent model still declares `HasMaxLength(64)`.
- Confirmed the current `ApiDbContextModelSnapshot` still declares max length 64 and `character varying(64)`.
- Confirmed this drift can reject generated tokens under relational enforcement or generate a future migration that shrinks an already widened production column.
- Updated the central test queue with the exact safe two-line model/snapshot correction, metadata test, relational persistence test, and validation commands required.
- Avoided creating a redundant migration because the intended 128-column migration already exists.

## What was missed

- The runtime model and snapshot were not modified in this connector session.
- The metadata and relational persistence regression tests were not added because they would correctly fail until the model patch lands.
- Schema-from-zero and migration-diff validation did not run.

## Validation run

- Static four-way consistency comparison: generator, migration, runtime EF model, current snapshot.
- Verified the mismatch is concrete rather than hypothetical: 88-character generated value versus 64-character model maximum.

## Validation not run

- `dotnet test tests/MathLearning.Tests/MathLearning.Tests.csproj --filter "RefreshToken|AuthRefresh"` — not run because the execution environment has no .NET SDK/repository checkout.
- `dotnet ef migrations has-pending-model-changes` — not run for the same reason.
- `./scripts/db/validate-schema.ps1` — not run for the same reason.
- PostgreSQL schema-from-zero — not run.

## Waste categories

- connector complete-file replacement limitation
- execution environment network/DNS limitation

## Mistakes observed

- BACKEND-MISTAKE-AUTH-001 confirmed as still open.

## Where time/context was wasted

- The available GitHub contents action supports complete-file replacement only. `ApiDbContext.cs` is approximately 1,600 lines and the model snapshot approximately 5,800 lines, so a two-line correction would require a high-risk broad rewrite through this interface.
- A local clone was attempted once but the environment could not resolve GitHub.

## Why waste happened

- No targeted patch action or authenticated repository checkout is available in this connector environment.

## What the next agent should avoid

- Do not create a redundant migration that re-widens a column already widened by `20260210114958_IncreaseRefreshTokenLength`.
- Do not add a passing test that ignores current EF metadata; the regression test must assert the actual configured maximum.
- Do not rewrite the full model snapshot through a lossy or truncated connector response.
- Do not mark BACKEND-TEST-012 Done until model, snapshot, generator, migration history, schema-from-zero, and regression tests all agree on 128.

## Docs/rules updated to prevent repeat

- Central queue now records the exact patch and the connector limitation.
- Existing BACKEND-MISTAKE-AUTH-001 remains the blocking mistake card.

## Queue updated

- BACKEND-TEST-012: `Confirmed drift / Needs safe patch`.

## New optimized prompt added

- none; the queue entry itself contains the complete targeted local patch scope.

## Follow-up prompt

- In a local checkout, change `RefreshToken.Token` fluent max length from 64 to 128 and the current snapshot max length/type from 64 to 128. Add an EF metadata test asserting 128, assert generated token length fits that maximum, add SQLite/PostgreSQL persistence coverage, run pending-model-change and schema-from-zero validation, and do not add a new migration unless EF still reports a genuine model difference after the snapshot correction.

## Completion %

- 40%

## Residual risk

- Current EF metadata still conflicts with the token generator and existing migration history.
- Login, registration, or refresh-token persistence can fail under a relational schema/model path that enforces the 64-character configuration.
- A future generated migration can attempt to shrink the production column from 128 to 64.

## Commit SHAs

- `d5e622fc962c2c9ade7cd864b3fc66139f3d8e56` — start evidence and confirmed drift
- `a001698a1043c3477053c6d456c69d599072c62b` — queue reconciliation and safe patch instructions
- `e06dae4ac77b650b0ce87ab2fe90d729979cb7f5` — queue retained with relational auth follow-up

## Cross-repo sync

Cross-repo sync: not applicable; backend persistence metadata only.
Mobile docs touched: none
