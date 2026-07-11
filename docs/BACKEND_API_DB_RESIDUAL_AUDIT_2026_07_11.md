# Backend API and Database Residual Audit — 2026-07-11

> Static connector-based audit only. This document records code-level findings and routes them into implementation prompts. It does **not** prove production impact and it does **not** claim that runtime fixes have landed.

Repo: `ivanjovicic/MathLearning`  
Reviewed head: `a72bbe2cf0ca9296c67b4a0877b609dde0e4fe9d`  
Scope: backend API, application/infrastructure services, EF Core mappings and mobile-facing contracts  
Excluded: `src/MathLearning.Admin/**` and Admin UI behavior  
Detailed queue: `docs/prompt_queues/backend_api_db_residuals_2026_07_11.md`

## Method and duplicate-avoidance

The review inspected current code first, then compared each finding with the active backend test, performance, critical-risk and latest-commit queues. The following existing owners were deliberately not duplicated:

- `BACKEND-TEST-012` — refresh-token length/model/snapshot drift;
- `BACKEND-TEST-013` — missing operation-identity policy;
- `BACKEND-TEST-022` — durable analytics ingest handoff;
- `BACKEND-TEST-023` / `BE-PERF-016` — outbox claim, lease, backoff and dead-letter correctness;
- `BACKEND-TEST-031` / `BE-PERF-009` — weakness scheduler durability and boundedness;
- `BACKEND-TEST-032` — PostgreSQL provider-sensitive test lane;
- `BACKEND-TEST-033` — cancellation and rollback proof;
- `BACKEND-TEST-034` / `BE-PERF-008` — legacy route parity and deprecation;
- `BE-PERF-012` — adaptive-answer exactly-once settlement;
- `BE-PERF-013` — writes performed by read paths;
- `BE-PERF-015` — practice-session answer/completion concurrency.

New prompts either own a genuinely uncovered boundary or explicitly link to an existing canonical owner.

## Executive priority

| Priority | Finding | Primary risk | Prompt |
|---|---|---|---|
| P0 | Online quiz and SRS payloads disclose `CorrectAnswerId`, full hints and explanations before settlement | trivial answer extraction, invalid learning/leaderboard trust, contract-level cheating | `BACKEND-API-DB-001` |
| P0 | Quiz answers are not strongly bound to a valid issued user-owned session and issued question set | arbitrary-session/question settlement, hidden malformed-client behavior, replay ambiguity | `BACKEND-API-DB-002` |
| P0/P1 | `/api/progress/sync` trusts client-declared completion/day and invokes reward evaluation | forged authoritative progress rows and settlement coupled to unproved client state | `BACKEND-API-DB-003` |
| P0/P1 | Offline sync reads cursor/idempotency state before its transaction and uses globally unique operation IDs | same-device race, cross-user key collision, ambiguous duplicate/conflict behavior | `BACKEND-API-DB-004` |
| P1 | Offline bundle maps `HintFull` from `Explanation` and content edits do not necessarily change bundle version | incorrect hints and stale offline learning content | `BACKEND-API-DB-005` |
| P1 | Sync accepts bounded operation count but unbounded operation payload/strings and persists raw failure details | DB/storage amplification, oversized JSON, sensitive error retention | `BACKEND-API-DB-006` |
| P1 | Refresh tokens are looked up and stored as bearer-token plaintext with no explicit bounded lifecycle | database disclosure becomes session compromise; expired/revoked-row growth | `BACKEND-API-DB-007` |
| P1/P2 | User statistics load all rows into memory; user search lacks a deliberate PostgreSQL search plan; remaining GETs write defaults/streak state | avoidable DB/memory cost, full scans, write amplification on reads | `BACKEND-API-DB-008` |

---

## Finding 1 — Pre-answer API contracts disclose answer keys and solution material

### Code evidence

`QuestionDto` contains `CorrectAnswerId`, `HintFull`, `Explanation` and `Steps`. Both quiz and SRS mappers populate those fields before an answer is submitted. The legacy mobile questions response also emits `correctAnswerId`, `hintFull` and `explanation`.

Affected online surfaces include:

- `POST /api/quiz/start`;
- `GET /api/quiz/questions`;
- `POST /api/quiz/questions`;
- `GET /api/quiz/srs/daily`;
- `GET /api/quiz/srs/mixed`.

The answer endpoint already has a post-settlement feedback path that can return explanation/steps after server validation. Therefore the online pre-answer disclosure is not required for ordinary authoritative scoring.

### Risk

A client can inspect JSON and obtain the correct option without solving the question. This undermines XP, streak, mastery, anti-cheat and leaderboard trust even when the server correctly validates the submitted option.

### Required action

Create a safe pre-answer DTO that cannot serialize the answer key or complete solution. Preserve post-answer pedagogical feedback in the settled response. If a separate offline mode truly requires an answer key, give it an explicit threat model and separate contract rather than reusing online quiz DTOs.

---

## Finding 2 — Quiz session identity is weak authority

### Code evidence

`QuizSession` currently stores only:

- `Id`;
- `UserId`;
- `StartedAt`.

It does not persist the questions issued to the session or a session state/revision. The answer endpoint accepts `quizId` or `sessionId`; when parsing fails, it silently generates a new GUID. It then ensures a session exists and processes the supplied `questionId`.

### Risk

The server cannot prove that the submitted question belonged to the issued session. A malformed/missing session ID becomes a new session rather than a client error. Repeated or arbitrary question submissions can therefore be processed outside a strong issued-session boundary unless lower-level first-correct constraints happen to limit one specific effect.

### Required action

Require a valid, active, authenticated-user-owned canonical session and persist or cryptographically bind the issued question set. Reject malformed/unknown/foreign/closed sessions. Define exact first, duplicate, conflict and concurrent-answer behavior.

---

## Finding 3 — Generic progress sync accepts client authority

### Code evidence

`POST /api/progress/sync` accepts raw JSON, reads client-supplied `completed` and `day`, creates or updates `UserDailyStat`, saves it, and then invokes `ProcessProgressRewardsAsync`.

The endpoint does not visibly require a server-settled quiz/practice operation, stable operation identity, or a bounded historical/future-date policy before creating the authoritative daily row.

### Risk

A modified client can declare completion for an arbitrary parseable day. Even when current cosmetic rules happen to evaluate profile XP/level/streak rather than the daily row, the endpoint creates authoritative progress state from unproved client input and couples generic synchronization to reward evaluation.

### Required action

Define the authoritative source for daily completion. Prefer derivation from settled server operations or a registered-device signed offline operation with stable identity. Bound accepted dates, reject future/implausible history, and make duplicate/conflict behavior explicit.

---

## Finding 4 — Sync operation scope and same-device serialization are incomplete

### Code evidence

`SyncService.SyncAsync`:

1. loads `SyncDevice` and `DeviceSyncState`;
2. loads existing logs by `OperationId`;
3. calculates the expected client sequence;
4. only then starts a relational transaction;
5. processes the batch and advances the cursor.

`SyncEventLog` has a globally unique `OperationId` index and a unique `(DeviceId, ClientSequence)` index. Existing-log lookup is by operation ID alone.

### Risk

Two concurrent requests from the same device can observe the same cursor before either transaction owns it. A globally unique operation ID can also turn another user's matching ID into a duplicate path instead of an isolated operation/conflict. Unique constraints may eventually reject one writer, but they do not by themselves define safe replay after downstream effects.

### Required action

Align operation identity with the documented authenticated scope, serialize cursor advancement inside the transaction with PostgreSQL locking/CAS semantics, store a canonical payload hash and settled acknowledgement, and prove concurrency on PostgreSQL.

---

## Finding 5 — Offline bundle mapping and version truth are incorrect

### Code evidence

`OfflineBundleService` maps:

- light hint from `HintClue`;
- medium hint from `HintFormula`;
- **full hint from `Explanation`**;
- explanation from `Explanation`.

The manifest version hashes profile update ticks plus topic/subtopic names and question ID/difficulty/type. It does not include question text, options, hint content, explanation, translations, content formats or a dedicated published-content revision.

### Risk

Offline clients can receive an explanation in the full-hint slot. Content edits can retain the same bundle version, causing clients or caches to treat changed learning material as unchanged.

### Required action

Correct the mapping through the canonical translation/content helpers and make the manifest version represent every response-affecting content revision without creating an expensive full-string hashing hot path on every request.

---

## Finding 6 — Sync envelope/storage bounds are incomplete

### Code evidence

The sync endpoint bounds operation count, but DTO strings, operation payload JSON and signature are not visibly bounded at the API contract. Raw payload JSON is stored in `SyncEventLog`; dead-letter creation stores `ex.ToString()` as the failure reason.

### Risk

A valid authenticated device can submit large payloads or strings, increase JSONB/storage/log volume, and retain sensitive internal exception detail. Long-lived event/dead-letter tables also need an explicit retention owner.

### Required action

Add transport and per-operation byte limits, field normalization/allowlists, schema validation before persistence, bounded public/internal error storage, and an indexed batched retention policy.

---

## Finding 7 — Refresh-token bearer secrets are persisted directly

### Code evidence

Refresh, logout and registration/login flows persist and query the raw refresh token value. The EF mapping places a unique index directly on `RefreshToken.Token`. The active queue already tracks generator/model/schema length drift but not at-rest bearer-secret protection or bounded cleanup.

### Risk

Read access to the refresh-token table yields reusable bearer credentials until expiry/revocation. Rows are added on login/rotation, while no explicit purge/retention owner was found in this review.

### Required action

After or together with `BACKEND-TEST-012`, store only a digest or selector/verifier representation, define safe migration/session invalidation, and purge expired/revoked tokens in bounded batches. Preserve single-use rotation and concurrency behavior.

---

## Finding 8 — Remaining user read paths do unnecessary work

### Code evidence

`GET /api/users/stats` loads all current-user `UserQuestionStats` rows and performs sums in memory, while `/api/progress/overview` already demonstrates a SQL aggregate projection. User search uses substring `Contains` over username/display name without a documented PostgreSQL prefix/trigram plan. `GET /users/{userId}/settings` creates/saves defaults when absent, and `GET /api/quiz/srs/streak` can roll and save streak state.

### Risk

Per-user history growth increases memory and transfer cost; substring search can degrade into scans; GET polling causes writes and concurrency noise.

### Required action

Push aggregates to SQL, define a bounded indexed search strategy with measured plans, and extend the canonical `BE-PERF-013` pure-read owner to the remaining settings/SRS paths instead of creating a second competing implementation.

---

## Recommended execution order

1. `BACKEND-API-DB-001` — stop answer/solution disclosure.
2. `BACKEND-API-DB-002` — establish quiz-session/question authority.
3. `BACKEND-API-DB-003` — close client-authoritative progress sync.
4. `BACKEND-TEST-032` prerequisite where PostgreSQL proof is required.
5. `BACKEND-API-DB-004` — serialize and scope offline sync.
6. `BACKEND-API-DB-005` — correct offline bundle and revision truth.
7. `BACKEND-API-DB-006` — bound sync envelopes/storage/retention.
8. `BACKEND-TEST-012` plus `BACKEND-API-DB-007` — align then protect refresh-token persistence.
9. `BACKEND-API-DB-008`, linked to `BE-PERF-013` — finish read/query discipline.

## Validation status

- Static GitHub connector review completed against the recorded head.
- No runtime code was changed.
- No `dotnet build`, `dotnet test`, PostgreSQL plan, load test or production telemetry was executed in this audit.
- Every implementation prompt requires executable evidence before any finding can be marked fixed or validated.
