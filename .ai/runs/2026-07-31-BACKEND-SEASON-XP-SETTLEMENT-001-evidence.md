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
Completed at UTC: 2026-07-31T10:15:20Z
Elapsed time: 34m
Relevant prior mistakes read: BACKEND-MISTAKE-EVIDENCE-001, BACKEND-MISTAKE-VALIDATION-001, BACKEND-MISTAKE-XREPO-001, BACKEND-MISTAKE-AUDIT-001
How this run avoids prior mistakes: red DailyXp/bucket proof before patch; reuse existing IXpTrackingService; preserve Flutter request shape; re-prove after rebase when catalog hooks aborted settlement
Owner/hypothesis: milestone claim writes profile.Xp directly and bypasses IXpTrackingService buckets/history
Files inspected: 10
Files changed: 7
Searches: 5
Validation runs: 4
Failed retries: 2

## Outcome
- Season milestone `xp` rewards call `IXpTrackingService.AddXpAsync` with source `season_milestone` / `season:{seasonId}:milestone:{milestoneId}`.
- Canonical total/daily/weekly/monthly XP, level and `user_xp_events` update together; endpoint no longer does independent `profile.Xp +=`.
- Milestone settlement passes `evaluateProgressRewards: false` so cosmetics catalog readiness cannot abort XP settlement inside the claim transaction.
- Duplicate milestone claim performs zero additional XP/event mutation.
- Sync answer XP call uses named `ct:` so the new optional bool does not steal CancellationToken positionally.

## Changed paths
- src/MathLearning.Api/Endpoints/EconomySettlementEndpoints.cs
- src/MathLearning.Application/Services/IXpTrackingService.cs
- src/MathLearning.Infrastructure/Services/XpTrackingService.cs
- src/MathLearning.Infrastructure/Services/Sync/SyncService.cs
- tests/MathLearning.Tests/Endpoints/EconomySettlementEndpointsIntegrationTests.cs
- docs/mobile_economy_api_contract.md
- docs/API_ENDPOINT_INVENTORY.md
- docs/prompt_queues/backend_season_authority_residuals_2026_07_31.md
- .ai/runs/2026-07-31-BACKEND-SEASON-XP-SETTLEMENT-001-evidence.md

## Validation
Validation run: `python3 scripts/run_guarded.py --timeout-seconds 180 -- dotnet test tests/MathLearning.Tests/MathLearning.Tests.csproj -c Release --filter "FullyQualifiedName~SeasonMilestone|FullyQualifiedName~XpTrackingServiceTests"` → Passed 9 / 0
Validation run: SeasonMilestone filter alone after `evaluateProgressRewards` fix → Passed 4 / 0
Validation run: API Release build → Passed
Validation run: `python3 scripts/check_documentation_health.py --context src/MathLearning.Application/Services/IXpTrackingService.cs` → failures=0
Validation not run: PostgreSQL concurrency/rollback injection deferred

## Exceptions and learning
Mistakes observed: none
Waste: branch thrash + post-rebase 500 from ProcessProgressRewardsAsync catalog-not-ready when AddXpAsync always evaluated cosmetics progress
Missed: none
Follow-up: optional progress-reward evaluation default remains true for non-milestone callers; PostgreSQL concurrent different-key proof remains under provider lanes
Residual risk: AddXpAsync SaveChanges relies on outer EF transaction on relational providers; push/PR/main open
Documentation impact: updated docs/mobile_economy_api_contract.md (evaluateProgressRewards:false semantics) and docs/API_ENDPOINT_INVENTORY.md
Cross-repo impact: yes - Flutter request/response shape preserved

## Delivery
State: Needs merge
Branch/PR: agent/BACKEND-SEASON-XP-SETTLEMENT-001
Commit SHA: self
Completion %: 79
