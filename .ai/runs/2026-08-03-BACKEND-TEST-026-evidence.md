# BACKEND-TEST-026 Evidence

Evidence format: v2
Prompt ID: BACKEND-TEST-026
Queue: docs/prompt_queues/backend_test_followups_2026_07_03.md
Agent/tool: cursor-cloud
Model provider: unknown-not-exposed
Model name/id: unknown-not-exposed
Client/IDE: cursor-cloud
Run mode: known-fix
Token budget: medium
Started at UTC: 2026-08-03T09:15:57Z
Completed at UTC: 2026-08-03T09:20:30Z
Elapsed time: ~5m
Relevant prior mistakes read: BACKEND-MISTAKE-EVIDENCE-001, BACKEND-MISTAKE-VALIDATION-001, BACKEND-MISTAKE-XREPO-001, BACKEND-MISTAKE-AUDIT-001
How this run avoids prior mistakes: compact v2 evidence; focused Health|Metrics|Monitoring proof before Done; inventory + mobile contract sync; no second subsystem

## Outcome
- Separated public liveness/readiness from admin diagnostics.
- Public `/api/health/db` and `/api/health/ready` return only status + safe reason codes + timestamp.
- `/api/health/schema`, `/health/schema`, `/metrics`, and `/api/monitoring/jobs` require `UiTokensAdminPolicy`.
- Focused tests: 27 passed, 0 failed.

## Changed paths
- src/MathLearning.Api/Endpoints/HealthEndpoints.cs
- src/MathLearning.Api/Program.cs
- tests/MathLearning.Tests/Endpoints/HealthEndpointContractTests.cs
- tests/MathLearning.Tests/Endpoints/RateLimitMetricsEndpointTests.cs
- tests/MathLearning.Tests/Endpoints/MonitoringLogAuthorizationTests.cs
- docs/API_ENDPOINT_INVENTORY.md
- docs/mobile_api_contract.md
- docs/DOCS_MANIFEST.json
- docs/DOCS_REGISTRY.md
- docs/prompt_queues/backend_test_followups_2026_07_03.md
- docs/prompt_queues/backend_test_coverage.md
- .ai/runs/2026-08-03-BACKEND-TEST-026-evidence.md

## Validation
Validation run: `dotnet test tests/MathLearning.Tests/MathLearning.Tests.csproj --filter "Health|Metrics|Monitoring"` → Passed 27 / Failed 0
Validation not run: full suite; PostgreSQL provider suite (docs/contract auth change only beyond health probes)

## Exceptions and learning
Mistakes observed: group-level AllowAnonymous overrode child RequireAuthorization on `/api/health/schema`; fixed by mapping schema outside the anonymous group
Waste: one failed test iteration before schema auth fix
Missed: none material
Follow-up: replace mock `/api/monitoring/jobs` with real Hangfire status when available
Residual risk: platform probes that still scrape rich ready payloads need to accept minimized shape
Documentation impact: updated API inventory, mobile contract readiness note, manifest/registry
Cross-repo impact: mobile consumers of public ready/db must not require removed fields; backend SHA sync on merge

## Delivery
State: Done candidate
Branch/PR: cursor/backend-test-026-public-health-fa87
Commit SHA: self
Completion %: 100
