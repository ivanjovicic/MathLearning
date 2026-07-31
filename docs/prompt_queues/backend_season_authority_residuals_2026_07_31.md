# Backend Season Authority Residual Queue â€” 2026-07-31

Target repo: `ivanjovicic/MathLearning`  
Reviewed head: `c33ce1b9b5bae3bce579594c297924ee486d12ee`  
Source: focused static audit of current season, Daily Run, cosmetics reward-track and XP settlement code  
Audit evidence: `.ai/runs/2026-07-31-BACKEND-SEASON-AUDIT-001-evidence.md`  
Scope: newly identified server-authority and consistency defects not owned by the existing account, idempotency-recovery, sync, leaderboard or adaptive/practice prompts

## Why this queue exists

The current backend has separate season authorities that disagree:

- cosmetics reward-track preview/claim reads lifetime `UserProfile.Xp` rather than `UserSeasonProgress.EarnedXp`;
- an explicit `seasonId` bypasses active/date checks in the cosmetics reward-track resolver;
- premium reward-track entries have no user entitlement gate;
- season Daily Run settlement proves that a chest exists, but not that the chest day belongs to the selected season;
- season milestone XP writes `UserProfile.Xp` directly and bypasses the canonical XP tracking service and its time-bucket/history invariants.

These issues are not the stale-pending recovery owned by `BACKEND-API-DB-015`, the generic cosmetics entitlement work delivered under `BACKEND-API-DB-009`, or the cancellation matrix owned by `BACKEND-TEST-033`. Each prompt below must preserve those owners and extend only the uncovered season-specific invariant.

## Queue rules

1. Read `AGENTS.md`, `docs/prompt_queues/README.md`, the linked prompt packet and only the relevant mistake IDs selected through `docs/ai/learning/MISTAKE_INDEX.json`; do not open the full mistake ledger unless updating a card.
2. Create `.ai/runs/<date>-<prompt-id>-evidence.md` before runtime changes.
3. Re-check current `main`, open PRs and active claims immediately before claim.
4. Keep one canonical owner per invariant. Do not reopen `BACKEND-API-DB-009`, `BACKEND-API-DB-015` or `BACKEND-TEST-033` under a new implementation name.
5. Use authenticated server identity, persisted season state and database constraints as authority. Request `seasonId`, `trackType`, transaction text or idempotency keys are selectors, not proof of entitlement.
6. Provider-sensitive completion requires PostgreSQL transaction/concurrency evidence. In-memory or SQLite-only success is not enough for uniqueness/locking claims.
7. Contract changes must update backend inventory/contracts and record any Flutter compatibility handoff.
8. Do not mark Done from static inspection, generated tests or queued CI. Record exact commands, results and commit SHA.

## Active prompts

| ID | Priority | Status | Prompt | Purpose |
|---|---:|---|---|---|
| `BACKEND-SEASON-TRACK-AUTHORITY-001` | P0 | Done | [Open](backend_season_authority/BACKEND-SEASON-TRACK-AUTHORITY-001.md) | Make reward-track preview and claim use season XP, active/reward-window truth and explicit premium entitlement. Done 79% â€” Run log: `.ai/runs/2026-07-31-BACKEND-SEASON-TRACK-AUTHORITY-001-evidence.md`; Validation: RewardTrack filter 7/7 + API Release build + docs health context AvatarEndpoints; Residual risk: premium remains fail-closed until persisted entitlement storage owner; push/PR/main verification open; Commit: self. |
| `BACKEND-SEASON-DAILY-RUN-PROVENANCE-001` | P0 | In progress | [Open](backend_season_authority/BACKEND-SEASON-DAILY-RUN-PROVENANCE-001.md) | Prevent old/future/out-of-season Daily Run chest transactions from funding the wrong season. Branch: `agent/BACKEND-SEASON-DAILY-RUN-PROVENANCE-001`. |
| `BACKEND-SEASON-XP-SETTLEMENT-001` | P1 correctness | Done | [Open](backend_season_authority/BACKEND-SEASON-XP-SETTLEMENT-001.md) | Route milestone XP through one canonical transaction-aware XP settlement path without bucket/history drift. Done 79% — Run log: `.ai/runs/2026-07-31-BACKEND-SEASON-XP-SETTLEMENT-001-evidence.md`; Validation: SeasonMilestone 3/3; Residual risk: PostgreSQL concurrency/rollback and push/PR/main open; Commit: self. |

## Canonical order and collision notes

1. `BACKEND-SEASON-TRACK-AUTHORITY-001` owns cosmetics reward-track read/claim authority in `AvatarEndpoints` and `CosmeticPlatformService`.
2. `BACKEND-SEASON-DAILY-RUN-PROVENANCE-001` owns the season Daily Run branch in `EconomySettlementEndpoints`.
3. `BACKEND-SEASON-XP-SETTLEMENT-001` owns only the milestone XP branch and canonical XP-service seam. It must not run concurrently with `BACKEND-SEASON-DAILY-RUN-PROVENANCE-001` because both may touch `EconomySettlementEndpoints.cs`.
4. Each medium implementation packet is capped at five product/test/doc paths plus one compact evidence file. Schema/bootstrap or a sixth product path requires a follow-up split before editing.
5. If implementation proves that premium milestone and cosmetics reward-track access require one shared entitlement abstraction, keep `BACKEND-SEASON-TRACK-AUTHORITY-001` as the policy owner and add only the smallest adapter in the milestone path; do not create another premium system.

## Audit evidence summary

- `CosmeticPlatformService.Public.GetRewardTrackAsync` calculates unlocks from `UserProfile.Xp` and resolves any explicitly requested season by ID without active/date filtering.
- `CosmeticPlatformService.Rewards.ClaimRewardTrackTierAsync` repeats the lifetime-XP check and accepts the requested `TrackType`; the domain declares `free` and `premium`, but the claim path has no per-user premium entitlement check.
- `POST /api/seasons/daily-run-claim` derives XP from a real `DailyRunChestClaim`, but does not validate `DailyRunChestClaim.Day` against the selected season's start/end window.
- `POST /api/seasons/milestones/{milestoneId}/claim` directly increments `profile.Xp`, while `IXpTrackingService` is the documented owner of total, daily, weekly, monthly and level updates.

## Validation for prompt-only changes

```powershell
python scripts/validate_agent_prompt.py --changed-from <base-sha>
python scripts/validate_agent_evidence.py --changed-from <base-sha> --verify-git
python scripts/check_documentation_health.py --full-links
```

Runtime validation is specified inside each prompt packet.
