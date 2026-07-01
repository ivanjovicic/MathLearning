# BACKEND-CRIT-005 Evidence

Prompt ID: BACKEND-CRIT-005
Queue: `docs/prompt_queues/backend_critical_risk_prevention.md`
Run mode: implementation/test
Relevant prior mistakes read: BACKEND-MISTAKE-AUDIT-001, BACKEND-MISTAKE-EVIDENCE-001, BACKEND-MISTAKE-VALIDATION-001, BACKEND-MISTAKE-XREPO-001

## Files changed

- `src/MathLearning.Api/Endpoints/EconomySettlementEndpoints.cs`
- `tests/MathLearning.Tests/Endpoints/EconomySettlementEndpointsIntegrationTests.cs`
- `tests/MathLearning.Tests/Contracts/MobileEconomyContractIntegrationTests.cs`

## What was done

- Fixed settlement snapshot truth: `BuildSeasonStateAsync` now merges tracked season progress and pending milestone claims before building first response.
- Season daily-run claim first response includes awarded XP in `season.earnedXp` / `season.level`.
- Season milestone claim first response includes `claimedMilestoneIds` and reward payload (coins/type).
- Strengthened idempotency replay assertions for settled season/reward fields (ledger replay and already-claimed paths).

## Validation run

```bash
dotnet test --filter "FullyQualifiedName~EconomySettlementEndpointsIntegrationTests|FullyQualifiedName~MobileEconomyContractIntegrationTests"
```

**Passed: 47, Failed: 0**

Note: broad filter `SeasonDailyRunClaim|SeasonMilestone|Economy|Idempotency|Contract` includes unrelated contract tests that can flake under rate limiting (429); focused settlement suite is the proof for this prompt.

## Risk prevented

- **settlement-snapshot-truth**: first settlement response and idempotency replay no longer return stale season state missing the mutation being settled.

## Commit SHA

b11f083
