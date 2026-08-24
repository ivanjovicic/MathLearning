# USER-SEASON-MILESTONE-CLAIM-WINDOW-001 Evidence

Evidence format: v2
Prompt ID: USER-SEASON-MILESTONE-CLAIM-WINDOW-001
Queue: user-assigned
Agent/tool: cursor-cloud
Model provider: unknown-not-exposed
Model name/id: unknown-not-exposed
Client/IDE: cursor-cloud
Run mode: known-fix
Token budget: medium
Started at UTC: 2026-08-24T11:22:00Z
Completed at UTC: open
Elapsed time: open
Relevant prior mistakes read: BACKEND-MISTAKE-EVIDENCE-001, BACKEND-MISTAKE-VALIDATION-001, BACKEND-MISTAKE-XREPO-001, BACKEND-MISTAKE-AUDIT-001
How this run avoids prior mistakes: one shared Domain claim-window policy; no Flutter edits; focused reward_lock/completed counterexamples
Owner/hypothesis: milestone ResolveActiveSeasonAsync ignored cosmetics reward_lock claim window from 42f5a90; falsifier = reward_lock season claims succeed and completed seasons stay 409 invalid_season
Files inspected: 6
Files changed: 5
Searches: 2
Validation runs: 0
Failed retries: 0

## Outcome
- Extracted `SeasonClaimWindow.IsAccessible` and wired milestone season resolution to it.
- Cosmetics reward-track resolver now delegates to the same Domain policy.
- Added reward_lock allow + completed deny milestone regressions.

## Changed paths
- `src/MathLearning.Domain/Entities/CosmeticItem.cs`
- `src/MathLearning.Infrastructure/Services/Cosmetics/CosmeticPlatformService.Helpers.cs`
- `src/MathLearning.Api/Endpoints/EconomySettlementEndpoints.cs`
- `tests/MathLearning.Tests/Endpoints/EconomySettlementEndpointsIntegrationTests.cs`
- `docs/mobile_economy_api_contract.md`
- `docs/API_ENDPOINT_INVENTORY.md`

## Validation
Validation run: pending with SeasonMilestone focused filter after queue clear
Validation not run: none after queue clear

## Exceptions and learning
Mistakes observed: none
Waste: none
Missed: none
Follow-up: none for claim-window; premium entitlement storage remains separate intentional residual
Residual risk: focused HTTP proof still pending in this environment
Documentation impact: updated `docs/mobile_economy_api_contract.md`, `docs/API_ENDPOINT_INVENTORY.md`
Cross-repo impact: yes - Flutter SeasonService can claim during reward_lock using the same window as cosmetics reward-track docs; no Flutter code in this repo

## Delivery
State: Needs validation
Branch/PR: cursor/season-milestone-premium-gate-e301
Commit SHA: self
Completion %: 70
