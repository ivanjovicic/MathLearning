# Backend API and Database Residual Queue — 2026-07-11

Target repo: `ivanjovicic/MathLearning`  
Source audit: `../BACKEND_API_DB_RESIDUAL_AUDIT_2026_07_11.md`  
Reviewed head: `a72bbe2cf0ca9296c67b4a0877b609dde0e4fe9d`  
Scope: mobile-facing backend API, application/infrastructure services and EF/PostgreSQL behavior  
Excluded: `src/MathLearning.Admin/**` and Admin UI work

## Queue rules

Before implementing any prompt:

1. Read `AGENTS.md`, `docs/DOCS_INDEX.md`, `docs/AGENT_SHARED_OPERATING_STANDARD.md`, `docs/AGENT_RUN_LOG_ENFORCEMENT.md`, `docs/BACKEND_REGRESSION_GUARDRAILS.md`, `docs/BUGFIX_PATTERN_GUARDRAILS.md` and `docs/ai/learning/MISTAKE_LEDGER.md`.
2. Create `.ai/runs/<date>-<prompt-id>-evidence.md` before edits.
3. Re-read the central queue immediately before claiming the prompt to avoid ID/ownership collisions.
4. Preserve one canonical runtime owner when this queue links to an existing prompt.
5. Derive mutation authority from the authenticated user, never a body `userId`.
6. For retryable mutations, prove first request, duplicate same payload, same key/different payload, rollback and cross-user isolation.
7. Use PostgreSQL for provider-sensitive uniqueness, locking, transaction and query-plan proof.
8. Do not mark a contract-changing backend prompt complete without an explicit mobile contract update or documented cross-repo blocker.
9. Do not report a static audit, committed test or migration as validated without the exact successful command/CI evidence.

## Prompt index

| ID | Priority | Status | Purpose | Dependencies / links |
|---|---:|---|---|---|
| `BACKEND-API-DB-001` | P0 | Prompt-ready | Remove answer keys and full solution material from pre-answer online quiz/SRS responses. | Cross-repo mobile contract; preserve post-answer feedback |
| `BACKEND-API-DB-002` | P0 | Prompt-ready | Bind answer settlement to a valid user-owned issued quiz session and question set. | `BACKEND-TEST-013`, `BACKEND-TEST-032`, `BACKEND-TEST-033` |
| `BACKEND-API-DB-003` | P0/P1 | Prompt-ready | Replace client-authoritative progress completion with server-verifiable idempotent settlement. | Mobile offline/sync contract; `BACKEND-TEST-032/033` where needed |
| `BACKEND-API-DB-004` | P0/P1 | Prompt-ready | Scope sync operation identity and serialize same-device cursor mutation on PostgreSQL. | `BACKEND-TEST-032`; reuse transaction/failure barriers from `BACKEND-TEST-033` |
| `BACKEND-API-DB-005` | P1 | Prompt-ready | Correct offline bundle hint mapping and make bundle version reflect all response-affecting content. | Cross-repo offline contract |
| `BACKEND-API-DB-006` | P1 | Prompt-ready | Bound sync request/payload/storage/error data and add explicit retention ownership. | Follow `BACKEND-API-DB-004` schema decisions |
| `BACKEND-API-DB-007` | P1 | Prompt-ready | Protect refresh tokens at rest and bound expired/revoked-token retention. | Depends on or safely supersedes schema part of `BACKEND-TEST-012` |
| `BACKEND-API-DB-008` | P1/P2 | Prompt-ready / linked owner | Push user aggregates to SQL, define indexed search, and remove remaining writes from GET paths. | `BE-PERF-013` is canonical owner for pure-read changes |

---

# BACKEND-API-DB-001 — Remove pre-answer answer-key and solution disclosure

Priority: **P0**  
Run mode: contract/security fix with backend and mobile synchronization  
Risk: online learning endpoints currently disclose enough response data to answer questions without solving them.

## Goal

Make every online pre-answer quiz/SRS response structurally incapable of serializing the correct answer, complete explanation or full worked solution, while preserving authoritative server scoring and pedagogical feedback after settlement.

## Confirmed code boundary

Inspect at minimum:

- `src/MathLearning.Application/DTOs/Quiz/QuestionDto.cs`;
- `src/MathLearning.Application/DTOs/Quiz/NextQuestionResponse.cs`;
- `src/MathLearning.Api/Endpoints/QuizEndpoints.cs`;
- `src/MathLearning.Api/Endpoints/SrsEndpoints.cs`;
- `src/MathLearning.Api/Services/LegacyStepExplanationAdapter.cs`;
- quiz/SRS HTTP contract tests;
- `openapi.yaml` and `docs/API_ENDPOINT_INVENTORY.md`;
- mobile `docs/mobile_api_contract.md`, API models and quiz/SRS parsing tests.

Affected online pre-answer routes include:

- `POST /api/quiz/start`;
- `GET /api/quiz/questions`;
- `POST /api/quiz/questions`;
- `POST /api/quiz/next-question`;
- `GET /api/quiz/srs/daily`;
- `GET /api/quiz/srs/mixed`.

## Required implementation

1. Create a dedicated pre-answer DTO/mapper. Do not reuse an entity or DTO containing an answer key.
2. Remove from all listed pre-answer payloads:
   - `correctAnswerId` / `CorrectAnswerId`;
   - option correctness flags;
   - complete `explanation`;
   - worked `steps` that reveal the solution;
   - `hintFull` when it is equivalent to the solution.
3. Decide explicitly which progressive hints may remain before settlement. Keep only hints approved by the product contract and verify they do not contain the correct option/answer.
4. Preserve post-answer feedback in `POST /api/quiz/answer` after server validation. Define whether explanation appears after an incorrect answer only or after every settled answer; do not silently change the existing pedagogical rule.
5. Ensure OpenAPI/schema generation cannot reintroduce forbidden fields through another shared DTO.
6. Do not solve this by returning forbidden fields as `null`. They must be absent from serialized pre-answer JSON so future code cannot populate them accidentally.
7. If offline scoring truly requires an answer key, isolate it behind a separate explicitly documented offline contract and threat model. Do not expose it through the online endpoints above.
8. Update backend endpoint inventory and mobile contract/status docs in the same workstream.

## Required tests

Add serialization/HTTP tests that inspect raw JSON and prove:

- every affected route omits forbidden property names entirely;
- no option contains `isCorrect` or equivalent answer metadata;
- post-answer response still returns the documented feedback only after settlement;
- translations and formatting metadata remain intact;
- legacy aliases cannot bypass the safe DTO;
- anonymous requests remain 401 and another user's state cannot be accessed;
- OpenAPI does not advertise forbidden pre-answer fields.

Include a regression fixture with an explanation/hint containing the literal correct answer so the test proves the response is safe by structure, not by benign seed content.

## Validation

Run the narrow contract suite first, then build:

```text
dotnet test tests/MathLearning.Tests/MathLearning.Tests.csproj -c Release --filter "FullyQualifiedName~Quiz|FullyQualifiedName~Srs|FullyQualifiedName~QuestionContract|FullyQualifiedName~OpenApi"
dotnet build MathLearning.slnx -c Release
```

Record the exact test count and inspect at least one serialized response from each route family.

## Non-goals

- Do not change Admin authoring/preview DTOs.
- Do not weaken server answer validation or anti-cheat.
- Do not redesign offline synchronization inside this prompt unless required to prevent online leakage.

## Completion rule

Complete only when raw HTTP contract tests prove the fields are absent, backend/mobile contracts are synchronized, and the exact build/test evidence is recorded. A mapper-only review is insufficient.

---

# BACKEND-API-DB-002 — Enforce quiz-session and issued-question authority

Priority: **P0**  
Run mode: correctness, authorization and relational settlement  
Risk: malformed/missing session IDs are silently replaced and the server does not persist the question set issued to a session.

## Goal

Allow canonical quiz answer settlement only for a valid active session owned by the authenticated user and only for a question issued to that session, with deterministic replay/conflict/concurrency behavior.

## Read and inspect

- `src/MathLearning.Domain/Entities/QuizSession.cs`;
- `src/MathLearning.Domain/Entities/UserAnswer.cs` and answer-audit/attempt entities;
- quiz session/answer mappings in `ApiDbContext` and migrations;
- `QuizEndpoints.cs`, especially start/questions/answer/offline-submit paths;
- `QuizEndpointHelpers`, idempotency services and XP tracking;
- existing quiz-answer transaction/idempotency tests;
- `BACKEND-TEST-013`, `BACKEND-TEST-032` and `BACKEND-TEST-033`.

## Required design decision

Choose and document one authoritative representation:

- persisted `QuizSessionQuestion` membership with a unique `(SessionId, QuestionId)` key; or
- an equally strong server-verifiable signed session manifest that cannot be modified by the client.

Prefer persisted membership when session lifecycle, answer status and concurrency must be queried. Do not rely only on the fact that a question exists globally.

## Required implementation

1. Canonical `POST /api/quiz/answer` must reject malformed/missing session IDs with a stable 400 contract. Never generate a replacement session inside answer settlement.
2. Reject an unknown session, foreign-user session and expired/completed session with stable 404/403/409 semantics chosen deliberately and tested.
3. Persist/bind the exact issued question set when `/start` or the canonical question route creates the session.
4. Verify membership inside the same authoritative transaction as answer mutation/idempotency settlement.
5. Define one settled answer per session/question unless multiple attempts are an explicit product rule. Enforce the rule with the correct unique index/atomic transition.
6. Store/replay the settled response for duplicate same-payload operation identity.
7. Return conflict for the same operation/session-question key with a different answer payload.
8. Prevent two concurrent requests from both receiving XP/mastery/anti-cheat/analytics effects.
9. Keep analytics ingest durable through the existing `BACKEND-TEST-022` owner; do not create another post-commit handoff design.
10. Decide how legacy `/questions` and no-idempotency answer clients transition:
    - migrate to the canonical contract;
    - temporarily isolate as compatibility with no authoritative reward settlement; or
    - gate behind a time-limited feature flag.
11. Update schema migration, model snapshot, endpoint inventory and mobile contract/status.

## Required PostgreSQL tests

Using deterministic barriers rather than sleeps, prove:

- valid owner/session/question settles once;
- malformed and missing session IDs create no session and no domain writes;
- valid session with non-issued question is rejected;
- another user's session is rejected without revealing its contents;
- closed/expired session is rejected;
- duplicate same payload replays the original body/status;
- same keys with different payload conflict;
- two concurrent answers produce one authoritative mutation and one deterministic replay/conflict;
- failure after SQL but before commit rolls back membership/answer/XP/ledger changes;
- legacy compatibility cannot bypass the canonical authority rule.

## Validation

```text
dotnet test tests/MathLearning.Tests/MathLearning.Tests.csproj -c Release --filter "FullyQualifiedName~QuizSession|FullyQualifiedName~QuizAnswer|FullyQualifiedName~RelationalIdempotency|FullyQualifiedName~MutationUserScope"
dotnet build MathLearning.slnx -c Release
```

Provider-sensitive completion requires the PostgreSQL lane from `BACKEND-TEST-032`; SQLite/InMemory success alone is not sufficient.

## Non-goals

- Do not redesign adaptive/practice settlement owned by `BE-PERF-012/015`.
- Do not add a second idempotency storage pattern.
- Do not mix answer-key response hardening from `BACKEND-API-DB-001` unless both prompts are explicitly combined in one evidence log.

## Completion rule

Complete only with migration/model agreement, PostgreSQL concurrency proof, stable HTTP contracts and mobile synchronization.

---

# BACKEND-API-DB-003 — Replace client-authoritative progress completion

Priority: **P0/P1**  
Run mode: trust-boundary and idempotent settlement  
Risk: `/api/progress/sync` accepts client-declared completion/day, persists it as authoritative progress and invokes reward evaluation.

## Goal

Make daily progress completion derive from server-verifiable settled activity rather than a raw client boolean/date, while preserving legitimate offline synchronization.

## Inspect

- `src/MathLearning.Api/Endpoints/ProgressEndpoints.cs`;
- `UserDailyStat` entity/configuration/migrations;
- quiz, practice and offline sync settlement events;
- `ICosmeticRewardService.ProcessProgressRewardsAsync` and reward rules;
- mobile progress sync caller and offline queue model;
- relevant idempotency and sync contracts.

## Required contract decision

Document the authoritative completion rule, for example:

- completion is derived from one or more server-settled quiz/practice operations for the UTC/user-calendar day; or
- a registered device submits a signed offline operation containing stable operation identity and evidence that the server revalidates.

A body `{ completed: true, day: ... }` without verifiable source is not sufficient authority.

## Required implementation

1. Replace raw `JsonElement` binding with a typed, validated request.
2. Derive the authenticated user exclusively from claims.
3. Require stable operation identity and canonical payload hashing for retryable sync.
4. Reject future days and dates older than an explicitly configured offline window. Normalize timezone/day semantics once and document them.
5. Verify referenced quiz/practice/offline operations belong to the user/device and are settled.
6. Create/update `UserDailyStat` in the same transaction or through a durable idempotent projection event.
7. Make duplicate same evidence return the original result without repeating reward work.
8. Return conflict when the same operation identity carries different day/evidence.
9. Remove direct reward evaluation from a generic unproved sync request. Reward processing must consume authoritative profile/progress state through one idempotent owner.
10. Preserve monotonic semantics only where intended: a completed day may remain completed, but an attacker must not be able to manufacture it.
11. Update mobile offline behavior, API inventory and contract status.

## Required tests

Prove:

- raw client `completed=true` without evidence is rejected and writes nothing;
- future and out-of-window dates are rejected;
- valid settled evidence creates exactly one daily record;
- duplicate replay is stable and reward processing runs at most once;
- same key/different evidence conflicts;
- evidence from another user/device is rejected;
- cancellation/failure rolls back or leaves a recoverable durable event;
- concurrent submissions cannot create duplicate daily/reward records;
- legacy clients receive an explicit compatibility error/version response rather than silent acceptance.

## Validation

```text
dotnet test tests/MathLearning.Tests/MathLearning.Tests.csproj -c Release --filter "FullyQualifiedName~ProgressSync|FullyQualifiedName~UserDailyStat|FullyQualifiedName~CosmeticReward|FullyQualifiedName~MutationUserScope|FullyQualifiedName~Sync"
dotnet build MathLearning.slnx -c Release
```

Use PostgreSQL when proving unique/concurrent settlement.

## Non-goals

- Do not redesign all cosmetic reward rules.
- Do not duplicate durable analytics ingest work from `BACKEND-TEST-022`.

## Completion rule

Complete only when no unproved client boolean/date can create authoritative completion, offline behavior remains supported by a documented verifiable path, and duplicate/concurrent tests are executable.

---

# BACKEND-API-DB-004 — Scope and serialize offline sync settlement

Priority: **P0/P1**  
Run mode: PostgreSQL concurrency, idempotency and ownership  
Risk: cursor/idempotency state is read before the transaction; operation IDs are globally unique and duplicate lookup is not user/device scoped.

## Goal

Guarantee one ordered writer per registered device cursor and isolate operation identity by authenticated scope, with deterministic stored acknowledgements for retries.

## Inspect

- `SyncService.SyncAsync` and retry/dead-letter paths;
- `SyncDevice`, `DeviceSyncState`, `SyncEventLog`, `ServerSyncEvent` entities/configurations;
- sync migrations and model snapshot;
- `SyncEndpoints.cs`, signature validation and `SyncOptions`;
- `SyncServiceTests` and provider fixtures;
- `BACKEND-TEST-032/033` test infrastructure.

## Required design

1. Choose canonical sync identity consistent with agent rules, such as:

```text
userId + deviceId + operationId
```

or, when operation type is required:

```text
userId + deviceId + operationType + operationId
```

2. Store a canonical payload hash and settled acknowledgement/status code for replay/conflict decisions.
3. Make `(UserId, DeviceId, ClientSequence)` uniqueness match cursor authority. Do not rely on a globally unique UUID chosen by the client.

## Required implementation

1. Begin the relational transaction before reading the mutable device cursor/idempotency state used for settlement.
2. Serialize a device's cursor with PostgreSQL `SELECT ... FOR UPDATE`, an atomic compare-and-swap update, or another measured single-writer mechanism.
3. Scope existing-operation lookup by the canonical identity. A matching UUID from another user/device must not enter the duplicate branch or reveal status/error metadata.
4. Validate the entire operation envelope before persisting payload/error data.
5. For duplicate same payload, return the original settled acknowledgement.
6. For same scoped key with a different canonical payload, return a stable conflict without invoking operation effects.
7. Define sequence-gap behavior under concurrent batches; do not advance over an uncommitted/missing sequence.
8. Ensure all authoritative operation effects, sync log state and cursor advancement commit atomically or through a documented durable handoff.
9. Preserve dead-letter/redrive ownership and do not create a second outbox implementation.
10. Add/migrate indexes safely for clean and already-upgraded PostgreSQL databases; update snapshot/schema docs.

## Required PostgreSQL concurrency matrix

Use two independent DbContexts/connections and deterministic barriers to prove:

- two same-device requests starting from the same cursor settle each sequence at most once;
- overlapping batches cannot both process the same operation;
- duplicate same payload replays the stored ack;
- same scoped key/different payload conflicts;
- same operation UUID used by different users/devices remains isolated;
- sequence gaps do not advance cursor;
- process failure before commit leaves no advanced cursor/settled log;
- cancellation propagates through queries, operation processing and commit;
- unique/serialization retry returns a deterministic HTTP contract, not raw provider errors.

## Validation

```text
dotnet test tests/MathLearning.Tests/MathLearning.Tests.csproj -c Release --filter "FullyQualifiedName~SyncService|FullyQualifiedName~SyncEndpoint|FullyQualifiedName~PostgreSql|FullyQualifiedName~MutationUserScope"
dotnet build MathLearning.slnx -c Release
```

Record provider/version, SQL isolation/locking mechanism and exact test count.

## Non-goals

- Do not implement outbox claim/backoff work owned by `BACKEND-TEST-023`.
- Do not combine payload-size/retention hardening from `BACKEND-API-DB-006` unless schema ownership is explicitly coordinated.

## Completion rule

Complete only with PostgreSQL race evidence, scoped unique indexes, stored replay/conflict behavior and no cross-user duplicate metadata path.

---

# BACKEND-API-DB-005 — Correct offline bundle content and revision truth

Priority: **P1**  
Run mode: contract correctness and cache/version integrity  
Risk: full hint duplicates explanation and response-affecting content edits can keep the same manifest version.

## Goal

Return semantically correct localized offline content and make the bundle version change whenever any serialized learning content changes, while keeping generation/query cost bounded.

## Inspect

- `OfflineBundleService.cs`;
- sync bundle DTOs;
- question/option/translation entities and versioning fields;
- question publish/version pipeline;
- mobile bundle cache, manifest comparison and offline quiz reader;
- existing translation/content-format helpers and tests.

## Required implementation

1. Correct hint mapping through canonical helpers:
   - light, medium and full hints must map to their intended fields;
   - explanation must remain separate;
   - translated content must follow the user's resolved language/fallback policy.
2. Define one canonical content revision source. Preferred options:
   - a published-content revision/version incremented transactionally by authoring/publish changes; or
   - a deterministic persisted fingerprint computed at publish time.
3. The manifest version must change when any serialized field changes, including:
   - question text/type/difficulty;
   - option IDs/order/text;
   - hint/explanation/step content;
   - translations and semantics metadata;
   - content/render formats;
   - topic/subtopic membership or names;
   - publication/retirement status relevant to selection.
4. Explicitly decide whether user progress/profile changes belong in the content bundle version or a separate user-snapshot revision. Avoid invalidating all content for an unrelated profile timestamp when a two-part version is cleaner.
5. Keep ordering deterministic.
6. Avoid loading/hashing all large strings on every request if a persisted revision can provide the same truth.
7. Add ETag/`If-None-Match` only if mobile can consume it without creating a second inconsistent version policy.
8. Update backend/mobile contract docs and offline cache tests.

## Required tests

Prove:

- `HintFull` is not populated from explanation;
- every hint/explanation slot maps correctly in default and translated language;
- changing option text, order, hint, explanation, step, translation, format or taxonomy changes content revision/version;
- unchanged content produces a stable deterministic version;
- profile-only changes follow the documented separate/combined version policy;
- empty/no-question bundles are stable and bounded;
- query count and payload count remain within a recorded budget;
- mobile invalidates/reloads exactly when the version changes.

## Validation

```text
dotnet test tests/MathLearning.Tests/MathLearning.Tests.csproj -c Release --filter "FullyQualifiedName~OfflineBundle|FullyQualifiedName~Translation|FullyQualifiedName~ContentVersion|FullyQualifiedName~SyncBundle"
dotnet build MathLearning.slnx -c Release
```

If a new persisted revision is introduced, validate migration/model/snapshot and a clean PostgreSQL schema.

## Non-goals

- Do not expose correct-answer metadata through online quiz endpoints.
- Do not redesign the entire question authoring UI.

## Completion rule

Complete only with exact mapping tests, deterministic revision tests, measured query behavior and mobile cache-contract synchronization.

---

# BACKEND-API-DB-006 — Bound sync envelopes, persisted errors and retention

Priority: **P1**  
Run mode: API hardening and operational data lifecycle  
Risk: operation count is bounded, but payload bytes/strings/error details and retained event volume lack one explicit contract.

## Goal

Reject oversized or malformed sync input before database work, store only bounded safe diagnostic data, and keep sync/event/dead-letter tables within a documented retention budget.

## Inspect

- `SyncDtos.cs`, `SyncEndpoints.cs`, `SyncService.cs`, signature code and `SyncOptions`;
- Kestrel/request-size configuration and exception mapping;
- `SyncEventLog`, `ServerSyncEvent`, `SyncDeadLetter` mappings/indexes;
- redaction helpers and logging rules;
- current maintenance/background-job infrastructure.

## Required limits

Define named configuration with safe defaults and hard ceilings for:

- total HTTP body bytes;
- operations per batch;
- bytes per operation payload and total payload bytes;
- `DeviceId`, device name, platform, app version and operation type lengths;
- signature encoding and byte length;
- error code/public message/internal diagnostic length;
- server events returned per sync;
- retention days and delete batch size for each event/dead-letter state.

## Required implementation

1. Apply transport/body limits before JSON materialization where possible.
2. Bind typed requests and validate all envelope fields before querying or inserting.
3. Use an explicit operation-type allowlist/dispatcher; reject unknown types with a stable safe error.
4. Validate operation payload schema before persisting raw JSON.
5. Never persist `Exception.ToString()` as user-facing or long-lived dead-letter failure text. Store a bounded safe category/message plus correlation/trace ID; keep sensitive detail only in approved logs/telemetry.
6. Return 413 for body-size violations and stable 400/422 contracts for field/payload violations.
7. Add an indexed, cancellable, batched retention owner. Exclude unresolved/dead-letter/audit rows according to a documented policy; never delete recoverable work accidentally.
8. Coordinate new indexes/schema with `BACKEND-API-DB-004` so migrations do not conflict.
9. Add metrics for rejection reason, payload size buckets, retained row counts and purge duration without high-cardinality identifiers.

## Required tests

- body just below/at/above maximum;
- operation count and total/per-operation byte boundaries;
- overlong/invalid device/operation/signature fields;
- malformed/unknown payload creates zero rows;
- public response contains no raw exception/SQL/stack text;
- stored failure fields are bounded/redacted;
- retention deletes only eligible rows in bounded batches;
- cancellation stops purge safely;
- indexes support the exact retention predicate/order on PostgreSQL;
- large rejected input does not invoke operation handlers.

## Validation

```text
dotnet test tests/MathLearning.Tests/MathLearning.Tests.csproj -c Release --filter "FullyQualifiedName~SyncInput|FullyQualifiedName~SyncEndpoint|FullyQualifiedName~SyncRetention|FullyQualifiedName~SafeClientError|FullyQualifiedName~Redact"
dotnet build MathLearning.slnx -c Release
```

Also capture PostgreSQL `EXPLAIN (ANALYZE, BUFFERS)` for the retention selection/delete pattern on representative data.

## Non-goals

- Do not change sync cursor/idempotency semantics owned by `BACKEND-API-DB-004`.
- Do not expose full payloads or identifiers in metrics.

## Completion rule

Complete only with executable boundary tests, zero-write rejection proof, safe stored/public errors and a measured retention query/index plan.

---

# BACKEND-API-DB-007 — Protect refresh tokens at rest and bound lifecycle

Priority: **P1**  
Run mode: authentication storage migration and rotation regression  
Dependency: `BACKEND-TEST-012` must be completed first or this prompt must deliberately supersede its schema patch and satisfy all of its tests.

## Goal

Ensure the database never stores a reusable refresh-token bearer secret, preserve single-use rotation/reuse detection, and purge expired/revoked records safely.

## Inspect

- `AuthEndpoints.cs`;
- `RefreshTokenService` and refresh-token DTOs;
- `RefreshToken` entity, EF mapping, migrations and snapshot;
- refresh concurrency/relational tests;
- authentication logs and device/IP metadata policy;
- mobile token refresh/logout behavior.

## Required design

Use a reviewed representation such as:

- random public selector + cryptographic verifier, storing selector and verifier digest; or
- token ID plus HMAC/SHA-256 digest of a high-entropy token.

The issued token may be shown to the client once. Database lookup and validation must use only the non-secret selector and/or digest. Do not use reversible encryption merely to keep the current lookup shape.

## Required implementation

1. Align generator, DTO, EF model, snapshot and PostgreSQL schema lengths as required by `BACKEND-TEST-012`.
2. Generate at least the existing entropy level using a CSPRNG.
3. Persist only selector/digest metadata, never the issued bearer value.
4. Use constant-time digest comparison where a verifier comparison occurs in application code.
5. Rotate transactionally: revoke/consume old token and insert new token atomically.
6. Preserve deterministic concurrent reuse behavior: one request succeeds; all other uses of the old token fail safely.
7. Define migration behavior for existing plaintext rows:
   - one-time digest backfill when safely possible; or
   - explicit forced reauthentication/session invalidation.
   Never leave mixed ambiguous lookup semantics indefinitely.
8. Add bounded cleanup for expired/revoked tokens with an index matching the predicate/order. Keep only the audit metadata required by policy.
9. Review username, token, device and IP logging. Redact or hash sensitive/high-cardinality fields according to existing logging rules.
10. Update auth contract/docs without changing the mobile token string shape unless necessary.

## Required tests

Prove:

- stored DB columns never equal the issued refresh token;
- valid refresh succeeds and rotates once;
- old token reuse returns the canonical 401 contract;
- two concurrent refreshes produce exactly one success;
- logout/revoke-all work with the new representation;
- malformed/unknown tokens do not create timing-visible/raw errors;
- expired/revoked cleanup deletes eligible rows only and is idempotent;
- migration handles existing rows according to the chosen policy;
- model, snapshot, migration and PostgreSQL schema lengths agree;
- logs never contain the raw token.

## Validation

```text
dotnet test tests/MathLearning.Tests/MathLearning.Tests.csproj -c Release --filter "FullyQualifiedName~RefreshToken|FullyQualifiedName~AuthRefresh|FullyQualifiedName~AuthLogout|FullyQualifiedName~AuthRevoke"
dotnet build MathLearning.slnx -c Release
```

Run PostgreSQL relational concurrency tests; InMemory-only validation is not sufficient.

## Non-goals

- Do not replace access-token/JWT architecture.
- Do not weaken reuse detection to simplify migration.

## Completion rule

Complete only when at-rest DB inspection proves no reusable token secret, rotation concurrency is green on a relational provider, lifecycle cleanup is measured, and `BACKEND-TEST-012` is satisfied or explicitly superseded.

---

# BACKEND-API-DB-008 — Finish user-read query discipline

Priority: **P1/P2**  
Run mode: measured query optimization and linked pure-read cleanup  
Canonical ownership: `BE-PERF-013` remains the runtime owner for removing writes from read endpoints. This prompt must link/extend that work, not create a competing implementation.

## Goal

Keep user-facing reads bounded and SQL-driven, provide a deliberate indexed PostgreSQL search plan, and make remaining GET endpoints side-effect free.

## Inspect

- `UserEndpoints.cs`;
- `ProgressEndpoints.cs` as the existing SQL aggregate reference;
- `SrsEndpoints.cs` streak read;
- `UserProfile`, `UserQuestionStat`, `UserSettings` mappings/indexes;
- existing compiled queries/performance middleware;
- `BE-PERF-013` owner/evidence and `BACKEND_REQUEST_PERFORMANCE_BUDGETS.md`.

## Required implementation

### A. `/api/users/stats`

1. Replace `ToListAsync` plus in-memory sums with one server-side aggregate projection for attempts/correct/count.
2. Use `AsNoTracking` for read-only profile data or project only required fields.
3. Count hints in SQL and avoid loading entities.
4. Preserve exact empty-user/accuracy rounding response semantics.
5. Record query count and rows read.

### B. `/api/users/search`

1. Normalize/trim the query and preserve minimum/maximum result bounds.
2. Choose one PostgreSQL strategy based on expected UX:
   - normalized prefix search with a compatible index; or
   - `pg_trgm`/GIN or GiST for deliberate substring search.
3. Do not add an index that PostgreSQL cannot use for the actual `Contains`/case-insensitive predicate.
4. Project the public identity allowlist only and batch appearance lookup.
5. Add deterministic ordering/tie-breaker and a future cursor boundary if the result limit grows.
6. Capture plans for selective and non-selective terms on representative data.

### C. Remaining read-side writes

Coordinate with `BE-PERF-013` to cover:

- `GET /users/{userId}/settings` creating/saving defaults;
- `GET /api/quiz/srs/streak` rolling/saving streak state;
- any login/read path that performs the same streak settlement as a convenience side effect.

Return computed defaults without inserting on GET, and move streak settlement to one explicit/durable authoritative activity owner. Add zero-write GET contract tests.

## Required tests and measurements

- stats response parity for zero, one and large-history users;
- SQL command count and no entity-list materialization for stats;
- search auth/privacy allowlist and bounds;
- PostgreSQL plans use the intended search index for supported predicates;
- worst allowed search cannot return unbounded rows;
- settings GET on missing row performs zero inserts/updates and returns documented defaults;
- SRS streak GET performs zero writes/reward effects;
- concurrent GET polling remains read-only;
- mutation endpoint/job still performs the intended streak/default initialization exactly once.

## Validation

```text
dotnet test tests/MathLearning.Tests/MathLearning.Tests.csproj -c Release --filter "FullyQualifiedName~UserStats|FullyQualifiedName~UserSearch|FullyQualifiedName~UserSettings|FullyQualifiedName~SrsStreak|FullyQualifiedName~ReadOnly"
dotnet build MathLearning.slnx -c Release
```

Capture SQL/query-count evidence plus PostgreSQL `EXPLAIN (ANALYZE, BUFFERS)` for search. Compare p95/query budgets with `BACKEND_REQUEST_PERFORMANCE_BUDGETS.md`.

## Non-goals

- Do not reimplement school leaderboard/progress read cleanup already owned by `BE-PERF-013`.
- Do not expose additional public profile fields for search convenience.
- Do not introduce a search service dependency without a measured need.

## Completion rule

Complete only with response parity tests, zero-write GET proof, SQL/query-count evidence and a PostgreSQL plan showing that the chosen search index matches the actual predicate.

---

## Recommended execution sequence

1. Run/close the latest validation/workflow/evidence prompts so current baseline status is honest.
2. `BACKEND-API-DB-001`.
3. `BACKEND-API-DB-002`.
4. `BACKEND-API-DB-003`.
5. `BACKEND-TEST-032` if the reusable PostgreSQL lane is not yet available.
6. `BACKEND-API-DB-004`.
7. `BACKEND-API-DB-005`.
8. `BACKEND-API-DB-006`.
9. `BACKEND-TEST-012` plus `BACKEND-API-DB-007`.
10. `BACKEND-API-DB-008` under the canonical `BE-PERF-013` owner.

## Queue completion rule

A row may move from `Prompt-ready` only with a referenced `.ai/runs` evidence file. Contract-sensitive rows require explicit backend/mobile synchronization status. Provider-sensitive rows require PostgreSQL evidence. Static review, generated migrations, committed tests and green unrelated suites are not fix proof.
