# MATHLEARNING-PRODUCTION-LOG-WARNINGS-001 Evidence

Evidence format: v2
Prompt ID: MATHLEARNING-PRODUCTION-LOG-WARNINGS-001
Queue: user-assigned
Agent/tool: Codex / exec
Model provider: unknown-not-exposed
Model name/id: unknown-not-exposed
Client/IDE: unknown-not-exposed
Run mode: known-fix
Token budget: medium
Started at UTC: 2026-09-08T17:00:00Z
Completed at UTC: 2026-09-08T19:05:00Z
Elapsed time: 2h05m
Relevant prior mistakes read: none
How this run avoids prior mistakes: fail closed for missing production key material, preserve explicit catalog import and Redis fallback ownership, and never deploy or print secrets without live verification.
Owner/hypothesis: API/deployment owner; supplied exit-code-0 machine lifecycle is not a crash, DataProtection is default ephemeral, Redis fallback is intentional, and catalog readiness is operator-controlled.
Files inspected: 24
Files changed: 14
Searches: 8
Validation runs: 10
Failed retries: 3

## Outcome

- Production warning ownership was confirmed and a repository patch was committed; production acceptance remains pending secret/volume configuration and live verification.
- DataProtection now requires durable absolute storage and a secret-managed PFX private-key certificate outside Development/Test. Redis mode is explicit in health; port defaults are cleared without changing 8080.
- Catalog ownership and SeedAdmin policy remain unchanged; current production catalog was previously imported and readiness verified independently.

## Changed paths

- `src/MathLearning.Api/Startup/DataProtectionConfiguration.cs`, `RedisRuntimeStatus.cs`
- `src/MathLearning.Api/Program.cs`, `Startup/ServiceRegistrationExtensions.cs`, `Endpoints/HealthEndpoints.cs`
- `tests/MathLearning.Tests/Startup/DataProtectionConfigurationTests.cs`, `Infrastructure/RedisRuntimeStatusTests.cs`, `Endpoints/HealthEndpointContractTests.cs`
- `Dockerfile`, `fly.toml`
- `docs/ARCHITECTURE_OVERVIEW.md`, `docs/BACKEND_COLD_START_BUDGET.md`, `docs/API_ENDPOINT_INVENTORY.md`

## Validation

Validation run: focused baseline 13 passed; focused post-edit 20 passed; API Release build passed with 0 errors; `git diff --check` passed; documentation health 25/25 and agent-system validation passed.
Validation run: full suite 1174 passed, 12 failed, 0 skipped. Failures are existing unrelated cosmetics/InMemory PayloadHash/email/Sync/economy/XP tests; no new warning-focused test failed.
Validation run: solution Release build blocked by missing `src/MathLearning.TranslationJob/obj/project.assets.json` under `--no-restore`; API Release build passed independently.
Validation not run: live deploy/Fly config validation - `flyctl` had no access token; no live secret, volume, DB or deployment mutation was authorized.

## Exceptions and learning

Mistakes observed: none
Waste: initial patch context mismatch, one missing test using directive, and one invalid 420-second guard timeout.
Missed: exact Fly restart event classification remains unverified without Fly event history.
Follow-up: deployment operator must provision the volume/certificate and run Fly machine/event plus health verification.
Residual risk: the committed DataProtection code will intentionally refuse Production startup until the declared Fly volume and certificate secrets are present; the patch is not live.
Documentation impact: updated `docs/ARCHITECTURE_OVERVIEW.md`, `docs/BACKEND_COLD_START_BUDGET.md`, and `docs/API_ENDPOINT_INVENTORY.md`.
Cross-repo impact: no - Flutter was not touched.

## Evidence details

- Supplied log shows `Machine created and started in 6.474s` and normal exit code 0, with no crash exception. This is consistent with a Fly deploy/replacement lifecycle but does not prove the exact orchestrator event; local Fly event commands were unavailable.
- Earlier same-day production verification after explicit catalog import returned `/health` 200, `/api/health/db` 200 and `/api/health/ready` 200 with revision `catalog-20260716-019`, checksum `sha256:d3fd4a067bc00abd2b28a0d873fa1e6dfaacfd9ef0e7d64cc6c5d514304538fc`, no catalog issues and schema pending count 0.
- Redis remains DB fallback by default (`Redis__Required=false`); `true` makes startup/readiness fail closed. SeedAdmin remains disabled normally and requires explicit one-time policy plus a non-default secret.
- Required handoff: `fly volumes create mathlearning_data --app mathlearning-api --region ams --size 1`; set `DataProtection__CertificateBase64` and `DataProtection__CertificatePassword` as Fly secrets (or a mounted PFX path), then deploy and verify logs/health.
- Credentials pasted earlier must be rotated through normal Fly/Neon secret management. Replacement values are not in this log or repository.

## Delivery

State: Needs validation
Branch/PR: `agent/BACKEND-SEASON-DAILY-RUN-PROVENANCE-001`, no PR opened
Commit SHA: self
Completion %: 79
