# BACKEND-SEASON-TRACK-AUTHORITY-001 — Enforce season-scoped reward-track authority

Prompt contract: v2
Prompt admission: v3
Repository: ivanjovicic/MathLearning
Prompt ID: BACKEND-SEASON-TRACK-AUTHORITY-001
Queue: docs/prompt_queues/backend_season_authority_residuals_2026_07_31.md
Run lane: known-fix
Token budget: medium
Timebox: 30 minutes

Problem evidence:
- `CosmeticPlatformService.Public.GetRewardTrackAsync` reads `UserProfile.Xp` and uses lifetime XP to mark season tiers unlocked.
- `CosmeticPlatformService.Rewards.ClaimRewardTrackTierAsync` repeats the lifetime-XP check instead of reading `UserSeasonProgress.EarnedXp` for the selected season.
- `ResolveSeasonAsync` validates active/date state only when no `seasonId` is supplied; an explicit ID resolves draft, scheduled, future, completed or archived seasons.
- The domain supports `free` and `premium` track types, but the authenticated claim path contains no persisted user premium entitlement check.

Deduplication check:
- `BACKEND-API-DB-009` replaced client-declared item/fragment grants with server-issued entitlement; it did not establish season XP, season-window or premium-track authority for `/api/cosmetics/reward-track*`.
- `BACKEND-API-DB-015` owns stale pending ledger recovery and must not be duplicated here.
- Existing season milestone settlement uses `UserSeasonProgress.EarnedXp`, proving a season-scoped authority already exists, but the cosmetics reward-track path does not consume it.

Priority rationale: P0 economy/cosmetics correctness because a user with historical lifetime XP can unlock a new season immediately, and explicit inactive/premium track selectors can expose or grant rewards without the intended season/access prerequisite.

Dependencies/collisions:
- Coordinate premium policy with the season milestone route, but keep this prompt as the canonical policy owner.
- Reuse existing season progress and any existing persisted premium entitlement. If none exists, deny premium access and create a separate storage/bootstrap prompt rather than adding schema here.
- Preserve cosmetics item provenance, reward claim uniqueness and stale-pending ownership from `BACKEND-API-DB-009/015`.
- Flutter baseline `0a75340c4c5ee20abd8f9351dc82fb2ad583e616` has no runtime `/api/cosmetics/reward-track*` caller; active-season identity disposition is owned by `XREPO44-SEASON-ACTIVE-ENDPOINT-DISPOSITION-001` and `lib/services/season_service.dart`.
- Do not expose a changed reward-track contract to mobile without updating `docs/mobile_backend_contract_status.md` and linking a named mobile implementation owner.
- Do not redesign billing or introduce a payment provider.

Owner boundary:
- Own reward-track season selection, unlock calculation, premium fail-closed access, claim behavior and contract tests.
- Do not own general season XP earning, Daily Run-to-season provenance, premium storage/bootstrap, package billing, Flutter purchase UI or generic cosmetics idempotency recovery.

Queue placement: first P0 row because this is a direct reward authorization bypass visible in both preview and mutation paths.

Task: Make reward-track reads and claims derive eligibility from one active/reward-claimable season, that user's persisted season XP and explicit server-owned track access, with deterministic duplicate behavior.

Source of truth:
- `src/MathLearning.Api/Endpoints/AvatarEndpoints.cs`
- `src/MathLearning.Infrastructure/Services/Cosmetics/CosmeticPlatformService.Public.cs`
- `src/MathLearning.Infrastructure/Services/Cosmetics/CosmeticPlatformService.Rewards.cs`
- `src/MathLearning.Infrastructure/Services/Cosmetics/CosmeticPlatformService.Helpers.cs`
- `src/MathLearning.Domain/Entities/CosmeticItem.cs`
- `src/MathLearning.Domain/Entities/EconomyEntities.cs`
- `src/MathLearning.Infrastructure/Persistance/ApiDbContext.cs`
- nearest cosmetics reward-track and season integration tests
- `docs/mobile_economy_api_contract.md`, `docs/API_ENDPOINT_INVENTORY.md`
- Flutter `lib/services/season_service.dart`, `docs/mobile_backend_contract_status.md` and `XREPO44-SEASON-ACTIVE-ENDPOINT-DISPOSITION-001` at baseline `0a75340c4c5ee20abd8f9351dc82fb2ad583e616`

Interpretation before work: Build the matrix `season status/window × explicit/implicit season ID × free/premium track × season XP × lifetime XP × entitlement × first/duplicate claim` before editing.

Ambiguity rule: Lifetime profile XP may be displayed separately, but it cannot authorize a season tier. An explicit season ID may select only a season that the documented claim-window policy permits. Premium is deny-by-default without persisted server proof.

Risk/ownership model:
- Unlock authority is `authenticated user + selected season + UserSeasonProgress.EarnedXp + active reward entry`.
- Premium authority additionally requires persisted user/season proof; request text is never proof.
- Preview and claim must call the same eligibility policy and return compatible reasons.
- Existing database reward-claim uniqueness remains authoritative; this packet adds focused duplicate regression proof without redesigning generic idempotency.
- Draft/future/archived season metadata and rewards must not be exposed through ordinary mobile routes unless an explicit safe read policy says otherwise.

Failure-mode matrix:
- User has 50,000 lifetime XP but zero XP in the new season.
- User supplies a future/draft/archived `seasonId` directly.
- User requests `trackType=premium` without entitlement, with another user's entitlement, or after revocation/expiry.
- Preview says locked while claim succeeds, or preview says claimable while claim uses another authority.
- Duplicate claim is delivered with a different transport key.
- Reward item is missing/inactive or catalog readiness changes between read and claim.
- Season crosses its end/reward-lock boundary during a request.

Execution packet:
- Initial reads: the eight source files above plus the nearest four tests; maximum 12 files.
- Search budget: maximum 4 searches for premium entitlement models, reward-track callers, unique indexes and season status policy.
- First hypothesis/falsifier: global XP plus explicit season selection bypasses season progression; falsify with endpoint/service tests where lifetime XP is high and season XP is low.
- Expected changed files: endpoint, up to two cosmetics policy/service files, one focused test file and one contract doc; maximum 5 paths plus evidence.
- Stop trigger: schema/bootstrap, payment integration, generic idempotency recovery, broad season redesign or Flutter implementation belongs in a separate owner.

Owned paths:
- Reward-track read/claim eligibility and shared policy.
- Active/reward-window season resolver used by reward-track mobile routes.
- Premium deny-by-default adapter using existing persisted proof only.
- Focused reward-track duplicate tests and one backend contract update.

Avoid paths:
- New premium schema/migration/bootstrap.
- `POST /api/seasons/daily-run-claim` provenance (`BACKEND-SEASON-DAILY-RUN-PROVENANCE-001`).
- Milestone XP mutation (`BACKEND-SEASON-XP-SETTLEMENT-001`).
- Generic economy/cosmetics pending recovery (`BACKEND-API-DB-015`).
- Payment processing, subscriptions UI and Flutter state management.

Documentation impact: update one canonical mobile economy/API contract with season XP authority, allowed season statuses/reward window, premium denial/entitlement semantics and stable error codes. Record that current Flutter has no runtime reward-track caller; if a mobile adapter becomes necessary, update `docs/mobile_backend_contract_status.md` under `XREPO44-SEASON-ACTIVE-ENDPOINT-DISPOSITION-001` or promote one named implementation residual.

Acceptance criteria:
1. High lifetime XP with insufficient `UserSeasonProgress.EarnedXp` cannot unlock or claim a tier.
2. Preview and claim use the same season-scoped XP and return consistent unlock/claimability truth.
3. Explicit draft, future, inactive, completed/archived or reward-locked seasons follow one documented deny/claim-window policy; callers cannot bypass it by ID.
4. Premium claims require existing persisted server proof; without it, premium fails closed with zero inventory/claim writes.
5. Free track remains usable without premium state and arbitrary track strings are rejected.
6. Duplicate claims create one reward claim/inventory mutation and return deterministic already-claimed behavior.
7. Existing server-owned cosmetic provenance and catalog readiness checks remain intact.

Proof required:
- Service and raw HTTP tests for lifetime-vs-season XP mismatch.
- Explicit inactive/future/archived season selection tests for GET and POST.
- Premium no-entitlement, valid existing entitlement and cross-user cases, or fail-closed proof when no entitlement model exists.
- Duplicate same-tier claim with exact claim/inventory row counts.
- Preview/claim parity and zero-write rejection assertions.
- Contract diff and exact validation evidence.

Validation:
```powershell
python scripts/run_guarded.py --timeout-seconds 180 -- dotnet test tests/MathLearning.Tests/MathLearning.Tests.csproj -c Release --filter FullyQualifiedName~RewardTrack
python scripts/run_guarded.py --timeout-seconds 180 -- dotnet build src/MathLearning.Api/MathLearning.Api.csproj -c Release --no-restore
python scripts/check_documentation_health.py --context src/MathLearning.Api/Endpoints/AvatarEndpoints.cs
python scripts/validate_agent_evidence.py --changed-from <base-sha> --verify-git
```

Completion gate: No Done from UI hiding, request validation alone or lifetime-XP tests only. Done requires one server-side policy shared by preview and claim, premium fail-closed behavior, duplicate proof, contract sync and verified main delivery.

Stop conditions:
- Stop and create a bounded premium-storage follow-up if no persisted entitlement exists; keep runtime deny-by-default.
- Stop before redesigning all cosmetics entitlements or season earning.
- Stop at five product/test/doc paths plus one evidence file, a second falsified design or the timebox.

Evidence: .ai/runs/<yyyy-mm-dd>-BACKEND-SEASON-TRACK-AUTHORITY-001-evidence.md