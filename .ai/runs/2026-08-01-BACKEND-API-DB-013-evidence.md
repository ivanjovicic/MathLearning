# BACKEND-API-DB-013 Evidence

Evidence format: v2
Prompt ID: BACKEND-API-DB-013
Queue: user-assigned
Agent/tool: unknown-not-exposed
Model provider: unknown-not-exposed
Model name/id: unknown-not-exposed
Client/IDE: unknown-not-exposed
Run mode: known-fix
Token budget: medium
Started at UTC: 2026-08-01T06:29:36Z
Completed at UTC: 2026-08-01T06:31:46Z
Elapsed time: 2m 10s
Relevant prior mistakes read: BACKEND-MISTAKE-EVIDENCE-001, BACKEND-MISTAKE-VALIDATION-001, BACKEND-MISTAKE-AUTH-001, BACKEND-MISTAKE-AUTH-002, BACKEND-MISTAKE-VALIDATION-002
How this run avoids prior mistakes: apply BACKEND-MISTAKE-EVIDENCE-001; apply BACKEND-MISTAKE-VALIDATION-001; apply BACKEND-MISTAKE-AUTH-001; apply BACKEND-MISTAKE-AUTH-002; apply BACKEND-MISTAKE-VALIDATION-002
Owner/hypothesis: IAccountProvisioningService owns Identity+UserProfile; falsifier=legacy register or login can mint usable tokens without UserProfile
Files inspected: 12
Files changed: 9
Searches: 4
Validation runs: 2
Failed retries: 1

## Outcome
- Legacy /auth/register now creates Identity+UserProfile via IAccountProvisioningService before tokens.
- Login denies Identity-only incomplete accounts with stable 403 Account setup incomplete and mints no tokens.
- Focused AuthIncompleteAccount + AuthMobileRegistrationAtomicity/Relational proofs green; historical orphan backfill deferred.

## Changed paths
- src/MathLearning.Api/Services/AccountProvisioningService.cs
- src/MathLearning.Api/Endpoints/AuthEndpoints.cs
- src/MathLearning.Api/Startup/ServiceRegistrationExtensions.cs
- tests/MathLearning.Tests/Endpoints/AuthIncompleteAccountTests.cs
- docs/API_ENDPOINT_INVENTORY.md
- docs/prompt_queues/backend_api_db_residuals_pass2_2026_07_11.md
- docs/prompt_queues/backend_test_coverage.md
- docs/prompt_queues/README.md
- .ai/runs/2026-08-01-BACKEND-API-DB-013-evidence.md

## Validation
Validation run: dotnet test tests/MathLearning.Tests/MathLearning.Tests.csproj -c Release --filter FullyQualifiedName~AuthIncompleteAccount|FullyQualifiedName~AuthMobileRegistrationAtomicity|FullyQualifiedName~AuthMobileRegistrationRelationalAtomicity|FullyQualifiedName~AuthDevSeedLogin => Passed 12/12
Validation not run: none - PostgreSQL race matrix and operator orphan backfill job deferred

## Exceptions and learning
Mistakes observed: none
Waste: none
Missed: historical orphan backfill/invalidation job; Flutter contract SHA sync; full PG concurrent register matrix
Follow-up: BACKEND-API-DB-013 residual orphan repair / Flutter sync if needed
Residual risk: Existing Identity-only rows remain until an explicit repair/invalidation job; login deny prevents new tokens for them.
Documentation impact: updated docs/API_ENDPOINT_INVENTORY.md, docs/prompt_queues/backend_api_db_residuals_pass2_2026_07_11.md, docs/prompt_queues/backend_test_coverage.md, docs/prompt_queues/README.md
Cross-repo impact: yes - deferred Flutter contract/status SHA sync; backend inventory updated

## Delivery
State: Needs merge
Branch/PR: cursor/backend-api-db-013-registration-owner-fa87
Commit SHA: self
Completion %: 75
