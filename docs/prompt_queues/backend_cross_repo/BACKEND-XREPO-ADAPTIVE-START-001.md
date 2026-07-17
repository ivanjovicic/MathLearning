# BACKEND-XREPO-ADAPTIVE-START-001 — Idempotent adaptive session start across timeout and restart

Prompt contract: v2
Prompt admission: v3
Repository: ivanjovicic/MathLearning
Prompt ID: BACKEND-XREPO-ADAPTIVE-START-001
Queue: docs/prompt_queues/backend_cross_repo_current_main_2026_07_17.md
Run lane: known-fix
Token budget: medium
Timebox: 30 minutes

Problem evidence:
- Current `POST /api/adaptive/session/start` accepts no operation identity and calls `GeneratePracticeSessionAsync`, which always creates a new random session and persists it.
- `AdaptiveApiFacade.StartAdaptiveSessionAsync` wraps this mutation in generic retry even though no settled replay contract exists.
- Flutter `CRIT16-FLOW-006` requires a verified same-key timeout/restart contract before mobile can safely recover an uncertain start.

Deduplication check:
- `BE-PERF-012` owns adaptive answer settlement only; it must not absorb session creation.
- `BE-PERF-015` owns the separate practice-session answer/completion service.
- Current backend queues/evidence and Flutter `CRIT16-FLOW-006`/`CRIT26-ADAPTIVE-SESSION-001` show no backend owner for adaptive start operation identity.

Priority rationale: P0 correctness because a response timeout after commit can create multiple active sessions and make mobile restart recovery choose the wrong authoritative session.

Dependencies/collisions:
- Reuse `IIdempotencyLedgerService`; do not introduce a second ledger or operation table.
- Do not edit adaptive answer settlement owned by `BE-PERF-012` or mobile latest-wins state owned by Flutter `CRIT26-ADAPTIVE-SESSION-001`.
- Coordinate request/response contract changes with current Flutter `CRIT16-FLOW-006`; making keys mandatory requires synchronized compatibility handling.

Owner boundary:
- Backend adaptive start coordinator owns operation identity, transaction, replay/conflict and returned session snapshot.
- Authenticated bearer identity remains authoritative; Flutter owns generation/storage/retry of the stable client operation ID.
- Question selection algorithms, adaptive answer mutation and reward projection are excluded.

Queue placement: first P0 row in the 2026-07-17 cross-repo queue because it is an uncovered prerequisite for safe mobile timeout/restart recovery.

Task: Make adaptive session start settle once for one authenticated user and one stable logical operation, and return the same `AdaptiveSessionDto` after duplicate delivery, response loss or process restart.

Source of truth:
- `src/MathLearning.Api/Endpoints/AdaptiveEndpoints.cs`
- `src/MathLearning.Api/Services/AdaptiveApiFacade.cs`
- `src/MathLearning.Api/Services/AdaptiveLearningService.cs`
- `src/MathLearning.Application/Services/IIdempotencyLedgerService.cs`
- `src/MathLearning.Infrastructure/Services/IdempotencyLedgerService.cs`
- `tests/MathLearning.Tests/Endpoints/AdaptiveApiFacadeIntegrationTests.cs`
- `docs/mobile_api_contract.md` and `docs/mobile_contract_idempotency_handoff.md`
- Flutter baseline `0d01e940...`, prompts `CRIT16-FLOW-006` and `CRIT26-ADAPTIVE-SESSION-001`

Interpretation before work: Build the matrix `operation ID/key -> normalized start payload -> first commit -> exact replay -> changed-payload conflict -> concurrent duplicate -> timeout/restart -> legacy no-key policy` before editing.

Ambiguity rule: Do not add a key that the backend ignores, do not retry start with a fresh key after possible acceptance and do not make the key mandatory until the compatibility decision is synchronized with Flutter.

Risk/ownership model:
- Operation scope is authenticated `userId + adaptive_session_start + operationId/idempotencyKey`.
- Use the existing ledger payload hash and completed result JSON as the replay source.
- Ledger begin, session/items creation and ledger completion must share one `ApiDbContext` transaction so no committed session exists without a replayable completed result.
- A same key with changed normalized payload is conflict; a different user never replays another user's session.
- Remove generic mutation retry until the transaction/replay path is proven; any later provider-transient retry preserves the same identity.

Failure-mode matrix:
- Session is committed but HTTP response is lost; same-key retry after restart returns the identical session and creates no rows.
- Two concurrent same-key starts race and only one session/items graph is committed.
- Same operation/key is reused with a different topic/target payload and returns stable conflict.
- Cancellation before commit rolls back ledger and session; cancellation after commit remains replayable.
- A legacy client omits operation identity and must follow the explicit non-retryable compatibility policy.
- User B submits user A's key or operation ID and receives no cross-user replay.

Execution packet:
- Initial reads: the seven backend files/doc owners above plus nearest ledger transaction tests; maximum 12 files.
- Search budget: maximum 3 searches for start DTO/header parsing, ledger transaction examples and adaptive start tests.
- First hypothesis/falsifier: generic retry plus random session creation duplicates start; falsify with same-key timeout/restart and concurrent PostgreSQL proof.
- Expected changed files: endpoint/request contract, one adaptive coordinator/service seam, focused tests and contract docs; maximum 6 paths plus evidence.
- Focused proof: first/replay/conflict/cancellation/cross-user tests; PostgreSQL race proof when transaction/uniqueness behavior is exercised.
- Stop trigger: split migration/provider work, answer mutation, Flutter implementation or a second runtime owner into their existing/new owner.

Owned paths:
- Adaptive start request/header parsing and compatibility policy.
- Start orchestration using the existing idempotency ledger and transaction.
- Adaptive start idempotency/concurrency tests.
- Backend mobile/idempotency contract synchronization.

Avoid paths:
- Adaptive answer implementation (`BE-PERF-012`).
- Practice-session completion (`BE-PERF-015`).
- Flutter provider/session revision code.
- New generic retry framework or second idempotency table.
- Adaptive question-selection redesign.

Documentation impact: update `docs/mobile_api_contract.md`, `docs/mobile_contract_idempotency_handoff.md` and `docs/API_ENDPOINT_INVENTORY.md`; record the exact Flutter prompt/handoff decision.

Acceptance criteria:
1. First keyed start creates one session/items graph and one completed replay record in one transaction.
2. Exact duplicate, including after simulated response loss/restart, returns the identical session IDs/items and performs zero extra mutation.
3. Changed-payload reuse returns stable conflict; cross-user reuse is isolated.
4. Concurrent same-key starts settle once under PostgreSQL or equivalent provider proof.
5. Cancellation/exception cannot leave a committed session with an unusable pending ledger.
6. Legacy no-key behavior is explicit, non-retryable and synchronized rather than silently claimed idempotent.

Proof required:
- Focused endpoint/integration fixtures for first, replay, conflict, cross-user and legacy compatibility.
- Transaction failure injection before commit and response-loss replay after commit.
- Concurrent provider-backed duplicate test with row/session/item counts.
- Exact response JSON/session identity comparison between first and replay.
- Contract diff showing the stable operation identity consumed by Flutter `CRIT16-FLOW-006`.

Validation:
```powershell
python scripts/run_guarded.py --timeout-seconds 180 -- dotnet test tests/MathLearning.Tests/MathLearning.Tests.csproj --filter FullyQualifiedName~AdaptiveSessionStartIdempotency
python scripts/run_guarded.py --timeout-seconds 180 -- dotnet build src/MathLearning.Api/MathLearning.Api.csproj -c Release --no-restore
python scripts/check_documentation_health.py --context src/MathLearning.Api/Endpoints/AdaptiveEndpoints.cs
python scripts/validate_agent_evidence.py --changed-from <base-sha> --verify-git
```

Completion gate: No Done from a client-generated unused key, an in-memory lock or generic retry. Done requires database/transaction-enforced single settlement, deterministic replay/conflict, compatibility sync, focused proof and verified main delivery.

Stop conditions:
- Stop if the existing ledger cannot share the adaptive session transaction; create a narrow ledger-coordinator follow-up instead of adding a new table.
- Stop before editing adaptive answer or mobile code.
- Stop at six changed paths, a second falsified design or the 30-minute limit.

Evidence: .ai/runs/<yyyy-mm-dd>-BACKEND-XREPO-ADAPTIVE-START-001-evidence.md
