# Backend API and Database Residual Queue — Pass 2 — 2026-07-11

Target repo: `ivanjovicic/MathLearning`  
Source audit: `../BACKEND_API_DB_RESIDUAL_AUDIT_2026_07_11_PASS2.md`  
Reviewed head: `6cdff4c7fbeb595ed29fc11b4641d7b9fe488100`  
Scope: remaining mobile-facing backend API, economy/cosmetics, leaderboard, identity/profile/avatar and EF/PostgreSQL mutation behavior  
Excluded: `src/MathLearning.Admin/**`, Admin UI and work already owned by another active prompt

## Queue rules

Before implementing any prompt:

1. Read `AGENTS.md`, `docs/DOCS_INDEX.md`, `docs/AGENT_SHARED_OPERATING_STANDARD.md`, `docs/AGENT_RUN_LOG_ENFORCEMENT.md`, `docs/BACKEND_REGRESSION_GUARDRAILS.md`, `docs/BUGFIX_PATTERN_GUARDRAILS.md` and `docs/ai/learning/MISTAKE_LEDGER.md`.
2. Create `.ai/runs/<date>-<prompt-id>-evidence.md` before runtime edits.
3. Re-read the central queue and current `main` immediately before claiming an ID.
4. Preserve one canonical runtime owner. Linked prompts must share tests/evidence rather than create parallel implementations.
5. Never treat idempotency as authorization or entitlement. A stable key can prevent duplicate execution but cannot prove that a user earned a reward.
6. Mobile mutations derive actor identity from authenticated claims. Client-provided user, source, price, reward, quantity or rank data is never authority.
7. Use PostgreSQL for uniqueness, row locking, transaction, lease and keyset-pagination proof.
8. Contract changes must update backend endpoint inventory plus the Flutter contract/status or record an explicit cross-repo blocker.
9. No row moves to Done from static review, a generated migration, committed tests or an unrelated green suite. Record exact executable evidence.

## Prompt index

| ID | Priority | Status | Purpose | Dependencies / canonical links |
|---|---:|---|---|---|
| `BACKEND-API-DB-009` | P0 | Runtime-fixed / Needs schema validation | Replace client-declared cosmetic item/fragment grants with server-authoritative entitlements. | Run log `.ai/runs/2026-07-14-BACKEND-API-DB-009-evidence.md`; schema validator still blocked on local PostgreSQL |
| `BACKEND-API-DB-010` | P0 | Runtime-fixed | Remove authoritative behavior from legacy coin/hint/power-up bypass routes. | Run log `.ai/runs/2026-07-14-BACKEND-API-DB-010-evidence.md`; follow-up is consumer cleanup and final route retirement |
| `BACKEND-API-DB-011` | P0 | Runtime-fixed | Make student leaderboard cursor/ranking compatible with string Identity user IDs. | Run log `.ai/runs/2026-07-14-BACKEND-API-DB-011-evidence.md`; PostgreSQL `EXPLAIN` proof still pending before parity work in `BACKEND-API-DB-012` |
| `BACKEND-API-DB-012` | P1 | Prompt-ready | Make Redis and DB leaderboard implementations contract-equivalent for scope, cursor, rank and failover. | Depends on `BACKEND-API-DB-011`; link `BE-PERF-004/005/008` |
| `BACKEND-API-DB-013` | P1 | Prompt-ready | Unify registration ownership and repair/prevent incomplete Identity-only accounts. | Link registration atomicity tests and mobile contract sync |
| `BACKEND-API-DB-014` | P1 | Prompt-ready | Retire or correctly rebuild the legacy photo-avatar contract and durable storage path. | Preserve validated file-safety work in `BACKEND-TEST-008` |
| `BACKEND-API-DB-015` | P0/P1 | Prompt-ready | Prevent permanent pending economy/cosmetics idempotency tombstones and prove safe recovery. | Canonical extension of `BACKEND-TEST-014`, `BACKEND-TEST-032/033` |

---

# BACKEND-API-DB-009 — Enforce server-authoritative cosmetic entitlement

Priority: **P0**  
Run mode: reward/inventory trust-boundary repair with PostgreSQL transaction proof  
Risk: authenticated clients can currently request ownership of arbitrary active items or arbitrary positive fragment copies for non-Daily-Run sources.

## Goal

Ensure that every cosmetic item or fragment received by a user is derived from a server-created, single-use entitlement or from a canonical server-owned purchase. Client input may identify an entitlement to consume, but must never define the granted item, fragment, copy count, source, price or eligibility.

## Inspect first

- `src/MathLearning.Api/Endpoints/CosmeticsEndpoints.cs`;
- `src/MathLearning.Api/Endpoints/CosmeticsEndpointHelpers.cs`;
- `src/MathLearning.Api/Endpoints/DailyRunCosmeticsSettlement.cs`;
- `src/MathLearning.Api/Endpoints/EconomySettlementEndpoints.cs`;
- `src/MathLearning.Api/Endpoints/AvatarEndpoints.cs` purchase/reward-track routes;
- `src/MathLearning.Infrastructure/Services/Cosmetics/CosmeticPlatformService.Public.cs`;
- `CosmeticPlatformService.Rewards.cs`, `CosmeticsFragmentService.cs` and cosmetics idempotency service;
- economy transactions, reward claims, chest claims, season/milestone claims and relevant unique indexes;
- existing Daily Run trust-boundary and cosmetics state-machine tests;
- mobile cosmetics claim/fragment/purchase callers and contract docs.

## Required authority model

Document one explicit entitlement model. A valid implementation may use a dedicated `CosmeticEntitlement`/settlement record or safely reuse a canonical existing settlement table when it can prove all of the following:

```text
entitlement id
+ authenticated user id
+ server-owned source type/reference
+ exact cosmetic item or fragment target
+ exact quantity
+ eligibility/settled timestamp
+ consumed timestamp/result
+ stable operation identity
```

The entitlement must be created by a trusted server operation such as Daily Run settlement, season milestone, reward track, achievement, admin-only audited grant or canonical shop purchase. A public request body is not a trusted source.

## Required implementation

1. Remove direct public authority from `POST /api/cosmetics/items/{itemKey}/claim`:
   - do not grant merely because the item exists and is active;
   - require a server entitlement/settlement reference scoped to the authenticated user;
   - derive the item and source from the stored entitlement;
   - reject item-key mismatch rather than silently using client data.
2. Remove direct public authority from non-Daily-Run `POST /api/cosmetics/fragments/grant`:
   - client `FragmentName`, `Copies`, `Source`, `SourceType`, `SourceEvent` and metadata may not determine the grant;
   - derive fragment target and copies from a stored server settlement;
   - enforce a bounded server-side quantity even for internal sources.
3. Decide whether the generic grant routes remain mobile-facing:
   - preferred: expose a narrow `consume entitlement` contract and keep generic grant methods internal/application-service only;
   - alternative: protect true operator endpoints with the exact admin policy and actor/target audit separation.
4. Canonical shop purchase must:
   - use stable operation identity and the existing economy/cosmetics ledger strategy;
   - derive price from the current server catalog;
   - reject inactive, hidden, not-yet-released, retired, default-only or non-purchasable items;
   - debit coins, create ownership/claim/audit and settle the response atomically;
   - replay the original result on duplicate same payload;
   - conflict on the same keys with a different item.
5. Preserve the existing Daily Run server-derived fragment path. Do not regress its chest/season settlement checks.
6. Make inventory uniqueness and entitlement consumption database-enforced. Do not rely on an `AnyAsync` pre-check alone.
7. Store safe provenance that cannot be forged by the request. Client metadata may be retained only as explicitly untrusted diagnostics and must be size/redaction bounded.
8. Define reconciliation for previously forged or unverifiable grants:
   - identify rows whose source cannot map to a valid settlement;
   - produce an audit/report or explicit revocation policy;
   - do not silently delete user inventory during deployment.
9. Update API inventory, architecture/gap docs and mobile contracts in the same change.

## Required PostgreSQL and HTTP tests

Prove with separate users, DbContexts and deterministic concurrency barriers:

- arbitrary active `itemKey` without entitlement is rejected and creates no ledger/inventory rows;
- a client cannot override the item attached to an entitlement;
- arbitrary fragment name/copies/source are rejected;
- spoofing `daily_run`, `season`, `badge`, `shop` or `admin` source text cannot create entitlement;
- valid entitlement settles exactly once and replays the exact stored response;
- same operation keys with another entitlement/item conflict;
- another user's entitlement is not found/forbidden without leaking its contents;
- two concurrent consumers create one inventory/progress mutation;
- purchase uses server price and rejects hidden/inactive/future/retired/non-purchasable items;
- insufficient-balance purchase leaves no ownership/claim and preserves balance;
- failure after SQL but before commit rolls back coin debit, inventory, entitlement consumption, audit and ledger completion;
- Daily Run fragment regression tests remain green;
- no public response or audit record trusts client source metadata as provenance.

## Validation

```text
dotnet test tests/MathLearning.Tests/MathLearning.Tests.csproj -c Release --filter "FullyQualifiedName~Cosmetic|FullyQualifiedName~Fragment|FullyQualifiedName~Purchase|FullyQualifiedName~Entitlement|FullyQualifiedName~DailyRunFragment|FullyQualifiedName~MutationUserScope"
dotnet build MathLearning.slnx -c Release
```

Provider-sensitive completion requires the PostgreSQL lane. Record exact constraints, transaction boundary and test count.

## Non-goals

- Do not redesign cosmetic rendering or Admin UI.
- Do not create a second idempotency ledger.
- Do not weaken existing Daily Run or reward-track checks to preserve an old client payload.

## Completion rule

Complete only when no authenticated mobile request can manufacture an item/fragment grant without a server entitlement, purchase and grant paths are atomic/idempotent on PostgreSQL, and mobile/backend contracts are synchronized.

---

# BACKEND-API-DB-010 — Eliminate legacy economy, hint and power-up bypasses

Priority: **P0**  
Run mode: compatibility-surface shutdown or canonical adapter repair  
Risk: legacy authenticated routes currently permit arbitrary coin changes, negative-amount minting, free access to paid hints and non-idempotent streak-freeze purchases.

## Goal

Make `/api/economy/*` the only backend-authoritative owner of balances, hint consumption and power-up purchases. Legacy routes must be read-only adapters or explicitly deprecated; they must never mutate balances/rewards outside the canonical settlement state machine.

## Inspect first

- `CoinEndpoints.cs`;
- `HintEndpoints.cs`;
- `PowerupEndpoints.cs`;
- `EconomySettlementEndpoints.cs` and economy transaction service;
- `UserHint`, `UserProfile`, economy transaction mappings and indexes;
- endpoint inventory, route compatibility audit and `BACKEND-TEST-034`;
- mobile callers for `/api/coins/*`, `/api/hints/*`, `/api/questions/*/hint/*`, `/api/powerups/*` and `/api/economy/*`.

## Required implementation

1. Inventory every compatibility route and classify it as:
   - canonical read-only alias;
   - canonical mutation adapter using the same service/ledger;
   - deprecated response (`410 Gone` or versioned stable error);
   - operator-only route protected by an exact policy.
2. Immediately close amount exploits:
   - no public route may earn an arbitrary client-provided amount;
   - spend/purchase amounts must be positive, server-derived and bounded against overflow;
   - negative, zero, `int.MinValue`, `int.MaxValue` and overflow combinations must fail before DB access.
3. `/api/coins/earn` must not remain an authenticated self-grant surface. Remove it, make it operator-only with audit, or replace it with consumption of a server reward entitlement.
4. `/api/coins/spend` must delegate to canonical `/api/economy/coins/spend` semantics or be deprecated. It must require stable operation identity and preserve exact replay/conflict behavior.
5. Hint access:
   - GET routes are pure reads and never debit coins or insert usage;
   - one explicit POST mutation settles a hint with server price/free-hint policy and stable operation identity;
   - legacy `/api/questions/{id}/hint/*` must not return paid formula/clue/elimination/solution content without the same settled entitlement;
   - previously unlocked hints can be read idempotently at zero additional cost through an explicit read contract.
6. Streak-freeze purchase must use the canonical economy transaction, server price, maximum count, exact replay and PostgreSQL-safe concurrency. Two purchases racing at the max/balance boundary must not overspend or exceed the cap.
7. Consolidate coin history from canonical transaction/provenance records. Do not present hint rows as the complete coin ledger while earnings are omitted.
8. Keep response compatibility only where it does not preserve an unsafe mutation. Add deprecation metadata and a removal date/version where legacy clients still exist.
9. Update API inventory, compatibility audit, backend gap report and mobile routes/tests.

## Required tests

Add raw HTTP and relational tests proving:

- arbitrary positive `/api/coins/earn` cannot mint coins;
- negative spend cannot increase coins or reduce total-spent counters;
- zero/extreme/overflow amounts are rejected before writes;
- legacy and canonical duplicate requests cannot settle twice;
- a paid hint cannot be fetched for free through another alias;
- all GET hint/coin routes perform zero INSERT/UPDATE/DELETE commands;
- first hint settlement debits once, duplicate replays, different payload conflicts;
- two concurrent first hint uses result in one debit/usage row;
- two concurrent streak-freeze purchases respect balance and max count;
- unsupported/deprecated routes return the documented stable status/body;
- canonical coin history reconciles exactly with balance counters for seeded transactions;
- another user's question/hint/transaction state cannot be consumed.

## Validation

```text
dotnet test tests/MathLearning.Tests/MathLearning.Tests.csproj -c Release --filter "FullyQualifiedName~Coin|FullyQualifiedName~Economy|FullyQualifiedName~Hint|FullyQualifiedName~Powerup|FullyQualifiedName~StreakFreeze|FullyQualifiedName~RouteCompatibility"
dotnet build MathLearning.slnx -c Release
```

Use PostgreSQL concurrency tests for balance and uniqueness invariants. Record zero-write GET SQL evidence.

## Non-goals

- Do not invent a new wallet table without first reconciling the existing economy transaction strategy.
- Do not keep insecure mutations merely because a legacy client still calls them.
- Do not duplicate stale-pending recovery work owned by `BACKEND-API-DB-015`.

## Completion rule

Complete only when all authoritative balance/hint/power-up mutations flow through one server-owned idempotent settlement path, compatibility bypass tests are green and mobile migration/deprecation is documented.

---

# BACKEND-API-DB-011 — Make leaderboard identity and cursor string-safe

Priority: **P0**  
Run mode: runtime crash fix, contract migration and PostgreSQL keyset proof  
Risk: student leaderboard currently parses ASP.NET Identity string/GUID user IDs as integers in cursor and ranking paths.

## Goal

Use the canonical string `UserId` end-to-end for ranking tie-breaks and keyset pagination, with deterministic ordering, versioned cursor decoding and no numeric-ID assumptions.

## Inspect first

- `StudentLeaderboardService.cs`;
- `LeaderboardRankingUtils.cs`;
- `LeaderboardCursor.cs` and `CursorCodec.cs`;
- `ScoreSelector`, leaderboard DTOs/endpoints and mobile cursor parsing;
- Identity/user-profile creation and migrations;
- existing leaderboard rank, bounds and compatibility tests;
- PostgreSQL indexes for daily/weekly/monthly/all-time ordering.

## Required implementation

1. Remove every `int.Parse`, `Convert.ToInt32` or numeric assumption applied to `UserProfile.UserId`.
2. Change the keyset cursor tiebreak component to canonical string user ID, using the same deterministic comparison as the SQL `ORDER BY`.
3. Define exact ordering for each period:

```text
score DESC, userId ASC
```

Use an explicitly documented ordinal/database collation strategy so cursor predicate and ordering cannot disagree.
4. Version the cursor payload, for example:

```json
{ "v": 2, "score": 123, "userId": "...", "scope": "global", "period": "week" }
```

Bind cursor to normalized scope/period so a token from one leaderboard cannot be reused against another silently.
5. Bound cursor length and decoded JSON size. Invalid, unsupported-version or mismatched cursors return a stable 400 contract rather than silently restarting page one.
6. Define old integer-cursor behavior:
   - accept through a time-limited compatibility decoder only where unambiguous; or
   - return a documented cursor-version error and make mobile restart pagination.
7. Keep ranking/count predicates SQL-translatable. Do not introduce client-side enumeration or `string.Compare` forms that Npgsql cannot translate/use efficiently.
8. Ensure rank, `Me`, page items and next cursor all use the same normalized scope/period and tiebreak rules.
9. Update OpenAPI, endpoint inventory and mobile contract/parser.

## Required tests and measurements

Prove:

- GUID-shaped, alphanumeric and legacy numeric string IDs all work;
- equal-score users have stable deterministic ordering;
- traversing all pages produces no duplicate or missing users;
- inserting/updating a user between pages follows the documented consistency model;
- cursor from another scope/period is rejected;
- malformed Base64, oversized JSON, missing fields and unsupported version return stable 400 responses;
- page one, later pages and `includeMe` never call numeric parsing;
- rank predicate and page predicate agree for all periods/scopes;
- PostgreSQL executes the keyset predicate server-side and uses an appropriate index or measured acceptable plan;
- mobile parser handles v2 and the chosen v1 transition behavior.

## Validation

```text
dotnet test tests/MathLearning.Tests/MathLearning.Tests.csproj -c Release --filter "FullyQualifiedName~StudentLeaderboard|FullyQualifiedName~LeaderboardCursor|FullyQualifiedName~LeaderboardRank|FullyQualifiedName~LeaderboardPagination"
dotnet build MathLearning.slnx -c Release
```

Capture `EXPLAIN (ANALYZE, BUFFERS)` for first and later pages on representative PostgreSQL data.

## Non-goals

- Do not fix Redis/DB semantic parity here; that is `BACKEND-API-DB-012`.
- Do not switch Identity key type.
- Do not couple reward settlement to the leaderboard read while fixing pagination.

## Completion rule

Complete only when ordinary Identity GUID users can traverse and rank on every leaderboard scope/period, cursor errors are explicit, PostgreSQL plans are recorded and mobile compatibility is updated.

---

# BACKEND-API-DB-012 — Unify Redis and DB leaderboard contracts

Priority: **P1**  
Run mode: distributed cache/data-source parity and failover correctness  
Dependency: run after `BACKEND-API-DB-011` establishes the canonical string cursor/ranking contract.

## Goal

Return the same membership, ordering, ranks, cursor progression and `includeMe` semantics whether Redis is healthy, unavailable, stale or disabled.

## Inspect first

- `MathLearning.Services/RedisLeaderboardService.cs`;
- `DbBackedRedisLeaderboardService.cs`;
- `StudentLeaderboardService.cs` and ranking utilities;
- `IRedisLeaderboardService`, request/update DTOs and `LeaderboardEndpoints.cs`;
- service registration/fallback behavior;
- XP update jobs and Redis rebuild/backfill code;
- friend graph, school/faculty identity and leaderboard opt-in rules;
- mobile leaderboard pagination tests.

## Required canonical contract

Create one normalized request/value object used by all implementations, containing at least:

```text
authenticated user id
normalized scope and period
resolved school/faculty scope id when applicable
friend candidate policy
limit
versioned cursor
includeMe
```

The service must not trust a caller-supplied arbitrary school/faculty/friend set when the authenticated profile/graph is authoritative.

## Required implementation

1. Redis wrapper must no longer discard cursor, user or scope identity fields.
2. Scope membership:
   - global: opted-in users;
   - school/faculty: only the authenticated user's resolved scope unless an explicitly authorized operator contract says otherwise;
   - friends: authenticated user plus actual friend/followee graph according to one documented directionality rule.
3. Choose a Redis design that preserves correctness:
   - scope-specific sorted sets keyed by actual school/faculty identity and period; or
   - global score set plus bounded server-side candidate intersection/rank strategy.
   Do not return the global set for friends and filter only on the client.
4. Implement the same versioned keyset cursor and tie-break ordering as `BACKEND-API-DB-011`. The endpoint must return a newly computed next cursor, not echo the incoming token.
5. Define rank semantics under ties identically in Redis and SQL.
6. Define source of truth and freshness:
   - authoritative XP remains in PostgreSQL;
   - Redis updates are idempotent/absolute where possible rather than blind deltas that can double-apply;
   - include version/rebuild generation and stale detection;
   - on inconsistency, fall back or repair without returning a different user's scope.
7. Make runtime Redis failure recoverable after startup. Startup success followed by connection loss must not turn endpoint exceptions into 500s without a documented fallback policy.
8. Provide bounded rebuild/backfill by page and distributed ownership. Do not scan/load all profiles into memory.
9. Record safe metrics: source used, fallback reason, stale generation, page latency and parity mismatch counts without user-ID cardinality.
10. Update API inventory, architecture and mobile contract/status.

## Required parity and integration tests

Run one fixture through Redis and DB implementations and assert identical:

- global/school/faculty/friends membership;
- opt-out exclusion;
- ordering/tie ranks;
- page items and next cursors;
- `Me` rank/score/percentile semantics;
- empty/no-school/no-friends behavior;
- malformed/mismatched cursor handling.

Also prove:

- friends request never returns a non-friend global user;
- two schools/faculties cannot share a Redis key accidentally;
- incoming cursor is not echoed when a next page exists;
- duplicate XP delivery does not double score under the chosen absolute/idempotent update design;
- Redis disconnect after startup follows the documented DB fallback;
- stale/partial Redis data triggers safe behavior rather than silently wrong ranks;
- recovery/rebuild is bounded and resumable;
- concurrent updates/read pagination satisfy the documented consistency model.

## Validation

```text
dotnet test tests/MathLearning.Tests/MathLearning.Tests.csproj -c Release --filter "FullyQualifiedName~RedisLeaderboard|FullyQualifiedName~DbBackedRedisLeaderboard|FullyQualifiedName~LeaderboardParity|FullyQualifiedName~LeaderboardFailover|FullyQualifiedName~FriendsLeaderboard"
dotnet build MathLearning.slnx -c Release
```

Use a real disposable Redis instance plus PostgreSQL for integration proof; mock-only validation is insufficient.

## Non-goals

- Do not move authoritative XP out of PostgreSQL.
- Do not expose client-provided friend IDs as authority.
- Do not process cosmetic rewards from leaderboard GETs as part of cache parity work; pure-read settlement remains linked to `BE-PERF-013`.

## Completion rule

Complete only when the same fixture produces contract-equivalent Redis and DB responses, failover is executable, cursor progression is real and no scope can leak global users.

---

# BACKEND-API-DB-013 — Unify registration and repair incomplete accounts

Priority: **P1**  
Run mode: authentication/account-provisioning ownership and data repair  
Risk: anonymous legacy `/auth/register` creates Identity users and refresh tokens without the `UserProfile` required by most application APIs.

## Goal

Have exactly one public registration implementation that atomically/compensatingly creates all mandatory account state and never returns usable tokens for an incomplete account.

## Inspect first

- `AuthEndpoints.cs` canonical mobile and legacy register paths;
- Identity/user profile/refresh-token mappings and registration tests;
- `TestAccountSeeder` and any user-provisioning helpers;
- profile/settings/default-cosmetics initialization requirements;
- rate-limit/auth error middleware;
- mobile registration/login contracts and callers;
- existing rows where `AspNetUsers` lacks `UserProfiles` or vice versa.

## Required ownership decision

Choose one explicit public route, preferably the current canonical mobile contract or a deliberately renamed common route. Every alias must delegate to the same service or return a stable deprecation response. A comment saying Admin is not authorization.

## Required implementation

1. Extract a single account-provisioning application service responsible for:
   - normalized username/email validation;
   - Identity user/password creation;
   - required `UserProfile` defaults;
   - any mandatory settings/default ownership initialization chosen by the architecture;
   - refresh-token creation only after mandatory account state is durable;
   - a canonical response contract.
2. Use one safe relational transaction where Identity and profile share `ApiDbContext`, with tested compensation only for provider/test paths that cannot enlist the same transaction.
3. Never return access/refresh tokens if profile creation or commit failed.
4. Remove/deprecate anonymous legacy `/auth/register`, or protect a genuinely operator-only target with the exact admin policy and actor/target separation. Do not leave it anonymous because the route group is anonymous.
5. Make duplicate username/email races return stable safe 409/validation responses, not raw Identity/DB exceptions.
6. Enforce production registration rate-limit/abuse controls through a documented policy without storing passwords or full request bodies in logs.
7. Define reconciliation for incomplete historical accounts:
   - report Identity-only, profile-only and token-without-profile states;
   - decide safe profile backfill versus account/token invalidation;
   - make repair idempotent and auditable;
   - do not invent usernames/display names silently when conflicts exist.
8. Ensure login detects an incomplete account and follows the chosen repair/deny path rather than issuing more tokens and continuing partially.
9. Update endpoint inventory, OpenAPI, backend contract gap report and Flutter contract/status.

## Required tests

Prove:

- canonical anonymous registration creates Identity, profile and token once;
- legacy route is delegated, protected or returns documented deprecation status;
- failure after Identity SQL but before profile/token commit leaves no usable incomplete account;
- failure during token persistence rolls back/compensates user/profile;
- clean retry succeeds after a failed attempt;
- concurrent same username/email produces one account and one stable conflict;
- no token is returned when mandatory profile state is missing;
- login of an existing incomplete account follows the documented repair/deny contract;
- reconciliation detects and repairs/invalidates each orphan category idempotently;
- another user's account data is never returned;
- anonymous/auth/admin route metadata is exact;
- logs and public errors do not expose password, token or provider detail.

## Validation

```text
dotnet test tests/MathLearning.Tests/MathLearning.Tests.csproj -c Release --filter "FullyQualifiedName~Register|FullyQualifiedName~AccountProvision|FullyQualifiedName~AuthMobileRegistration|FullyQualifiedName~IncompleteAccount|FullyQualifiedName~MutationUserScope"
dotnet build MathLearning.slnx -c Release
```

Use PostgreSQL for uniqueness and transaction proof. Run an idempotent reconciliation dry-run against representative seeded data.

## Non-goals

- Do not modify Admin UI.
- Do not combine refresh-token hashing/lifecycle work from `BACKEND-API-DB-007` unless explicitly sharing one coordinated migration/evidence log.
- Do not silently auto-create profiles on every arbitrary read endpoint.

## Completion rule

Complete only when one public owner exists, tokens cannot escape before a complete account commit, orphan reconciliation is explicit and backend/mobile contracts are synchronized.

---

# BACKEND-API-DB-014 — Retire or rebuild the legacy photo-avatar path

Priority: **P1**  
Run mode: contract decision, durable file storage and compensation  
Risk: integer route IDs are incompatible with string/GUID Identity users, generated URLs do not match the route, and container-local files are not durable/shared.

## Goal

Choose one supported avatar model. Either safely deprecate photo avatars in favor of cosmetics avatars, or provide a string/self-scoped photo upload with durable storage, resolvable public access and transactional compensation.

## Inspect first

- legacy photo routes in `UserEndpoints.cs`;
- `LegacyAvatarUploadValidator` and validated `BACKEND-TEST-008` file-safety tests;
- `UserProfile.AvatarUrl`, public profile/search/leaderboard DTOs;
- cosmetics avatar routes/services in `AvatarEndpoints.cs` and `CosmeticsEndpoints.cs`;
- storage abstractions such as screenshot storage and deployment/runtime filesystem;
- mobile profile/avatar upload and rendering callers;
- existing local `uploads/avatars` data and cleanup needs.

## Required product/contract decision

Document one of:

### Option A — Deprecate photo avatars

- cosmetics appearance is the canonical avatar;
- upload/read routes return a stable deprecation contract for supported transition period;
- remove stale `AvatarUrl` from public contracts only through coordinated mobile migration;
- provide reconciliation/cleanup for existing stored paths.

### Option B — Retain photo avatars

- canonical authenticated upload route uses `/api/users/me/avatar-photo` or equivalent, never `id:int`;
- public read uses an opaque asset URL/key or signed URL, not an owner-only integer route;
- a durable object-storage abstraction is mandatory outside Development/Test.

## Required implementation when retained

1. Derive user ID from claims; no route user ID is needed for self upload.
2. Preserve and extend existing byte-level validation, extension normalization, size limits, path safety and content-type tests from `BACKEND-TEST-008`.
3. Store using a durable provider abstraction with bounded timeouts/cancellation. Local storage is explicitly Development/Test only.
4. Use an opaque storage key; do not expose filesystem paths or trust original filenames.
5. Define safe write ordering/compensation:
   - upload temporary/new object;
   - persist profile reference with concurrency control;
   - delete new object if DB commit fails;
   - delete prior object only after new reference commits;
   - retry cleanup through a durable orphan mechanism if deletion fails.
6. Public reads must resolve consistently across replicas/deploys and use correct cache/security headers. Private/signed semantics must match what profile DTOs expose.
7. Handle concurrent replacements deterministically so a slower old upload cannot overwrite a newer avatar.
8. Reconcile existing broken URLs and local files through an explicit migration/report; do not silently retain unresolvable URLs.
9. Update mobile contract/status, endpoint inventory and deployment configuration.

## Required tests

Whether retiring or retaining, prove the chosen contract. For retained uploads prove:

- GUID/string Identity user can upload successfully;
- route cannot target another user;
- generated public URL/key resolves through the configured storage provider;
- storage failure creates no DB reference;
- DB failure deletes/queues cleanup of the newly uploaded object;
- replacing an avatar cleans the old object only after commit;
- two concurrent replacements preserve the winning version and do not leak objects;
- path traversal, disguised content, oversized files and unsupported MIME remain rejected;
- public/private read authorization matches DTO exposure;
- another replica can serve/resolve the asset;
- cancellation propagates and does not leave a committed partial reference;
- deprecated route status/body is stable if Option A is chosen.

## Validation

```text
dotnet test tests/MathLearning.Tests/MathLearning.Tests.csproj -c Release --filter "FullyQualifiedName~AvatarUpload|FullyQualifiedName~AvatarStorage|FullyQualifiedName~LegacyAvatar|FullyQualifiedName~PublicProfile|FullyQualifiedName~AvatarConcurrency"
dotnet build MathLearning.slnx -c Release
```

Run integration tests against the selected object-storage emulator/provider, not only an in-memory mock.

## Non-goals

- Do not weaken validated upload-safety checks.
- Do not redesign cosmetic loadouts.
- Do not keep container-local storage in production behind an undocumented fallback.

## Completion rule

Complete only when normal Identity users have a coherent supported/deprecated contract, every advertised URL resolves according to its visibility model, durable storage/compensation is tested and mobile contracts are synchronized.

---

# BACKEND-API-DB-015 — Recover safely from stale pending economy/cosmetics operations

Priority: **P0/P1**  
Run mode: idempotency ownership, failure recovery and PostgreSQL concurrency  
Canonical links: extend the existing economy/cosmetics ledger strategy and tests in `BACKEND-TEST-014`; use provider/failure infrastructure from `BACKEND-TEST-032/033`.

## Goal

Prevent an operation from remaining permanently blocked in `Pending` after process failure or cancellation, while guaranteeing that stale recovery can never create two authoritative settlers.

## Confirmed failure window

Current begin services persist a pending economy/cosmetics row before endpoint code opens the domain transaction. A crash or cancellation after that save but before completion can leave every retry returning `transaction_in_progress` forever.

## Inspect first

- `EconomyTransactionService.cs`;
- `CosmeticsIdempotencyService.cs`;
- economy/cosmetics endpoint helpers and every endpoint using them;
- transaction helpers/execution strategies;
- ledger entity mappings, unique indexes and retention policy;
- existing state-machine, relational idempotency and cancellation tests;
- clock/time abstractions and deployment multi-replica behavior.

## Required design decision

Choose and document one of these patterns:

### Pattern A — Single authoritative database transaction

Atomically claim/insert the ledger, execute domain writes and store settled response in one transaction/execution strategy. A rollback removes the uncommitted pending claim. Concurrent creators resolve through unique constraints/locking and then replay the committed result.

### Pattern B — Durable lease ownership

When work cannot remain inside one transaction, add fields such as:

```text
OwnerToken
LeaseExpiresAtUtc
AttemptCount
LastHeartbeatAtUtc
NextAttemptAtUtc
```

Acquire or take over with one PostgreSQL atomic compare-and-swap/row-lock statement. Only the current owner token may complete/fail/renew. Lease expiry is based on an injected clock and a documented maximum operation duration.

Do not invent a third independent idempotency storage pattern.

## Required implementation

1. Move the effective processing claim into the authoritative transaction or implement lease ownership atomically.
2. Exact semantics:
   - first request owns and settles once;
   - duplicate same payload after completion replays exact body/status;
   - same keys/different canonical payload conflicts;
   - active owner returns documented in-progress/retry metadata;
   - genuinely stale owner can be taken over once;
   - failed deterministic business result remains replayable according to current policy.
3. Domain mutation and final ledger state/response commit atomically where they share the DB. A completed ledger must never exist without the balance/inventory mutation, and the mutation must never commit with an unowned/unrecoverable pending ledger.
4. Persist ownership/version fields with indexes matching claim/recovery predicates. Use database UTC/current time deliberately or an injected clock with tested skew assumptions.
5. Cancellation before commit must rollback/release naturally. Do not convert request cancellation into a permanent failed business result unless that is explicitly required.
6. Unexpected process death is simulated by disposing the owner context/connection without cleanup; next request must recover after the documented stale threshold.
7. Two replicas racing to take over one stale operation must produce one owner and one in-progress/replay result.
8. If heartbeats are needed, make them bounded and cancellable; never extend a lease after ownership was lost.
9. Add operator observability and bounded remediation for aged pending rows. A sweeper may surface or recover candidates but must not blindly fail active work.
10. Define retention for completed/failed ledgers separately from active/stale pending work.
11. Coordinate this prompt with `BACKEND-API-DB-009/010` so newly canonical cosmetic/economy mutations use the repaired ownership model.

## Required PostgreSQL failure/concurrency matrix

Use independent connections/DbContexts, injected clock and deterministic barriers to prove:

- failure immediately after claim SQL but before domain SQL is recoverable;
- failure after domain SQL but before commit leaves neither domain nor completed ledger state;
- cancellation at claim, mutation, completion and commit boundaries is safe;
- active unexpired lease cannot be stolen;
- stale lease can be taken over;
- two stale takeovers race and exactly one wins;
- old owner cannot complete after losing ownership;
- heartbeat cannot revive a lost lease;
- duplicate after successful takeover replays the exact stored response;
- same key/different payload conflicts before domain effects;
- cross-user and cross-operation-type isolation remains intact;
- clock boundary/skew cases do not create dual ownership;
- retention excludes pending/recoverable rows and uses the intended index.

## Validation

```text
dotnet test tests/MathLearning.Tests/MathLearning.Tests.csproj -c Release --filter "FullyQualifiedName~EconomyTransaction|FullyQualifiedName~CosmeticsIdempotency|FullyQualifiedName~PendingRecovery|FullyQualifiedName~Lease|FullyQualifiedName~RelationalIdempotency|FullyQualifiedName~Cancellation"
dotnet build MathLearning.slnx -c Release
```

Record PostgreSQL version, isolation/locking SQL, stale threshold and exact test count. InMemory/SQLite-only proof is not sufficient.

## Non-goals

- Do not change outbox claim/backoff work owned by `BACKEND-TEST-023`.
- Do not solve permanent Pending by deleting rows blindly or allowing every retry to reprocess.
- Do not mark all old pending rows Failed without proving whether domain effects committed.

## Completion rule

Complete only when crash/cancellation windows are executable, one stale operation can be recovered by exactly one owner, domain and result settlement are atomic and every canonical economy/cosmetics endpoint uses the repaired contract.

---

## Recommended execution sequence

1. Finish current baseline/evidence owners and `BACKEND-MIGRATION-001` without weakening the schema gate.
2. `BACKEND-API-DB-009` — close direct cosmetic entitlement forgery.
3. `BACKEND-API-DB-010` — close legacy coin/hint/power-up bypasses.
4. `BACKEND-API-DB-011` — repair string/GUID leaderboard identity and cursor.
5. `BACKEND-API-DB-015` — make canonical economy/cosmetics settlement recoverable.
6. `BACKEND-API-DB-012` — prove Redis/DB leaderboard parity after the cursor contract is stable.
7. `BACKEND-API-DB-013` — unify account provisioning and reconcile incomplete accounts.
8. `BACKEND-API-DB-014` — retire or rebuild the legacy photo-avatar path.
9. Continue first-pass `BACKEND-API-DB-001…008` and linked test/performance owners according to central risk/dependency order.

## Queue completion rule

A row may move from `Prompt-ready` only with a referenced `.ai/runs` evidence file. Contract-sensitive rows require explicit backend/mobile synchronization. PostgreSQL-sensitive rows require real provider evidence; Redis/storage-sensitive rows require the real disposable dependency or emulator specified by the prompt. Static review and the existing 996-test green baseline do not prove these new invariants.
