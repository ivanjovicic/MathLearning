# Backend API Endpoint Inventory

Last aligned: 2026-06-25  
Repo: `ivanjovicic/MathLearning`

This inventory is for agents and backend/mobile contract work. It is intentionally compact: route, auth, owner file, and notes. Always inspect the owning endpoint file before changing a route.

Primary source: `src/MathLearning.Api/Program.cs` endpoint map plus endpoint files under `src/MathLearning.Api/Endpoints/`.

---

## 1. Inventory rules

Update this file when:

- a route is added, removed, renamed, or deprecated
- a canonical route changes payload/response semantics
- a legacy route becomes mobile-facing or is retired
- auth policy changes
- idempotency behavior changes

Do not mark routes verified only from docs. Route behavior must be backed by code/tests.

Auth legend:

| Value | Meaning |
|---|---|
| Public | No auth required. |
| Auth | Authenticated user required. |
| Admin | Admin role/policy required. |
| Mixed | Group has mixed auth or special rules. Inspect owner file. |
| Legacy | Exists for compatibility; do not expand without explicit reason. |

---

## 2. Runtime / health / observability

| Method | Route | Auth | Owner | Notes |
|---|---|---|---|---|
| GET | `/` | Public | `Program.cs` | Basic API running response. |
| GET | `/health` | Public | `Program.cs` | ASP.NET health checks. |
| GET | `/health/background-jobs` | Public | `Program.cs` | Hangfire/background job startup state. |
| GET | `/api/health/background-jobs` | Public | `Program.cs` | API alias for background job startup state. |
| GET | `/metrics` | Public/internal | `Program.cs` | Minimal runtime metrics; no Prometheus dependency. |
| GET | `/api/health/` | Public | `HealthEndpoints.cs` | Basic liveness. |
| GET | `/api/health/db` | Public | `HealthEndpoints.cs` | DB connectivity and schema summary. |
| GET | `/api/health/ready` | Public | `HealthEndpoints.cs` | Readiness: DB, schema, data counts. |
| GET | `/api/health/schema` | Public | `HealthEndpoints.cs` | Schema/migration state. |
| GET | `/health/schema` | Public | `HealthEndpoints.cs` | Canonical schema health alias. |
| GET | `/api/idempotency/observability/*` | Admin | `IdempotencyObservabilityEndpoints.cs` | Safe idempotency telemetry. Verify exact subroutes in file. |
| GET | `/api/monitoring/jobs` | Public/internal | `Program.cs` | Mock/admin UI monitoring payload. |
| GET | `/api/monitoring/logs` | Public/internal | `Program.cs` | Reads log file if present. |
| GET | `/api/monitoring/logs-advanced` | Public/internal | `Program.cs` | Filtered log-file reader. |

---

## 3. Authentication

Owner: `AuthEndpoints.cs`

| Method | Route | Auth | Status | Notes |
|---|---|---|---|---|
| POST | `/auth/mobile/register` | Public | Canonical mobile | Creates Identity user, UserProfile, access/refresh tokens. |
| POST | `/auth/login` | Public | Canonical group route | Login with refresh token issuance. |
| POST | `/api/auth/login` | Public | Compatibility alias | Mobile/API alias for login. |
| POST | `/auth/refresh` | Public | Canonical | Rotates refresh token and returns new access token. |
| POST | `/auth/logout` | Public | Canonical | Revokes supplied refresh token. |
| POST | `/auth/revoke-all` | Auth | Canonical | Revokes all current user's refresh tokens. |
| POST | `/auth/register` | Public/legacy | Legacy/admin-era | Existing register path; inspect before mobile use. |

---

## 4. Users / profile / settings

Owner: `UserEndpoints.cs`

| Method | Route | Auth | Status | Notes |
|---|---|---|---|---|
| GET | `/api/users/profile` | Auth | Canonical mobile | Current user's profile + appearance. |
| PUT | `/api/users/profile` | Auth | Canonical mobile | Update current user's profile. |
| GET | `/api/users/stats` | Auth | Canonical | Current user's stats/profile aggregate. |
| GET | `/api/users/search` | Auth | Canonical | Search users by query. |
| GET | `/api/user/profile/{userId}` | Auth | Legacy/canonical public profile route | Profile by user id with appearance. |
| GET | `/api/users/{userId}/profile` | Auth | Compatibility alias | Alias for profile-by-id. |
| GET | `/api/user/coins` | Auth | Legacy read | Current coins/progress summary. |
| GET | `/api/user/daily-hints` | Auth | Legacy read | Daily hints alias. |
| GET | `/api/user/hints/daily` | Auth | Legacy read | Daily hints alias. |
| GET | `/users/{userId}/settings` | Auth | Canonical settings | Route user must match auth user. |
| PATCH | `/users/{userId}/settings` | Auth | Canonical settings | Route user must match auth user. |
| POST | `/users/{id}/avatar` | Auth | Legacy/profile upload caveat | Verify mobile caller shape before touching. Canonical avatar equip is `/api/cosmetics/avatar`. |

---

## 5. Quiz and SRS

Owners: `QuizEndpoints.cs`, `SrsEndpoints.cs`

| Method | Route | Auth | Status | Notes |
|---|---|---|---|---|
| POST | `/api/quiz/start` | Auth | Canonical | Start quiz session. |
| GET | `/api/quiz/questions` | Auth | Legacy/mobile content | Fetch legacy/mobile questions. |
| POST | `/api/quiz/questions` | Auth | Legacy/mobile content | Fetch questions by posted payload. |
| POST | `/api/quiz/next-question` | Auth | Canonical/adaptive | Next question for subtopic. |
| POST | `/api/quiz/answer` | Auth | Canonical P0 mutation | Idempotent when operation keys are supplied. Operation type: `quiz_answer`. |
| POST | `/api/quiz/offline-submit` | Auth | Offline/sync | Inspect before changing. User scoped by auth. |
| POST | `/api/quiz/batch-submit` | Auth | Offline/sync | Inspect before changing. User scoped by auth. |
| POST | `/api/quiz/srs/update` | Auth | Canonical P0 mutation | Idempotent when operation keys are supplied. Operation type: `srs_update`. |
| GET | `/api/quiz/srs/daily` | Auth | Canonical SRS read | Due SRS cards with mobile fallback padding. |
| GET | `/api/quiz/srs/mixed` | Auth | Canonical SRS read | Due + random SRS mix. |
| GET | `/api/quiz/srs/streak` | Auth | SRS read | Current SRS streak. Inspect file for response. |

---

## 6. Practice sessions

Owner: `PracticeSessionEndpoints.cs`

| Method | Route | Auth | Status | Notes |
|---|---|---|---|---|
| POST | `/api/practice/session/start` | Auth | Canonical | Starts user-owned practice session. |
| POST | `/api/practice/session/{sessionId:guid}/answer` | Auth | Canonical | Submit answer; session ownership must be enforced. |
| POST | `/api/practice/session/{sessionId:guid}/complete` | Auth | Canonical | Complete session; ownership must be enforced. |

---

## 7. Progress

Owner: `ProgressEndpoints.cs`

| Method | Route | Auth | Status | Notes |
|---|---|---|---|---|
| GET | `/api/progress/overview` | Auth | Canonical mobile | Attempts, accuracy, streak, freezes, streak event. |
| GET | `/api/progress/weak-areas` | Auth | Canonical | Lowest accuracy subtopics. |
| GET | `/api/progress/topics` | Auth | Canonical | Topic progress. |
| GET | `/api/topics/progress` | Auth | Legacy alias | Alias for topic progress. |
| POST | `/api/progress/sync` | Auth | Mobile sync | Writes daily completion; user from auth. |

---

## 8. Economy / seasons / shop

Owner: `EconomySettlementEndpoints.cs`

| Method | Route | Auth | Status | Notes |
|---|---|---|---|---|
| POST | `/api/economy/coins/spend` | Auth | Canonical P0 mutation | Uses `economy_transactions`; operation id supported. |
| POST | `/api/economy/hints/use` | Auth | Canonical P0 mutation | Uses `economy_transactions`; free hint / coin debit. |
| GET | `/api/economy/rewards/preview` | Auth | Canonical read | Preview reward resolution. |
| POST | `/api/economy/rewards/claim` | Auth | Canonical P0 mutation | Idempotent reward claim. |
| POST | `/api/shop/streak-freeze/purchase` | Auth | Canonical P0 mutation | Idempotent streak-freeze purchase. |
| POST | `/api/seasons/daily-run-claim` | Auth | Canonical P0 mutation | Season Daily Run XP claim. |
| POST | `/api/seasons/milestones/{milestoneId}/claim` | Auth | Canonical P0 mutation | Season milestone claim. |
| POST | `/api/admin/economy/rewards/grant` | Admin | Admin | Actor from auth, target from request. |

---

## 9. Cosmetics and avatar

Owners: `CosmeticsEndpoints.cs`, `AvatarEndpoints.cs`

Canonical mobile routes:

| Method | Route | Auth | Status | Notes |
|---|---|---|---|---|
| GET | `/api/cosmetics/catalog` | Auth | Canonical mobile | Published catalog, ETag support. |
| GET | `/api/cosmetics/inventory` | Auth | Canonical mobile | Current user's item keys and fragment progress. |
| GET | `/api/cosmetics/avatar` | Auth | Canonical mobile | Current user's equipped avatar slots. |
| PUT | `/api/cosmetics/avatar` | Auth | Canonical mobile | Persist equipped avatar slots with ownership validation. |
| POST | `/api/cosmetics/items/{itemKey}/claim` | Auth | Canonical P0 mutation | Uses cosmetics idempotency ledger. |
| POST | `/api/cosmetics/fragments/grant` | Auth | Canonical P0 mutation | Uses cosmetics idempotency ledger. |

Legacy/compatibility avatar routes exist in `AvatarEndpoints.cs`. Do not expand them for mobile runtime unless explicitly required. Canonical mobile avatar equip is `PUT /api/cosmetics/avatar`.

---

## 10. Daily Run

Owner: `DailyRunEndpoints.cs`

| Method | Route | Auth | Status | Notes |
|---|---|---|---|---|
| POST | `/api/daily-run/chest/claim` | Auth | Canonical P0 mutation | Server-authoritative, idempotent via domain-table Policy B. |

---

## 11. Hints / coins / powerups legacy surfaces

Owners: `HintEndpoints.cs`, `CoinEndpoints.cs`, `PowerupEndpoints.cs`

| Route family | Auth | Status | Notes |
|---|---|---|---|
| `/api/hints/*` | Auth | Canonical/legacy read depending route | Mobile reads hints; economy settlement for hint cost is `/api/economy/hints/use`. |
| `/api/coins/*` | Auth | Legacy | Do not use for new mobile settlement. Prefer `/api/economy/*`. |
| `/api/powerups/*` | Auth | Legacy/feature | Inspect before modifying. |

---

## 12. Leaderboard / adaptive / analytics / explanations / authoring / sync / maintenance

Owners: `LeaderboardEndpoints.cs`, `AdaptiveEndpoints.cs`, `AnalyticsEndpoints.cs`, `ExplanationEndpoints.cs`, `QuestionAuthoringEndpoints.cs`, `SyncEndpoints.cs`, `MaintenanceEndpoints.cs`, `LoggingEndpoints.cs`, `BugEndpoints.cs`.

| Route family | Auth | Owner | Notes |
|---|---|---|---|
| `/api/leaderboard*` | Mixed/Auth | `LeaderboardEndpoints.cs` | Global/friends/schools/admin leaderboard surfaces. Inspect file before route changes. |
| `/api/adaptive*` | Auth | `AdaptiveEndpoints.cs` | Adaptive path/recommendation/review surfaces. |
| `/api/analytics*` | Auth/Admin depending route | `AnalyticsEndpoints.cs` | Analytics/recommendation surfaces. |
| `/api/explanations*` | Auth | `ExplanationEndpoints.cs` | Explanation generation/read surfaces. |
| question authoring routes | Auth/Admin depending route | `QuestionAuthoringEndpoints.cs` | Admin/content authoring. |
| `/api/sync*` | Auth | `SyncEndpoints.cs` | Offline sync transport. Reject mismatched payload user ids. |
| `/api/maintenance*` | Admin/internal | `MaintenanceEndpoints.cs` | Maintenance operations. |
| logging routes | Admin/internal | `LoggingEndpoints.cs` | Logging/admin support. |
| bug routes | Mixed | `BugEndpoints.cs` | Bug reporting/support. |

Because these families contain more route variants and some admin/internal behavior, inspect the owning file and tests before changing them. Add precise rows here when a route becomes mobile-critical or release-critical.

---

## 13. Canonical mobile P0 mutation checklist

Before changing any route below, inspect backend tests and mobile contract:

| Endpoint | Operation type / policy | Evidence area |
|---|---|---|
| `POST /api/quiz/answer` | `quiz_answer` | `QuizAnswerIdempotencyTests.cs`, `MobileMutationContractIntegrationTests.cs` |
| `POST /api/quiz/srs/update` | `srs_update` | `SrsUpdateIdempotencyTests.cs`, `MobileMutationContractIntegrationTests.cs` |
| `POST /api/daily-run/chest/claim` | `daily_run_chest_claim`, Policy B | `DailyRunChestClaimIdempotencyTests.cs`, endpoint tests |
| `POST /api/seasons/daily-run-claim` | `season_daily_run_claim` | economy contract/idempotency tests |
| `POST /api/seasons/milestones/{milestoneId}/claim` | `season_milestone_claim` | economy contract/idempotency tests |
| `POST /api/cosmetics/fragments/grant` | `cosmetics_fragment_grant` | cosmetics contract/idempotency tests |
| `POST /api/cosmetics/items/{itemKey}/claim` | `cosmetics_item_claim` | cosmetics contract/idempotency tests |
| `POST /api/economy/coins/spend` | `economy_coins_spend` | economy idempotency/contract tests |
| `POST /api/economy/hints/use` | `economy_hint_use` | economy idempotency/contract tests |
| `POST /api/economy/rewards/claim` | `economy_reward_claim` | economy idempotency/contract tests |
| `POST /api/shop/streak-freeze/purchase` | `shop_streak_freeze_purchase` | economy idempotency/contract tests |
