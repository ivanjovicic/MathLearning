# USER-SEASON-MILESTONE-PREMIUM-001 Evidence

Evidence format: v2
Prompt ID: USER-SEASON-MILESTONE-PREMIUM-001
Queue: user-assigned
Agent/tool: cursor-cloud
Model provider: unknown-not-exposed
Model name/id: unknown-not-exposed
Client/IDE: cursor-cloud
Run mode: known-fix
Token budget: medium
Started at UTC: 2026-08-24T11:10:00Z
Completed at UTC: open
Elapsed time: open
Relevant prior mistakes read: BACKEND-MISTAKE-EVIDENCE-001, BACKEND-MISTAKE-VALIDATION-001, BACKEND-MISTAKE-XREPO-001, BACKEND-MISTAKE-AUDIT-001
How this run avoids prior mistakes: one season-milestone authority owner; shared Domain premium policy; contract + focused regression; no Flutter path edits
Owner/hypothesis: `POST /api/seasons/milestones/{id}/claim` bypassed cosmetics premium deny-by-default; falsifier = premium TrackType claim returns 409 premium_required with zero side effects
Files inspected: 12
Files changed: 6
Searches: 4
Validation runs: 0
Failed retries: 0

## Outcome
- Premium-track milestone claims now share `SeasonRewardTrackAccess` deny-by-default with cosmetics reward-track.
- Focused regression `SeasonMilestone_PremiumTrack_WithoutEntitlement_IsDeniedAndWritesNoClaim` added.
- Mobile economy contract + endpoint inventory updated for `409 premium_required`.

## Changed paths
- `src/MathLearning.Domain/Entities/CosmeticItem.cs`
- `src/MathLearning.Infrastructure/Services/Cosmetics/CosmeticPlatformService.Helpers.cs`
- `src/MathLearning.Api/Endpoints/EconomySettlementEndpoints.cs`
- `tests/MathLearning.Tests/Endpoints/EconomySettlementEndpointsIntegrationTests.cs`
- `docs/mobile_economy_api_contract.md`
- `docs/API_ENDPOINT_INVENTORY.md`

## Validation
Validation run: pending - deferred while follow-up queue non-empty; will run focused SeasonMilestone filter
Validation not run: none after queue clear

## Exceptions and learning
Mistakes observed: none
Waste: none
Missed: milestone `ResolveActiveSeasonAsync` still lacks cosmetics `reward_lock` claim-window parity
Follow-up: residual season claim-window alignment for milestones; persisted premium entitlement owner still absent (deny-by-default intentional)
Residual risk: PostgreSQL concurrency not re-proven in this slice; entitlement storage still missing by design
Documentation impact: updated `docs/mobile_economy_api_contract.md`, `docs/API_ENDPOINT_INVENTORY.md`
Cross-repo impact: yes - Flutter SeasonService should treat `premium_required` as non-retryable business denial; no Flutter repo edits in this backend run

## Delivery
State: Needs validation
Branch/PR: cursor/season-milestone-premium-gate-e301
Commit SHA: self
Completion %: 70
