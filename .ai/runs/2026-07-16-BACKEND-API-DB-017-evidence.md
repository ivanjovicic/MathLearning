# BACKEND-API-DB-017 Evidence

Prompt ID: BACKEND-API-DB-017
Queue: docs/prompt_queues/backend_api_db_residuals_pass3_2026_07_16.md
Agent/tool: Codex / functions.exec_command
Model provider: unknown-not-exposed
Model name/id: unknown-not-exposed
Model mode/settings: unknown-not-exposed
Client/IDE: unknown-not-exposed
Run mode: authentication policy + endpoint contract + distributed abuse tests
Token budget: medium
Actual context: auth lockout, password policy and enumeration hardening
Started from queue status: Prompt-ready
Local collision check: no existing 2026-07-16 BACKEND-API-DB-017 run log found
Relevant prior mistakes read:
- BACKEND-MISTAKE-EVIDENCE-001
- BACKEND-MISTAKE-VALIDATION-001
- BACKEND-MISTAKE-XREPO-001
How this run avoids prior mistakes:
- keep the auth scope narrow, record the explicit account-verification decision, and only claim provider behavior that is actually tested.
Elapsed time: unknown-not-recorded
Phase time breakdown: unknown-not-recorded

## Auth policy table

| Endpoint | Identifier(s) | Credential/account check | Account / IP / device budget | Public failure contract | Audit event | Retry / reset rule |
| --- | --- | --- | --- | --- | --- | --- |
| `/auth/mobile/register` | trimmed username + parsed email | real email parser, max password length guard, Identity password validator, unique username/email checks | 3 account attempts / 9 network-device attempts per 15 minutes | `400` or `409` with generic `Registration could not be completed` | registration failure by safe category only | retry after limiter window; duplicates reset only after corrected payload |
| `/auth/register` | trimmed username + parsed email | real email parser, max password length guard, Identity password validator, unique username/email checks | 3 account attempts / 9 network-device attempts per 15 minutes | `400`, `409`, or `429` with generic safe message | registration failure by safe category only | retry after limiter window; duplicates reset only after corrected payload |
| `/auth/login` | trimmed normalized username | `SignInManager.CheckPasswordSignInAsync(..., lockoutOnFailure: true)` | 5 account attempts / 15 network-device attempts per 10 minutes | `401` with generic invalid-credentials body, `429` for throttling | login failure reason kept internal only | lockout expires via Identity lockout window; correct password cannot bypass while locked |
| `/api/auth/login` | trimmed normalized username | same handler and same lockout path as `/auth/login` | same as `/auth/login` | same as `/auth/login` | same as `/auth/login` | same as `/auth/login` |
| `/auth/refresh` | refresh token user id + bounded UA/IP metadata | refresh token existence/validity, then auth budget before rotation | 10 account attempts / 30 network-device attempts per 10 minutes | `401` for invalid token/user, `429` for throttling | refresh failure by safe category only | retry after limiter window; rotation still revokes once accepted |

Verification mode decision:

- reviewed no-email / managed provisioning path;
- backend registration is the authoritative verifier for trusted accounts;
- `EmailConfirmed = true` is set on issued identities so the flag is not left dangling as an unused value;
- no external email/guardian token flow is claimed in this repo.

## Cross-repo impact

- yes
- Other repos checked: none directly in this turn; current-repo mobile contract docs were used as the reference
- Other repo docs touched: none
- Deferred sync reason: no direct `Mathlearning-Mobile-App` checkout was available in this turn
- Follow-up prompt: `BACKEND-API-DB-018`

## Files inspected

- `AGENTS.md`
- `docs/BUGFIX_PATTERN_GUARDRAILS.md`
- `docs/DOCS_INDEX.md`
- `docs/AGENT_SHARED_OPERATING_STANDARD.md`
- `docs/AGENT_RUN_LOG_ENFORCEMENT.md`
- `docs/ai/learning/MISTAKE_LEDGER.md`
- `docs/prompt_queues/backend_api_db_pass3/BACKEND-API-DB-017.md`
- `docs/API_ENDPOINT_INVENTORY.md`
- `docs/mobile_api_contract.md`
- `src/MathLearning.Api/Endpoints/AuthEndpoints.cs`
- `src/MathLearning.Api/Startup/ServiceRegistrationExtensions.cs`
- `src/MathLearning.Api/Startup/TestAccountSeeder.cs`
- `tests/MathLearning.Tests/Endpoints/AuthDevSeedLoginTests.cs`
- `tests/MathLearning.Tests/Endpoints/AuthMobileRegistrationAtomicityTests.cs`
- `tests/MathLearning.Tests/Endpoints/AuthMobileRegistrationRelationalAtomicityTests.cs`
- `tests/MathLearning.Tests/Endpoints/AuthRefreshConcurrencyTests.cs`
- `tests/MathLearning.Tests/Endpoints/AuthRefreshPostgresConcurrencyTests.cs`
- `tests/MathLearning.Tests/Endpoints/AuthRefreshRelationalConcurrencyTests.cs`
- `tests/MathLearning.Tests/Endpoints/AuthSafeErrorResponseTests.cs`

## Files changed

- `docs/API_ENDPOINT_INVENTORY.md`
- `src/MathLearning.Api/Endpoints/AuthEndpoints.cs`
- `src/MathLearning.Api/Startup/ServiceRegistrationExtensions.cs`
- `src/MathLearning.Api/Startup/TestAccountSeeder.cs`
- `tests/MathLearning.Tests/Endpoints/AuthDevSeedLoginTests.cs`
- `tests/MathLearning.Tests/Endpoints/AuthMobileRegistrationAtomicityTests.cs`
- `tests/MathLearning.Tests/Endpoints/AuthMobileRegistrationRelationalAtomicityTests.cs`
- `tests/MathLearning.Tests/Endpoints/AuthRefreshConcurrencyTests.cs`
- `tests/MathLearning.Tests/Endpoints/AuthRefreshPostgresConcurrencyTests.cs`
- `tests/MathLearning.Tests/Endpoints/AuthRefreshRelationalConcurrencyTests.cs`
- `tests/MathLearning.Tests/Endpoints/AuthSafeErrorResponseTests.cs`
- `.ai/runs/2026-07-16-BACKEND-API-DB-017-evidence.md`

## Commands run

- `Get-Content` / `Select-Object` / `rg` reads for AGENTS, queue prompt, auth endpoints, DTOs, tests, limiter/store, and mobile contract docs
- `git status --short`
- `git diff --check`
- `python scripts/validate_agent_evidence.py`
- `dotnet test tests/MathLearning.Tests/MathLearning.Tests.csproj -c Release --filter "FullyQualifiedName~AuthLogin|FullyQualifiedName~AuthRegistration|FullyQualifiedName~Lockout|FullyQualifiedName~AuthMobileRegistration|FullyQualifiedName~AuthRefresh|FullyQualifiedName~AuthSafeError"`
- `dotnet build MathLearning.slnx -c Release`
- `dotnet ef migrations has-pending-model-changes --project src/MathLearning.Infrastructure/MathLearning.Infrastructure.csproj --startup-project src/MathLearning.Api/MathLearning.Api.csproj --context ApiDbContext`

## What was done

- Tightened `Identity` password policy, enabled lockout, and registered `SignInManager`.
- Switched login to lockout-aware `CheckPasswordSignInAsync(..., lockoutOnFailure: true)`.
- Added auth-specific account + network/device throttling and generic `429` responses.
- Replaced endpoint-only short password checks with one Identity-owned policy plus a long-input guard.
- Normalized email parsing and collapsed duplicate registration responses into generic `409` / `400` contracts.
- Set `EmailConfirmed = true` for backend-managed registration and seed flows, with the decision documented as managed/no-email provisioning.
- Updated auth tests for the longer passwords, added a lockout regression, and asserted the managed verification state.
- Synchronized `docs/API_ENDPOINT_INVENTORY.md` with the new auth contract.

## What was missed

- No external email/guardian verification flow was implemented.
- Multi-replica distributed limiter proof remains deferred to the shared limiter owner; this run only validated the auth contract against the local store abstraction.
- Direct `Mathlearning-Mobile-App` repo sync was not performed in this turn.

## Validation run

- `python scripts/validate_agent_evidence.py` - failed due existing repo-wide evidence-lint debt in older logs/queue rows, not due to this auth diff
- `dotnet test tests/MathLearning.Tests/MathLearning.Tests.csproj -c Release --filter "FullyQualifiedName~AuthLogin|FullyQualifiedName~AuthRegistration|FullyQualifiedName~Lockout|FullyQualifiedName~AuthMobileRegistration|FullyQualifiedName~AuthRefresh|FullyQualifiedName~AuthSafeError"` - passed
- `dotnet build MathLearning.slnx -c Release` - passed
- `dotnet ef migrations has-pending-model-changes --project src/MathLearning.Infrastructure/MathLearning.Infrastructure.csproj --startup-project src/MathLearning.Api/MathLearning.Api.csproj --context ApiDbContext` - passed
- CI: No GitHub Actions evidence found via connector

## Validation not run

- none

## Waste categories

- repo-wide evidence-lint debt outside this diff
- one PowerShell command-separator retry

## Mistakes observed

- none for this diff; the evidence validator surfaced pre-existing repo debt in older files

## Where time/context was wasted

- The evidence validator reports many legacy queue/run-log issues unrelated to this prompt.
- One shell command had to be retried because `&&` is not valid in this PowerShell session.

## Why waste happened

- The repository already contains older evidence files/queue rows that fail the current validator.
- The first commit attempt used bash-style chaining instead of PowerShell-separated commands.

## What the next agent should avoid

- Do not reintroduce six-character passwords or per-endpoint password policy checks.
- Do not claim distributed/auth abuse protection beyond what the shared limiter owner actually proves.
- Do not leave `EmailConfirmed = false` as a dangling flag in managed registration paths.
- Do not weaken the generic `401` / `409` / `429` auth contract back into account-enumerating messages.

## Docs/rules updated to prevent repeat

- `docs/API_ENDPOINT_INVENTORY.md`
- `src/MathLearning.Api/Startup/TestAccountSeeder.cs`
- auth regression tests for lockout, alias parity, and managed verification state

## Queue updated

- none

## New optimized prompt added

- none

## Follow-up prompt

- `BACKEND-API-DB-018`

## Completion %

- 90%

## Residual risk

- Auth throttling is still backed by the local store abstraction in this repo, so multi-replica behavior still needs the shared limiter owner.
- The managed/no-email verification decision is documented, but no external verification workflow exists yet.

## Commit SHA

- `cf8ea27b03153f5c8ef6c140f7e0ebe5bf498bf4`
