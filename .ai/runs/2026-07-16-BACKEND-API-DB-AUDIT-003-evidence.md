# BACKEND-API-DB-AUDIT-003 Evidence

Prompt ID: BACKEND-API-DB-AUDIT-003  
Queue: `docs/prompt_queues/backend_api_db_residuals_pass3_2026_07_16.md`  
Agent/tool: ChatGPT with GitHub connector  
Model provider: OpenAI  
Model name/id: GPT-5.6 Thinking  
Model mode/settings: reasoning; connector-only static source/queue/PR audit and direct-main documentation publication  
Client/IDE: ChatGPT connector session  
Run mode: third-pass backend API/database audit and prompt authoring; no runtime implementation  
Token budget: high  
Actual context: current ASP.NET Core API, auth, bug screenshot storage, JWT/revoke-all, cosmetics startup catalog, existing API/DB/test/performance/migration queues and current main history  
Started from queue status: ad-hoc user request to find the largest previously unowned backend defects, exclude Blazor Admin, author precise prompts and publish them to main  
Starting main SHA: `0d0a1965b88f20855987c865fcd4038c856cdfa8`  
Local collision check: current `main`, open PRs, remote `agent/claim/*` branches, BACKEND-API-DB-001…015, BACKEND-TEST, BE-PERF, migration, critical-risk and second-pass queues were searched before allocating 016…019  
Relevant prior mistakes read: BACKEND-MISTAKE-EVIDENCE-001, BACKEND-MISTAKE-AUDIT-001, BACKEND-MISTAKE-VALIDATION-001, BACKEND-MISTAKE-XREPO-001, BACKEND-MISTAKE-AUTH-001, BACKEND-MISTAKE-AUTH-002, BACKEND-MISTAKE-QUEUE-001, BACKEND-MISTAKE-PERF-003  
How this run avoids prior mistakes: all findings remain explicitly static/prompt-ready; IDs were checked on refreshed main; existing auth/rate-limit/storage/migration owners are linked instead of duplicated; Blazor Admin is excluded; no executable or runtime-fix claim is made  
Elapsed time: unknown-not-recorded  
Phase time breakdown: repository/queue deduplication -> endpoint/service investigation -> failure-model analysis -> prompt authoring -> central routing/evidence; exact durations unknown-not-recorded

## Files inspected

- `AGENTS.md`
- `docs/DOCS_INDEX.md`
- `docs/API_ENDPOINT_INVENTORY.md`
- `docs/BACKEND_CRITICAL_APP_FLOW_AUDIT_2026_07_01.md`
- `docs/BACKEND_SECOND_PASS_APP_FLOW_AUDIT_2026_07_01.md`
- `docs/BACKEND_PERFORMANCE_BUG_AUDIT_2026_07_03.md`
- `docs/BACKEND_API_DB_RESIDUAL_AUDIT_2026_07_11.md`
- `docs/BACKEND_API_DB_RESIDUAL_AUDIT_2026_07_11_PASS2.md`
- `docs/prompt_queues/backend_test_coverage.md`
- `docs/prompt_queues/backend_test_followups_2026_07_03.md`
- `docs/prompt_queues/backend_api_db_residuals_2026_07_11.md`
- `docs/prompt_queues/backend_api_db_residuals_pass2_2026_07_11.md`
- `docs/prompt_queues/backend_performance_followups_2026_07_03.md`
- `docs/ai/learning/MISTAKE_LEDGER.md`
- `.ai/RUN_LOG_TEMPLATE.md`
- `REFRESH_TOKEN_SYSTEM.md`
- `src/MathLearning.Api/Program.cs`
- `src/MathLearning.Api/Startup/ServiceRegistrationExtensions.cs`
- `src/MathLearning.Api/Startup/CosmeticStartupSeeder.cs`
- `src/MathLearning.Api/Endpoints/AuthEndpoints.cs`
- `src/MathLearning.Api/Endpoints/BugEndpoints.cs`
- `src/MathLearning.Api/Endpoints/UserEndpoints.cs`
- `src/MathLearning.Api/Endpoints/SyncEndpoints.cs`
- `src/MathLearning.Api/Endpoints/DailyRunEndpoints.cs`
- `src/MathLearning.Api/Endpoints/DailyRunCosmeticsSettlement.cs`
- `src/MathLearning.Infrastructure/Services/BugReportService.cs`
- `src/MathLearning.Infrastructure/Services/LocalScreenshotStorageService.cs`
- `src/MathLearning.Infrastructure/Services/Cosmetics/CosmeticPlatformService.Public.cs`
- related auth, user-settings, bug, cosmetics, migration and endpoint tests found through exact repository search

`src/MathLearning.Admin/**` was explicitly excluded from source analysis and prompt ownership.

## Files changed

- `docs/BACKEND_API_DB_RESIDUAL_AUDIT_2026_07_16_PASS3.md`
- `docs/prompt_queues/backend_api_db_residuals_pass3_2026_07_16.md`
- `docs/prompt_queues/backend_api_db_pass3/BACKEND-API-DB-016.md`
- `docs/prompt_queues/backend_api_db_pass3/BACKEND-API-DB-017.md`
- `docs/prompt_queues/backend_api_db_pass3/BACKEND-API-DB-018.md`
- `docs/prompt_queues/backend_api_db_pass3/BACKEND-API-DB-019.md`
- `docs/DOCS_INDEX.md`
- `docs/prompt_queues/backend_test_coverage.md`
- `.ai/runs/2026-07-16-BACKEND-API-DB-AUDIT-003-evidence.md`

## Commands run

- GitHub connector current-main commit search.
- GitHub connector code/file searches for existing prompt IDs, risk phrases, endpoint/service owners and open work.
- Exact current-main file reads and ranged reads.
- Open PR and remote claim-branch inspection.
- GitHub contents API create/update operations directly on `main`.
- No local shell, .NET SDK, PostgreSQL, Redis or object-storage command executed.

## What was done

- Audited the current non-Admin backend against all existing critical, second-pass, performance, test, migration and API/DB prompt owners.
- Rejected already-owned candidates including refresh-token storage, avatar durability, bug input validation, rate-limit store cardinality, proxy trust, account-provisioning atomicity, pending economy/cosmetics recovery, GET writes, sync bounds and outbox/background work.
- Confirmed four previously unowned high-impact residuals:
  - `BACKEND-API-DB-016` — private/durable bug screenshot access and lifecycle;
  - `BACKEND-API-DB-017` — lockout-aware credential/account verification and auth-specific abuse policy;
  - `BACKEND-API-DB-018` — access-token/session invalidation and truthful revoke-all semantics;
  - `BACKEND-API-DB-019` — versioned/auditable cosmetics catalog deployment and readiness.
- Authored detailed execution packets with exact evidence, owner boundaries, dependencies, failure matrices, provider/concurrency tests, validation commands, non-goals, stop conditions and completion gates.
- Registered the pass-3 audit/queue in the documentation index and primary backend test/API queue.
- Preserved current canonical owners instead of creating duplicate limiter, refresh, avatar, migration, entitlement or pending-operation systems.

## What was missed

- This was a high-risk third pass, not a formal proof that every remaining backend defect has been found.
- No production traffic, logs, deployment settings, branch-protection configuration, PostgreSQL data, Redis state or object-storage provider was available.
- No runtime implementation or mobile contract change was made.
- User settings still accept some arbitrary strings; this was recorded as a lower-priority deferred candidate, not allocated a new ID.

## Validation run

Docs/queue consistency validation through exact GitHub reads:

- refreshed current main before allocating prompt IDs;
- searched `BACKEND-API-DB-016` and surrounding ownership to avoid ID collision;
- found no active remote `agent/claim/*` branch for the selected work;
- checked open backend PRs for overlap;
- every selected audit finding maps to exactly one linked detailed prompt;
- central queue and docs index reference the pass-3 audit/queue;
- all create/update operations returned committed main SHAs.

## Validation not run

- `dotnet build MathLearning.slnx -c Release` — not run; docs/prompt/evidence only.
- `dotnet test` — not run; no runtime fix was implemented.
- PostgreSQL/provider tests — not run; required by implementation prompts 017–019 as applicable.
- Redis/distributed-cache tests — not run; required by 017/018.
- Durable object-storage tests — not run; required by 016.
- Flutter/mobile contract tests — not run; each contract-changing implementation prompt requires an explicit cross-repo sync decision.
- GitHub Actions evidence — no workflow was triggered or inspected for this docs-only direct-main publication.

## Waste categories

- connector-only source traversal;
- broad legacy queue overlap requiring exact owner reconciliation;
- code search index and current-main reads required separate verification;
- no local repository tree or executable environment.

## Mistakes observed

Mistakes observed: none newly introduced.  
Mistake ID: none  
New or repeated: none  
Root cause: not applicable  
Prevention added: precise dedup tables, explicit exclusions and shared-owner dependencies in audit/queue/prompts  
Existing rule that should have prevented it: BACKEND-MISTAKE-AUDIT-001, BACKEND-MISTAKE-VALIDATION-001 and BACKEND-MISTAKE-QUEUE-001  
Did this run update a rule/prompt/test/queue: yes — audit, four detailed prompts, docs index and central queue; no runtime/test files

## Where time/context was wasted

- Historical queues contain both validated, runtime-fixed and prompt-ready rows, requiring code-first reclassification before admitting anything new.
- Bug screenshot safety overlaps input validation, avatar storage and static-file rules but has a distinct private-read boundary.
- Auth abuse, distributed limiter storage, registration atomicity, refresh-token storage and access-session revocation share files but require separate canonical owners.

## Why waste happened

- The backend has accumulated several generations of audits and compatibility routes.
- Connector search is file-oriented and cannot run a complete call graph or provider test.
- Security boundaries cross API, Identity, middleware, database, cache, storage and mobile contracts.

## What the next agent should avoid

- Do not expose bug screenshots through static files or persist public bearer URLs.
- Do not implement auth throttling as another process-local dictionary or IP-only rule.
- Do not call six-character password acceptance “secure” merely by adding character classes.
- Do not claim logout-all while old access JWTs remain valid.
- Do not query the database on every request or store every JWT without a measured session-version design.
- Do not silently overwrite production catalog fields at API startup.
- Do not edit `src/MathLearning.Admin/**` for these prompts.
- Do not duplicate BACKEND-TEST-025, BE-PERF-011, BACKEND-API-DB-007/009/013/014/015 or BACKEND-MIGRATION-001.

## Docs/rules updated to prevent repeat

- `docs/BACKEND_API_DB_RESIDUAL_AUDIT_2026_07_16_PASS3.md`
- `docs/prompt_queues/backend_api_db_residuals_pass3_2026_07_16.md`
- four detailed pass-3 prompt packets
- `docs/DOCS_INDEX.md`
- `docs/prompt_queues/backend_test_coverage.md`

## Queue updated

- Added BACKEND-API-DB-016…019 as prompt-ready/dependency-gated rows.
- Linked existing test/performance/migration owners instead of duplicating them.
- Added risk/dependency execution order and focused validation package.

## New optimized prompt added

- BACKEND-API-DB-016 — private bug screenshot authorization, durable storage and lifecycle.
- BACKEND-API-DB-017 — credential abuse, Identity lockout, enumeration-safe account verification.
- BACKEND-API-DB-018 — access-session revocation and account/role state awareness.
- BACKEND-API-DB-019 — versioned cosmetics catalog deployment and readiness.

## Follow-up prompt

Start with 016 and 017 after refreshed-main collision checks. Run 018 only after 017. Run 019 after migration/catalog-owner collision checks. Existing unimplemented P0 trust-boundary prompts retain higher priority where current code still proves them open.

## Completion %

- 90% for the requested static audit, detailed prompt authoring, queue routing and main publication.
- Capped because no executable/provider/runtime validation ran and the audit cannot prove exhaustiveness.

## Residual risk

- Production exploitability and affected data are unmeasured.
- Implementation may require security/product/operations decisions for account verification, cache failure mode, durable object storage and catalog field ownership.
- Each implementation agent must re-read current main and mobile contracts before changing HTTP behavior.

## Commit SHA

- `cbf2d9fbb57d21fd103bd22c4149017b0dcfa350` — pass-3 audit.
- `0099e2ea97928cea41334eacbb19f6cb251146a9` — pass-3 owning queue.
- `2dffd3bd41c136f2c5d23ffa1a7d59bc906f58f0` — BACKEND-API-DB-016 detail.
- `2f48a8d82d30be34ab999dbb6fde646a28b72106` — BACKEND-API-DB-017 detail.
- `59b7cd607f5cfd334c40a8ff53a37810ea1b95f3` — BACKEND-API-DB-018 detail.
- `a0ea0193ba6d9cd1abae3ebcc5054282e20e37d5` — BACKEND-API-DB-019 detail.
- `2c2b0313ffed47348659119c578dd3861b303994` — documentation index registration.
- `17eaac0c7399c5fe7aeb6e9b480e3143b0945108` — primary queue routing/order.
- Evidence-file commit: the commit containing this file; report its exact SHA in the final response.