# BACKEND-API-DB-005 Evidence

Evidence format: v2
Prompt ID: BACKEND-API-DB-005
Queue: docs/prompt_queues/backend_api_db_residuals_2026_07_11.md
Agent/tool: unknown-not-exposed
Model provider: unknown-not-exposed
Model name/id: unknown-not-exposed
Client/IDE: unknown-not-exposed
Run mode: known-fix
Token budget: low
Started at UTC: 2026-07-30T08:06:42Z
Completed at UTC: 2026-07-30T08:27:00Z
Elapsed time: 20m 18s
Relevant prior mistakes read: BACKEND-MISTAKE-EVIDENCE-001, BACKEND-MISTAKE-VALIDATION-001, BACKEND-MISTAKE-XREPO-001, BACKEND-MISTAKE-AUDIT-001
How this run avoids prior mistakes: apply BACKEND-MISTAKE-EVIDENCE-001; apply BACKEND-MISTAKE-VALIDATION-001; apply BACKEND-MISTAKE-XREPO-001; apply BACKEND-MISTAKE-AUDIT-001
Owner/hypothesis: open
Files inspected: 25
Files changed: 8
Searches: 10
Validation runs: 5
Failed retries: 3

## Outcome
- Implemented localized offline bundle mapping and split content vs snapshot revision truth

## Changed paths
- src/MathLearning.Infrastructure/Services/Sync/OfflineBundleService.cs
- src/MathLearning.Application/DTOs/Sync/SyncDtos.cs
- src/MathLearning.Application/Services/SyncServices.cs
- src/MathLearning.Api/Endpoints/SyncEndpoints.cs
- tests/MathLearning.Tests/Services/OfflineBundleServiceTests.cs
- tests/MathLearning.Tests/Endpoints/OfflineBundleEndpointTests.cs
- docs/mobile_api_contract.md
- docs/API_ENDPOINT_INVENTORY.md

## Validation
Validation run: dotnet test tests/MathLearning.Tests/MathLearning.Tests.csproj -c Release --filter FullyQualifiedName~OfflineBundle ; dotnet build MathLearning.slnx -c Release
Validation not run: none

## Exceptions and learning
Mistakes observed: Route path was /api/offline/bundle rather than /api/sync/offline/bundle; bundle fixture had to target the first difficulty-ordered question.
Waste: Two failed test iterations while aligning the route and bundle selection order.
Missed: None.
Follow-up: Optional persisted publish-time revision if request-time hashing becomes too expensive.
Residual risk: Version computation still hashes all serialized content at request time; if bundle size grows, move the fingerprint to a persisted revision.
Documentation impact: Updated docs/mobile_api_contract.md and docs/API_ENDPOINT_INVENTORY.md for offline bundle route and revision semantics.
Cross-repo impact: None.

## Delivery
State: Done
Branch/PR: unknown
Commit SHA: self
Completion %: 100
