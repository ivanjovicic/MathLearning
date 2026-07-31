# BACKEND-SEASON-DAILY-RUN-PROVENANCE-001 — Bind Daily Run chest provenance to the correct season

Prompt contract: v2
Prompt admission: v3
Repository: ivanjovicic/MathLearning
Prompt ID: BACKEND-SEASON-DAILY-RUN-PROVENANCE-001
Queue: docs/prompt_queues/backend_season_authority_residuals_2026_07_31.md
Run lane: known-fix
Token budget: medium
Timebox: 30 minutes

Problem evidence:
- `POST /api/seasons/daily-run-claim` verifies an authenticated user's `DailyRunChestClaim` by transaction ID and derives the awarded XP from that row.
- The endpoint resolves the requested/current active season, but does not verify that `DailyRunChestClaim.Day` falls within that season's start/end ownership window.
- An old, previously unsettled chest transaction can therefore be submitted while a later season is active and fund that later season's `UserSeasonProgress`.
- Existing uniqueness prevents the same chest from funding two seasons, but it does not prevent first settlement into the wrong season.

Deduplication check:
- `BACKEND-API-DB-009` owns cosmetic entitlement provenance after a valid chest/season settlement; it assumes the season settlement itself is valid.
- `BACKEND-API-DB-015` owns stale pending/recovery behavior, not the chest-to-season business identity.
- Existing Daily Run idempotency intentionally owns one chest per user/day and transaction, but does not decide which season may consume that chest.

Priority rationale: P0 progression integrity because users can bank prior chest transactions and transfer XP/fragment eligibility across season boundaries without forging a transaction.

Dependencies/collisions:
- Preserve `DailyRunChestClaim` as the chest authority and existing domain-table idempotency policy.
- Preserve economy ledger replay/conflict semantics; use the chest's stored day rather than request-provided XP/date.
- Coordinate the season-window definition with `BACKEND-SEASON-TRACK-AUTHORITY-001` so reward access and Daily Run earning agree on season boundaries.
- Do not run concurrently with `BACKEND-SEASON-XP-SETTLEMENT-001`; both may edit `EconomySettlementEndpoints.cs`.

Owner boundary:
- Own only the mapping and validation from a settled Daily Run chest to one season progress record and its replay response.
- Do not own chest reward generation, schema backfill, generic cosmetics fragment consumption, reward-track premium policy, XP bucket tracking or stale ledger recovery.

Queue placement: second P0 row because the endpoint already uses a trusted transaction but omits a critical temporal/season binding.

Task: Make each Daily Run chest eligible for exactly the season whose authoritative window contains the chest day, and make first, replay, wrong-season and boundary behavior deterministic.

Source of truth:
- `src/MathLearning.Api/Endpoints/EconomySettlementEndpoints.cs`
- `src/MathLearning.Api/Endpoints/DailyRunEndpoints.cs`
- `src/MathLearning.Api/Endpoints/DailyRunChestClaimIdempotency.cs`
- `src/MathLearning.Api/Endpoints/DailyRunCosmeticsSettlement.cs`
- `src/MathLearning.Domain/Entities/EconomyEntities.cs`
- `src/MathLearning.Domain/Entities/CosmeticItem.cs`
- `src/MathLearning.Infrastructure/Persistance/ApiDbContext.cs`
- nearest Daily Run, season settlement, idempotency and fragment trust-boundary tests
- `docs/mobile_economy_api_contract.md`, `docs/backend_contract_gap_report.md`

Interpretation before work: Build the matrix `chest day before/start/inside/end/after season × explicit/implicit season × first/replay/different key × overlapping/missing season data` before editing.

Ambiguity rule: Define date semantics explicitly. `DailyRunChestClaim.Day` is a `DateOnly`; compare it against one canonical season calendar window derived once and tested at exact boundaries. Do not compare request timestamps.

Risk/ownership model:
- Eligibility authority is the persisted chest row plus the persisted season window.
- A chest is consumed by at most one season; use existing claim provenance and uniqueness rather than adding schema in this phase.
- Wrong-season attempts do not mutate progress, fragment eligibility, claim tables or completed economy results that falsely look successful.
- Same business transaction with a new transport key replays the original result or returns a stable wrong-season conflict; it never projects the old claim onto a new season.
- Overlapping active seasons follow one deterministic policy or fail closed.

Failure-mode matrix:
- Chest day predates current season but transaction was never season-settled.
- Chest day is after the requested season end or before its start.
- Chest occurs exactly on start/end boundary.
- Explicit season ID is active now but not the season containing chest day.
- Two active windows overlap or no season contains the day.
- Same chest is submitted with different idempotency keys or season IDs.
- Original claim is replayed after the season becomes completed/archived.
- Cancellation occurs before commit or after commit before response.

Execution packet:
- Initial reads: the seven runtime sources above plus nearest five tests; maximum 12 files.
- Search budget: maximum 4 searches for season date conversion, existing constraints, mobile date assumptions and replay tests.
- First hypothesis/falsifier: any valid chest can currently fund whichever season is active at submission; falsify with an integration test using an old chest and a new active season.
- Expected changed files: season endpoint, optional existing helper, one focused test file and one contract doc; maximum 4 paths plus evidence.
- Stop trigger: schema/backfill, generic ledger recovery, reward-track policy, chest reward redesign or Flutter UI belongs elsewhere.

Owned paths:
- `season_daily_run_claim` eligibility and replay semantics.
- Chest-day to season-window resolver in an existing file/helper.
- Focused provider-backed provenance tests.
- One canonical season Daily Run contract update.

Avoid paths:
- New migration or production-row backfill.
- Cosmetics reward-track services (`BACKEND-SEASON-TRACK-AUTHORITY-001`).
- Milestone XP awarding (`BACKEND-SEASON-XP-SETTLEMENT-001`).
- Daily Run chest reward amounts/idempotency Policy B.
- Generic stale pending recovery (`BACKEND-API-DB-015`).

Documentation impact: update one canonical backend/mobile economy contract with chest-day ownership, boundaries, wrong-season error, overlap policy and replay behavior.

Acceptance criteria:
1. A chest whose day is outside the selected season cannot increase that season's XP or create fragment eligibility/season claim rows.
2. A valid in-window chest increases exactly one `UserSeasonProgress` through existing persisted provenance.
3. Boundary dates follow one documented and tested rule.
4. Explicit `seasonId` cannot override chest-day ownership; omitted season resolution produces the same owner.
5. Same chest under another key/season cannot mint again or return a misleading current-season success projection.
6. Duplicate attempts settle once with deterministic replay/conflict and exact row/progress counts on PostgreSQL.
7. Replay after season closure returns the original settled result without revalidating it into another season.

Proof required:
- Raw HTTP/integration tests for old-to-new season banking, future/out-of-window chest and exact boundaries.
- Explicit-vs-implicit season selection parity.
- Cross-season same-transaction replay/conflict assertions.
- Provider-backed duplicate test with exact XP and row counts.
- Cancellation/response-loss proof linked rather than duplicated with `BACKEND-TEST-033/015`.
- Contract diff and exact validation results.

Validation:
```powershell
python scripts/run_guarded.py --timeout-seconds 240 -- dotnet test tests/MathLearning.Tests/MathLearning.Tests.csproj -c Release --filter FullyQualifiedName~SeasonDailyRun
python scripts/run_guarded.py --timeout-seconds 180 -- dotnet build src/MathLearning.Api/MathLearning.Api.csproj -c Release --no-restore
python scripts/check_documentation_health.py --context src/MathLearning.Api/Endpoints/EconomySettlementEndpoints.cs
python scripts/validate_agent_evidence.py --changed-from <base-sha> --verify-git
```

Completion gate: No Done from transaction-ID existence alone, an in-memory lock or request-date validation. Done requires persisted chest-day ownership, deterministic wrong-season/replay semantics, provider proof, contract sync and verified main delivery.

Stop conditions:
- Stop and create a separate migration/backfill prompt if existing persisted fields cannot express the invariant safely; do not add schema here.
- Stop before changing chest reward generation or premium reward-track rules.
- Stop at five product/test/doc paths plus one evidence file, a second falsified design or the timebox.

Evidence: .ai/runs/<yyyy-mm-dd>-BACKEND-SEASON-DAILY-RUN-PROVENANCE-001-evidence.md