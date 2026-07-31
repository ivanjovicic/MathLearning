# BACKEND-SEASON-XP-SETTLEMENT-001 Evidence

Evidence format: v2
Prompt ID: BACKEND-SEASON-XP-SETTLEMENT-001
Queue: docs/prompt_queues/backend_season_authority_residuals_2026_07_31.md
Agent/tool: cursor-composer
Model provider: unknown-not-exposed
Model name/id: unknown-not-exposed
Client/IDE: cursor
Run mode: known-fix
Token budget: medium
Started at UTC: 2026-07-31T09:41:00Z
Completed at UTC: 2026-07-31T09:50:00Z
Elapsed time: 9m
Relevant prior mistakes read: BACKEND-MISTAKE-EVIDENCE-001, BACKEND-MISTAKE-VALIDATION-001, BACKEND-MISTAKE-XREPO-001, BACKEND-MISTAKE-AUDIT-001
How this run avoids prior mistakes: red DailyXp/bucket proof before patch; reuse existing IXpTrackingService; preserve Flutter request shape; keep cosmetic reward processing best-effort when catalog readiness is missing
Owner/hypothesis: milestone claim writes profile.Xp directly and bypasses IXpTrackingService buckets/history; cosmetic reward processing can abort XP settlement when catalog readiness is missing
Files inspected: 8
Files changed: 6
Searches: 4
Validation runs: 4
Failed retries: 1

## Outcome
- Season milestone `xp` rewards now call `IXpTrackingService.AddXpAsync` with source `season_milestone` / `season:{seasonId}:milestone:{milestoneId}`.
- Canonical total/daily/weekly/monthly XP, level and `user_xp_events` update together; endpoint no longer does independent `profile.Xp +=`.
- Duplicate milestone claim performs zero additional XP/event mutation.
- XP settlement remains authoritative even when cosmetic reward processing is unavailable; that side effect now logs and skips when the catalog is not ready.

## Changed paths
- src/MathLearning.Api/Endpoints/EconomySettlementEndpoints.cs
- tests/MathLearning.Tests/Endpoints/EconomySettlementEndpointsIntegrationTests.cs
- src/MathLearning.Infrastructure/Services/XpTrackingService.cs
- docs/mobile_economy_api_contract.md
- docs/API_ENDPOINT_INVENTORY.md
- docs/prompt_queues/backend_season_authority_residuals_2026_07_31.md
- .ai/runs/2026-07-31-BACKEND-SEASON-XP-SETTLEMENT-001-evidence.md

## Validation
Validation run: `python scripts/run_guarded.py --timeout-seconds 180 -- dotnet test tests/MathLearning.Tests/MathLearning.Tests.csproj -c Release --filter FullyQualifiedName~SeasonMilestone` → Passed 3 / 0
Validation not run: PostgreSQL concurrency/rollback injection deferred

## Exceptions and learning
Mistakes observed: none
Waste: branch thrash from external checkout mid-run; reapplied patch on owner branch
Missed: none
Follow-up: none - PostgreSQL concurrent different-key proof remains available under existing provider lanes
Residual risk: cosmetic progress rewards are best-effort when catalog readiness is missing; push/PR/main open
Documentation impact: updated docs/mobile_economy_api_contract.md and docs/API_ENDPOINT_INVENTORY.md
Cross-repo impact: yes - Flutter request/response shape preserved

## Delivery
State: Needs merge
Branch/PR: agent/BACKEND-SEASON-XP-SETTLEMENT-001
Commit SHA: self
Completion %: 79
