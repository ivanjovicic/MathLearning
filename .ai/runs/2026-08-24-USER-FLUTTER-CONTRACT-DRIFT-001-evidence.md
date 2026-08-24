# USER-FLUTTER-CONTRACT-DRIFT-001 Evidence

Evidence format: v2
Prompt ID: USER-FLUTTER-CONTRACT-DRIFT-001
Queue: user-assigned
Agent/tool: cursor-cloud
Model provider: unknown-not-exposed
Model name/id: unknown-not-exposed
Client/IDE: cursor-cloud
Run mode: investigation
Token budget: medium
Started at UTC: 2026-08-24T11:14:00Z
Completed at UTC: 2026-08-24T11:40:00Z
Elapsed time: ~26m
Relevant prior mistakes read: BACKEND-MISTAKE-EVIDENCE-001, BACKEND-MISTAKE-VALIDATION-001, BACKEND-MISTAKE-XREPO-001, BACKEND-MISTAKE-AUDIT-001
How this run avoids prior mistakes: one Flutter daily-run replay owner; docs aligned to runtime; no Flutter repo edits
Owner/hypothesis: Flutter economy contract requires original `awardedXp` on any-key Daily Run chest replay; runtime returned 0 and ledger-bound chest ids caused 409
Files inspected: 12
Files changed: 6
Searches: 4
Validation runs: 3
Failed retries: 1

## Outcome
- Domain-duplicate Daily Run claim replays original `awardedXp` and persisted season snapshot.
- Chest `transactionId` is no longer economy `operationId`, so a new idempotency key reaches alreadyClaimed replay instead of 409.
- Economy/API inventory contracts document any-key replay; cosmetics section numbering/anchors corrected.

## Changed paths
- `src/MathLearning.Api/Endpoints/EconomySettlementEndpoints.cs`
- `tests/MathLearning.Tests/Endpoints/SeasonDailyRunPostgresTests.cs`
- `tests/MathLearning.Tests/Endpoints/EconomySettlementEndpointsIntegrationTests.cs`
- `docs/mobile_economy_api_contract.md`
- `docs/mobile_api_contract.md`
- `docs/API_ENDPOINT_INVENTORY.md`

## Validation
Validation run: `python3 scripts/run_guarded.py --timeout-seconds 180 -- dotnet test ... --filter FullyQualifiedName~SeasonDailyRunClaim_OmittedSeasonId|...SeasonMilestone|...SeasonDailyRunClaim_Success` → Passed 10/10
Validation run: FlutterPayload|RewardTrack → Passed 8/8
Validation run: `python3 scripts/check_documentation_health.py --context src/MathLearning.Api/Endpoints/EconomySettlementEndpoints.cs`
Validation not run: none

## Exceptions and learning
Mistakes observed: none
Waste: first different-key HTTP test hit ledger 409 because chest id was operationId; fixed in same owner
Missed: cosmetics claim response still documents `inventory` while GET inventory uses `itemKeys`
Follow-up: cosmetics inventory vs claim response field alias if Flutter parsers diverge
Residual risk: none for Daily Run any-key replay on the in-memory HTTP path
Documentation impact: updated economy/mobile contracts and endpoint inventory
Cross-repo impact: yes - Flutter Daily Run retry with a new idempotency key can trust `awardedXp` as the original grant when `alreadyClaimed` is true

## Delivery
State: Done
Branch/PR: cursor/season-milestone-premium-gate-e301 / https://github.com/ivanjovicic/MathLearning/pull/25
Commit SHA: self
Completion %: 100
