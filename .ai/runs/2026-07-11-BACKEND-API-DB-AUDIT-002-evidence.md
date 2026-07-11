# BACKEND-API-DB-AUDIT-002 Evidence

Prompt ID: BACKEND-API-DB-AUDIT-002  
Queue: `docs/prompt_queues/backend_api_db_residuals_pass2_2026_07_11.md`  
Agent/tool: ChatGPT with GitHub connector  
Model provider: OpenAI  
Model name/id: GPT-5.6 Thinking  
Model mode/settings: reasoning, connector-only static repository audit and documentation publication  
Client/IDE: ChatGPT connector session  
Run mode: second-pass backend API/database audit; no runtime implementation  
Token budget: high  
Actual context: remaining economy/cosmetics, hint/power-up compatibility, leaderboard, auth registration, photo avatar, idempotency services, current queues and latest main repair evidence  
Started from queue status: ad-hoc user request to audit the remaining backend and push prompts to main  
Starting main SHA: `6cdff4c7fbeb595ed29fc11b4641d7b9fe488100`  
Local collision check: exact `BACKEND-API-DB-009`, `010` and `016` searches plus central/failing-test/performance queues were inspected before allocating `009…015`; existing `BACKEND-MIGRATION-001` was reused rather than duplicated  
Relevant prior mistakes read: BACKEND-MISTAKE-EVIDENCE-001, BACKEND-MISTAKE-AUDIT-001, BACKEND-MISTAKE-VALIDATION-001, BACKEND-MISTAKE-XREPO-001, BACKEND-MISTAKE-IDEM-001, BACKEND-MISTAKE-IDEM-002, BACKEND-MISTAKE-QUEUE-001, BACKEND-MISTAKE-PERF-002  
How this run avoids prior mistakes: findings remain explicitly static; prompt IDs were collision-checked; existing migration/provider/idempotency owners are linked rather than copied; each contract prompt requires mobile synchronization; no new runtime or test-green claim is made  
Elapsed time: unknown-not-recorded  
Phase time breakdown: unknown-not-recorded

## Files inspected

- `AGENTS.md`
- `docs/DOCS_INDEX.md`
- `docs/prompt_queues/backend_test_coverage.md`
- `docs/prompt_queues/backend_api_db_residuals_2026_07_11.md`
- `docs/prompt_queues/backend_failing_test_followups_2026_07_11.md`
- `docs/BACKEND_PERFORMANCE_BUG_AUDIT_2026_07_03.md`
- `docs/ai/learning/MISTAKE_LEDGER.md`
- `.ai/runs/2026-07-02-BACKEND-TEST-CORE-001-evidence.md`
- `.ai/runs/2026-07-11-BACKEND-FAILING-TESTS-001-evidence.md`
- `src/MathLearning.Api/Endpoints/CoinEndpoints.cs`
- `src/MathLearning.Api/Endpoints/HintEndpoints.cs`
- `src/MathLearning.Api/Endpoints/PowerupEndpoints.cs`
- `src/MathLearning.Api/Endpoints/EconomySettlementEndpoints.cs`
- `src/MathLearning.Api/Endpoints/DailyRunEndpoints.cs`
- `src/MathLearning.Api/Endpoints/CosmeticsEndpoints.cs`
- `src/MathLearning.Api/Endpoints/CosmeticsEndpointHelpers.cs`
- `src/MathLearning.Api/Endpoints/AvatarEndpoints.cs`
- `src/MathLearning.Api/Endpoints/UserEndpoints.cs`
- `src/MathLearning.Api/Endpoints/AuthEndpoints.cs`
- `src/MathLearning.Api/Endpoints/LeaderboardEndpoints.cs`
- `src/MathLearning.Api/Startup/ServiceRegistrationExtensions.cs`
- `src/MathLearning.Api/Program.cs`
- `src/MathLearning.Infrastructure/Services/Cosmetics/CosmeticPlatformService.Public.cs`
- `src/MathLearning.Infrastructure/Services/Cosmetics/CosmeticPlatformService.Rewards.cs`
- `src/MathLearning.Infrastructure/Services/Cosmetics/CosmeticsIdempotencyService.cs`
- `src/MathLearning.Infrastructure/Services/EconomyTransactionService.cs`
- `src/MathLearning.Infrastructure/Services/StudentLeaderboardService.cs`
- `src/MathLearning.Infrastructure/Services/Leaderboard/LeaderboardRankingUtils.cs`
- `src/MathLearning.Infrastructure/Services/Leaderboard/LeaderboardCursor.cs`
- `src/MathLearning.Infrastructure/Services/Leaderboard/CursorCodec.cs`
- `src/MathLearning.Infrastructure/Services/Leaderboard/DbBackedRedisLeaderboardService.cs`
- `src/MathLearning.Services/RedisLeaderboardService.cs`
- `src/MathLearning.Infrastructure/Migrations/Api/20260309091241_AddCosmeticSystem.cs`
- `src/MathLearning.Infrastructure/Migrations/Api/20260624133144_AlignCosmeticsMobileDataModel.cs`

Admin UI/source under `src/MathLearning.Admin/**` was excluded from the audit scope. The latest test-repair commit contained Admin changes, but they were read only as commit context and not audited as part of this task.

## Files changed

- `docs/BACKEND_API_DB_RESIDUAL_AUDIT_2026_07_11_PASS2.md`
- `docs/prompt_queues/backend_api_db_residuals_pass2_2026_07_11.md`
- `docs/DOCS_INDEX.md`
- `docs/prompt_queues/backend_test_coverage.md`
- `.ai/runs/2026-07-11-BACKEND-API-DB-AUDIT-002-evidence.md`

## Commands run

- GitHub connector repository searches, current-head inspection, exact file fetches and prompt-ID collision searches.
- GitHub contents API writes directly to `main` for audit, queue, indexes and evidence.
- No local shell, `dotnet`, PostgreSQL, Redis or object-storage execution was available/needed for this docs-only audit.

## What was done

- Continued from the first API/DB residual audit and reviewed the remaining non-Admin backend surfaces.
- Confirmed seven new packages:
  - `BACKEND-API-DB-009` — server-authoritative cosmetic entitlements;
  - `BACKEND-API-DB-010` — legacy coin/hint/power-up bypass closure;
  - `BACKEND-API-DB-011` — string/GUID-safe student leaderboard cursors/ranks;
  - `BACKEND-API-DB-012` — Redis/DB leaderboard contract parity and runtime failover;
  - `BACKEND-API-DB-013` — one complete registration/account-provisioning owner;
  - `BACKEND-API-DB-014` — photo-avatar deprecation or durable storage repair;
  - `BACKEND-API-DB-015` — safe stale-pending economy/cosmetics recovery.
- Identified but did not duplicate the historical cosmetics migration failure because `BACKEND-MIGRATION-001` was already the canonical owner on current main.
- Added detailed implementation prompts with authority models, exact code boundaries, required database/API work, adversarial tests, PostgreSQL/Redis/storage validation, non-goals and completion gates.
- Updated the documentation index and central execution order while preserving truthful status language for existing rows.

## What was missed

- This was a targeted second pass, not a line-by-line proof that every remaining backend defect has been found.
- No runtime implementation, schema migration or Flutter code was changed.
- No production database, Redis, object storage, logs, traces or traffic were available.
- The identified exploit/failure scenarios are code-derived risks until implementation prompts reproduce and close them executably.

## Validation run

Docs-only consistency validation through the GitHub connector:

- current `main` and the latest repair commit were inspected before publication;
- exact new prompt IDs were searched before allocation;
- `BACKEND-MIGRATION-001` was found and reused rather than duplicated;
- every new audit finding maps to exactly one detailed prompt row;
- central queue and docs index link the new audit/queue;
- files were committed successfully to `main`.

The existing latest repair evidence reports a Release build and **996 passed, 0 failed** for the backend test project. That is baseline context only; those tests do not prove the seven newly queued invariants.

## Validation not run

- `dotnet build MathLearning.slnx -c Release` — not run in this audit; no runtime code changed.
- `dotnet test` — not run; prompts were authored, not implemented.
- PostgreSQL concurrency/EXPLAIN — not run; required by prompts `009`, `010`, `011` and `015` as applicable.
- Redis integration/failover — not run; required by `012`.
- Object-storage integration — not run; required if `014` retains photo avatars.
- Flutter contract tests — not run; each contract-changing implementation prompt requires cross-repo synchronization.

## Waste categories

- connector-only repository access;
- broad search results requiring exact-path fetches;
- parallel main update during the audit;
- overlapping historical queues requiring ownership reconciliation.

## Mistakes observed

- Mistake ID: none newly introduced or requiring a new ledger card
- New or repeated: the inspected runtime code illustrates existing classes already represented by AUTH/IDEM/PERF/XREPO guardrails
- Root cause: not applicable to this docs-only publication
- Prevention added: detailed prompts explicitly separate entitlement from idempotency, prohibit numeric Identity assumptions, require one registration owner and define stale-pending ownership/recovery
- Existing rule that should have prevented it: AGENTS auth/idempotency/contract rules and BACKEND-MISTAKE-QUEUE-001
- Did this run update a rule/prompt/test/queue: yes — audit, detailed queue, central queue and docs index

## Where time/context was wasted

- Main advanced after the first current-head check, requiring a second head/queue inspection.
- The migration defect initially appeared as another possible package, but the new failing-test queue had already reserved `BACKEND-MIGRATION-001`.
- Cosmetics and economy code contains both canonical and legacy paths, so authority had to be traced across endpoint and service layers before allocating separate owners.

## Why waste happened

- Multiple generations of compatibility endpoints coexist with newer canonical settlement services.
- Connector search is file-oriented and does not replace an executable call graph or local static-analysis pass.
- Several risks overlap test, performance, contract and migration queues by design.

## What the next agent should avoid

- Do not treat an idempotency key as proof that a cosmetic reward was earned.
- Do not preserve `/api/coins/earn` or negative/GET mutation behavior for compatibility.
- Do not fix string leaderboard IDs by hashing/truncating them into integers.
- Do not implement Redis friends scope by returning global rows and filtering on the client.
- Do not create tokens before mandatory profile state commits.
- Do not keep production avatar files only on container-local disk.
- Do not recover stale pending rows by deleting them or allowing every retry to process.
- Do not duplicate `BACKEND-MIGRATION-001`, `BACKEND-TEST-032/033/034` or the first-pass API/DB owners.

## Docs/rules updated to prevent repeat

- `docs/BACKEND_API_DB_RESIDUAL_AUDIT_2026_07_11_PASS2.md`
- `docs/prompt_queues/backend_api_db_residuals_pass2_2026_07_11.md`
- `docs/DOCS_INDEX.md`
- `docs/prompt_queues/backend_test_coverage.md`

## Queue updated

- Added `BACKEND-API-DB-009…015` as Prompt-ready.
- Added canonical dependencies and a risk/dependency-based execution order.
- Reused `BACKEND-MIGRATION-001` for migration-chain work rather than reserving `BACKEND-API-DB-016`.

## New optimized prompt added

- `BACKEND-API-DB-009` — cosmetic entitlement and purchase authority.
- `BACKEND-API-DB-010` — legacy economy/hint/power-up bypass removal.
- `BACKEND-API-DB-011` — string-safe leaderboard cursor/rank contract.
- `BACKEND-API-DB-012` — Redis/DB leaderboard parity and failover.
- `BACKEND-API-DB-013` — complete account provisioning and orphan reconciliation.
- `BACKEND-API-DB-014` — photo-avatar durable/deprecation contract.
- `BACKEND-API-DB-015` — pending-operation lease/transaction recovery.

## Follow-up prompt

Start with `BACKEND-API-DB-009`, then `010`, `011` and `015` according to the central queue. Keep `BACKEND-MIGRATION-001` ahead of provider-sensitive schema validation.

## Completion %

- **90% for the requested second-pass static audit and queue-authoring task.**
- Capped because no runtime/provider/dependency validation was executed and the pass remains targeted rather than exhaustive.

## Residual risk

- Exact production exploitability, affected historical rows and operational scale are unmeasured.
- Runtime fixes may require coordinated Flutter changes and data reconciliation.
- Current 996-test baseline does not cover these newly identified invariants.
- A later implementation agent must re-read current main because direct pushes may change code/queue ownership.

## Commit SHA

- `b90d55c86bd71dcc302ee356a2b99c1a6b657395` — second-pass audit.
- `6ce3a2ee90f57ed3f467ca2bfd111cf15a735921` — detailed prompt queue.
- `48f0926958e368cb9a2373d84bd052677577fcfb` — documentation index.
- `84c31dcb8629dabae7d45802ead49e0bee82b256` — central queue routing/order.
- Evidence-file commit: the commit containing this file; report its exact SHA in the final response.
