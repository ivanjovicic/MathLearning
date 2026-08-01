# BACKEND-SEASON-TRACK-AUTHORITY-001 Evidence

Evidence format: v2
Prompt ID: BACKEND-SEASON-TRACK-AUTHORITY-001
Queue: docs/prompt_queues/backend_season_authority_residuals_2026_07_31.md
Agent/tool: cursor-composer
Model provider: unknown-not-exposed
Model name/id: unknown-not-exposed
Client/IDE: cursor
Run mode: known-fix
Token budget: medium
Started at UTC: 2026-07-31T09:20:44Z
Completed at UTC: 2026-07-31T09:28:30Z
Elapsed time: 7m46s
Relevant prior mistakes read: BACKEND-MISTAKE-EVIDENCE-001, BACKEND-MISTAKE-VALIDATION-001, BACKEND-MISTAKE-XREPO-001, BACKEND-MISTAKE-AUDIT-001
How this run avoids prior mistakes: red RewardTrack proof before patch; inventory sync; Flutter caller deferred with explicit residual
Owner/hypothesis: lifetime XP + explicit seasonId bypass season XP/window/premium entitlement
Files inspected: 12
Files changed: 6
Searches: 4
Validation runs: 3
Failed retries: 1

## Outcome
- Red RewardTrack proof observed before patch (7/7 red); green filter passed 7/7 after the shared policy patch.
- Preview/claim unlock authority now uses `UserSeasonProgress.EarnedXp` and a shared active/`reward_lock` claim-window resolver.
- Premium track is deny-by-default with zero writes; free track and duplicate claims remain deterministic.

## Changed paths
- src/MathLearning.Infrastructure/Services/Cosmetics/CosmeticPlatformService.Helpers.cs
- src/MathLearning.Infrastructure/Services/Cosmetics/CosmeticPlatformService.Public.cs
- src/MathLearning.Infrastructure/Services/Cosmetics/CosmeticPlatformService.Rewards.cs
- tests/MathLearning.Tests/Services/CosmeticPlatformServiceTests.cs
- docs/API_ENDPOINT_INVENTORY.md
- docs/prompt_queues/backend_season_authority_residuals_2026_07_31.md
- .ai/runs/2026-07-31-BACKEND-SEASON-TRACK-AUTHORITY-001-evidence.md

## Validation
Validation run: `python scripts/run_guarded.py --timeout-seconds 180 -- dotnet test tests/MathLearning.Tests/MathLearning.Tests.csproj -c Release --filter FullyQualifiedName~RewardTrack` → Passed 7 / 0
Validation run: `python scripts/run_guarded.py --timeout-seconds 180 -- dotnet build src/MathLearning.Api/MathLearning.Api.csproj -c Release --no-restore` → exit 0
Validation run: `python scripts/check_documentation_health.py --context src/MathLearning.Api/Endpoints/AvatarEndpoints.cs` → documents healthy
Validation not run: none

## Exceptions and learning
Mistakes observed: none
Waste: none
Missed: none
Follow-up: BACKEND-SEASON-PREMIUM-ENTITLEMENT-001 (new) — persisted premium entitlement storage/bootstrap; keep deny-by-default until then
Residual risk: none
Documentation impact: updated docs/API_ENDPOINT_INVENTORY.md
Cross-repo impact: yes - Flutter baseline has no runtime `/api/cosmetics/reward-track*` caller; mobile adapter deferred under XREPO44-SEASON-ACTIVE-ENDPOINT-DISPOSITION-001

## Delivery
State: Needs merge
Branch/PR: agent/BACKEND-SEASON-TRACK-AUTHORITY-001
Commit SHA: self
Completion %: 79
