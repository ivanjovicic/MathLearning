# BACKEND-SEASON-XP-SETTLEMENT-001 — Canonicalize season milestone XP settlement

Prompt contract: v2
Prompt admission: v3
Repository: ivanjovicic/MathLearning
Prompt ID: BACKEND-SEASON-XP-SETTLEMENT-001
Queue: docs/prompt_queues/backend_season_authority_residuals_2026_07_31.md
Run lane: known-fix
Token budget: medium
Timebox: 30 minutes

Problem evidence:
- The `xp` reward branch in `POST /api/seasons/milestones/{milestoneId}/claim` directly mutates `UserProfile.Xp` and recalculates `Level`.
- The documented `IXpTrackingService`/`XpTrackingService` owner updates total XP together with daily, weekly, monthly and level state and carries source metadata.
- Direct milestone mutation can therefore leave total XP, time-bucket XP, history/audit, leaderboard inputs and downstream reward hooks inconsistent even though the milestone claim succeeds.
- Calling the existing service outside the milestone transaction could create committed XP without a completed milestone/ledger, or a completed milestone without XP.

Deduplication check:
- `BACKEND-API-DB-015` owns stale pending recovery, not canonical XP accounting.
- `BACKEND-TEST-033` owns broad cancellation tests; this prompt owns the concrete milestone XP transaction seam and links shared failure injection.
- `BACKEND-SEASON-TRACK-AUTHORITY-001` consumes season XP for eligibility but does not award global XP.
- Existing XP tracking tests cover the service itself, not every season milestone caller.

Priority rationale: P1 correctness because successful rewards can silently corrupt ranking/time-window/accounting truth and create hard-to-reconcile user state.

Dependencies/collisions:
- Run after or coordinate with `BACKEND-SEASON-DAILY-RUN-PROVENANCE-001`; both may touch `EconomySettlementEndpoints.cs`.
- Reuse one canonical XP mutation service; do not add a second XP table or update counters independently in the endpoint.
- Preserve the existing economy transaction/milestone uniqueness boundary and exact replay response.
- Flutter baseline `0a75340c4c5ee20abd8f9351dc82fb2ad583e616` calls this route from `SeasonService.claimMilestone` with `seasonId`, `milestoneId` and a stable idempotency key.
- Preserve the accepted request/response shape. If status/error or progress fields change, create and link `XREPO-SEASON-SETTLEMENT-CONTRACT-001` and update the mobile backend contract status before delivery.
- If the current service cannot compose with the existing transaction in five product/test/doc paths, stop and create a separate service-refactor prompt.

Owner boundary:
- Own XP reward application from season milestone through the existing canonical XP accounting seam.
- Do not own season XP earning, reward-track eligibility, reset scheduler redesign, generic leaderboard parity, schema redesign or historical XP reconciliation.

Queue placement: P1 after the two direct season authorization defects because it fixes silent state divergence rather than a first-order eligibility bypass.

Task: Make a season milestone XP reward settle atomically through one canonical XP accounting path so total, time buckets, level, provenance and replay state cannot diverge.

Source of truth:
- `src/MathLearning.Api/Endpoints/EconomySettlementEndpoints.cs`
- current `IXpTrackingService` interface declaration
- `src/MathLearning.Infrastructure/Services/XpTrackingService.cs`
- `src/MathLearning.Domain/Entities/UserProfile.cs`
- XP history/source entities and `ApiDbContext` mappings
- leaderboard consumers of daily/weekly/monthly/all-time XP
- nearest milestone, XP tracking, concurrency and economy idempotency tests
- `XP_TRACKING_INTEGRATION.md`, `docs/mobile_economy_api_contract.md`, `docs/API_ENDPOINT_INVENTORY.md`
- verified Flutter SeasonService, its focused service test and mobile backend contract status at baseline `0a75340c4c5ee20abd8f9351dc82fb2ad583e616`; exact mobile paths are recorded in the owning audit evidence

Interpretation before work: Inventory every field/row/event the canonical XP service updates, then compare the milestone direct-write branch. Build `first claim -> canonical updates -> exact replay -> concurrent different key -> rollback` before editing.

Ambiguity rule: Do not assume all XP sources affect every leaderboard bucket. Preserve the canonical policy, make source type/id explicit and test it. Intentional exclusions belong in service policy, not endpoint drift.

Risk/ownership model:
- Milestone business identity and economy ledger remain the outer settlement authority.
- Canonical XP mutation must join the same `ApiDbContext` transaction and must not commit independently.
- A stable source identity such as `season:{seasonId}:milestone:{milestoneId}` prevents duplicate XP application even when transport keys differ.
- Replay reads the stored milestone result and performs zero XP mutation.
- Concurrent different-key claims for the same milestone create one milestone row and one canonical XP source effect.

Test-first contract:
- Pre-change proof: the focused season settlement test must fail before the transaction seam is corrected.
- Post-change proof: the same `python scripts/run_guarded.py --timeout-seconds 180 -- dotnet test tests/MathLearning.Tests/MathLearning.Tests.csproj --filter Season` command must pass after implementation.
- Counterexample cases: duplicate replay, concurrent different-key claim and rollback after XP staging.

Failure-mode matrix:
- Failure after XP fields change but before milestone claim/ledger completion.
- Failure after milestone row is staged but before XP source/history staging.
- Two different idempotency keys claim the same milestone concurrently.
- Same key is retried after response loss/restart.
- XP amount overflows or produces invalid level/bucket state.
- Reset job runs near settlement while time-bucket counters are updated.
- User/profile disappears or canonical XP source already exists from partial legacy behavior.

Execution packet:
- Initial reads: endpoint, interface, service, profile/mapping and nearest six tests/docs; maximum 12 files.
- Search budget: maximum 4 searches for direct XP writes, source/history entities, leaderboard bucket reads and transaction-aware service examples.
- First hypothesis/falsifier: milestone XP updates only total/level; falsify by asserting all canonical service-owned state after an XP milestone claim.
- Expected changed files: milestone endpoint, XP interface/service files, one focused test file and one contract doc; maximum 5 paths plus evidence.
- Stop trigger: schema redesign, reset scheduler overhaul, broad XP ledger refactor or historical repair belongs in a separate follow-up.

Owned paths:
- Season milestone XP reward branch.
- Small transaction-aware API on the existing XP service.
- One focused milestone XP atomicity/idempotency test file.
- One canonical XP/economy contract update.

Avoid paths:
- New XP schema/migration.
- Season Daily Run provenance.
- Reward-track eligibility/premium entitlement.
- Generic economy stale-pending recovery.
- Broad leaderboard, reset-job or anti-cheat redesign.
- Unrelated direct XP writes.

Documentation impact: update one canonical contract to state whether milestone XP affects all-time/daily/weekly/monthly values, its source identity, replay semantics and atomic relationship with milestone claim state. Preserve current Flutter shape or hand off any incompatible change through `XREPO-SEASON-SETTLEMENT-CONTRACT-001`.

Acceptance criteria:
1. First XP milestone claim updates exactly the canonical XP-owned fields/records and returns authoritative post-settlement state.
2. Endpoint no longer performs an independent `profile.Xp +=`/level formula outside the canonical service policy.
3. Milestone claim, XP mutation/source record and economy ledger completion commit or roll back together.
4. Exact replay performs zero additional XP/history/bucket mutation.
5. Different-key and concurrent claims for the same milestone settle XP once under PostgreSQL.
6. Overflow/invalid amount and injected failures leave all XP, claim and ledger state consistent.
7. Existing XP tracking, milestone reward and leaderboard-window tests remain green or are updated to the explicit policy.

Proof required:
- Before/after assertions for total, daily, weekly, monthly, level and existing XP history/source rows.
- Exact source identity and row-count proof.
- Failure injection before/after canonical XP staging with full rollback assertions.
- Provider-backed different-key same-milestone duplicate test.
- Response-loss replay with zero mutation.
- Contract diff and exact command evidence.

Validation:
```powershell
python scripts/run_guarded.py --timeout-seconds 180 -- dotnet test tests/MathLearning.Tests/MathLearning.Tests.csproj -c Release --filter FullyQualifiedName~SeasonMilestone
python scripts/run_guarded.py --timeout-seconds 180 -- dotnet build src/MathLearning.Api/MathLearning.Api.csproj -c Release --no-restore
python scripts/check_documentation_health.py --context src/MathLearning.Api/Endpoints/EconomySettlementEndpoints.cs
python scripts/validate_agent_evidence.py --changed-from <base-sha> --verify-git
```

Completion gate: No Done from matching total XP alone or calling the service after the outer commit. Done requires one transaction-aware canonical source application, replay/concurrency/rollback proof, contract sync and verified main delivery.

Stop conditions:
- Stop and create one narrow service-refactor follow-up if the canonical XP service cannot compose within the current transaction and path budget.
- Stop before redesigning reset jobs, leaderboards or all XP sources.
- Stop at five product/test/doc paths plus one evidence file, a second falsified design or the timebox.

Evidence: .ai/runs/<yyyy-mm-dd>-BACKEND-SEASON-XP-SETTLEMENT-001-evidence.md
