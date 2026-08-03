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
Completed at UTC: 2026-08-03T09:22:00Z
Elapsed time: 6m
Relevant prior mistakes read: BACKEND-MISTAKE-EVIDENCE-001, BACKEND-MISTAKE-VALIDATION-001, BACKEND-MISTAKE-XREPO-001, BACKEND-MISTAKE-AUDIT-001
How this run avoids prior mistakes: compact v2 evidence with numeric counters; focused Health|Metrics|Monitoring proof; inventory + mobile contract sync in same delivery
Owner/hypothesis: Public probes keep only stable status/reason fields; schema/metrics/jobs require admin; falsifier is anonymous access to diagnostic detail or public migration/count leakage
Files inspected: 14
Files changed: 12
Searches: 10
Validation runs: 2
Failed retries: 1

## Outcome
- Public `/api/health/db` and `/api/health/ready` expose only status, optional safe reason codes, and timestamp.
- `/api/health/schema`, `/health/schema`, `/metrics`, and `/api/monitoring/jobs` require `UiTokensAdminPolicy`.
- Focused tests passed 27/27 after moving schema routes off the anonymous group.

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
Validation run: `dotnet test tests/MathLearning.Tests/MathLearning.Tests.csproj --filter "Health|Metrics|Monitoring"` -> passed (27/27)
Validation not run: full suite; PostgreSQL provider suite not required for this auth/contract slice

## Exceptions and learning
Mistakes observed: none
Waste: one failed test iteration before schema auth fix
Missed: none
Follow-up: replace mock `/api/monitoring/jobs` with real Hangfire status when available; notify mobile/platform owners that public ready/db no longer include counts/checksums
Residual risk: none
Documentation impact: updated API inventory, mobile contract readiness note, manifest/registry, queue Done rows
Cross-repo impact: mobile/platform consumers must not require removed public ready/db fields; sync backend SHA on merge

## Delivery
State: Needs merge
Branch/PR: cursor/backend-test-026-public-health-fa87 / https://github.com/ivanjovicic/MathLearning/pull/23
Commit SHA: self
Completion %: 79
