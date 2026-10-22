# USER-20261022-PRODUCTION-STARTUP-HEALTH Evidence

Evidence format: v2
Prompt ID: USER-20261022-PRODUCTION-STARTUP-HEALTH
Queue: user-assigned
Agent/tool: unknown-not-exposed
Model provider: unknown-not-exposed
Model name/id: unknown-not-exposed
Client/IDE: unknown-not-exposed
Run mode: audit
Token budget: high
Started at UTC: 2026-10-22T15:48:13Z
Completed at UTC: 2026-10-22T16:10:28Z
Elapsed time: 22m 15s
Relevant prior mistakes read: BACKEND-MISTAKE-EVIDENCE-001, BACKEND-MISTAKE-VALIDATION-001, BACKEND-MISTAKE-AUTH-001, BACKEND-MISTAKE-CI-001, BACKEND-MISTAKE-SCOPE-001
How this run avoids prior mistakes: apply BACKEND-MISTAKE-EVIDENCE-001; apply BACKEND-MISTAKE-VALIDATION-001; apply BACKEND-MISTAKE-AUTH-001; apply BACKEND-MISTAKE-CI-001; apply BACKEND-MISTAKE-SCOPE-001
Owner/hypothesis: Top-level fatal catch swallows exceptions and Fly routes using liveness only; readiness exists but is not configured as a service check.
Files inspected: 21
Files changed: 7
Searches: 12
Validation runs: 9
Failed retries: 4

## Outcome
- Startup fatal-exit behavior and Fly readiness routing stabilized locally; production remains unreconciled and undeployed.

## Changed paths
- .ai/runs/2026-10-22-USER-20261022-PRODUCTION-STARTUP-HEALTH-evidence.md
- FLY_DEPLOYMENT_GUIDE.md
- fly.toml
- src/MathLearning.Api/Program.cs
- tests/MathLearning.Tests/Endpoints/HealthEndpointContractTests.cs
- tests/MathLearning.Tests/Infrastructure/RedisRuntimeStatusTests.cs
- tests/MathLearning.Tests/Services/ProgramFatalStartupTests.cs

## Validation
Validation run: Release solution build passed with 0 errors and 6 existing warnings; focused health/startup/Redis/DB-fallback tests passed 20/20; isolated previously timing-out tests passed 6/6; strict Fly config validation passed; docs health 28/28 and agent-system validation passed; diff check passed.
Validation not run: Full test project did not complete within the guarded 300-second limit; several HttpClient 100-second test timeouts occurred under suite load, while representative affected cases passed when isolated. Live production readiness/database query skipped because the only machine is stopped and app startup may write the missing initial design-token record. No production migration or deploy was run.

## Exceptions and learning
Mistakes observed: Initial child-process regression probe first stopped at missing production DataProtection test configuration; added ephemeral test-only certificate and reran passing. Full suite runner initially used cp1252 for UTF-8 output; rerun used Python UTF-8 mode.
Waste: Retried full suite after runner encoding/idle issues; isolated the timed-out endpoint cases to distinguish load contention.
Missed: Production's current applied migration state and live readiness still require a safe operator-approved check before deployment.
Follow-up: Before deployment, verify the production migration history using the approved read-only provider workflow; apply the reviewed migration plan only with explicit authorization, then run the existing manual Fly workflow.
Residual risk: The October 19 migration mismatch may still be active; Fly production machine is stopped and current app logs contain no startup evidence, so exact schema status is unverified.
Documentation impact: Updated FLY_DEPLOYMENT_GUIDE.md with liveness/readiness, required config names, fallback behavior, deploy and image rollback instructions; no route inventory change because API routes did not change.
Cross-repo impact: no

## Delivery
State: partial
Branch/PR: codex/fly-startup-health; target origin/main delivery pending
Commit SHA: self
Completion %: 90
