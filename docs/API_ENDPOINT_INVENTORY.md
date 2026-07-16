# Backend API Endpoint Inventory

Last aligned: 2026-07-03  
Repo: `ivanjovicic/MathLearning`

This inventory is for agents and backend/mobile contract work. It is intentionally compact: route, auth, owner file, and notes. Always inspect the owning endpoint file before changing a route.

**Route compatibility audit:** [`BACKEND_ROUTE_COMPATIBILITY_AUDIT.md`](BACKEND_ROUTE_COMPATIBILITY_AUDIT.md)  
**Current coverage audits:** [`BACKEND_TEST_COVERAGE_AUDIT_2026_07_03.md`](BACKEND_TEST_COVERAGE_AUDIT_2026_07_03.md), [`BACKEND_TEST_COVERAGE_AUDIT_2026_07_03_PASS2.md`](BACKEND_TEST_COVERAGE_AUDIT_2026_07_03_PASS2.md)

Primary source: `src/MathLearning.Api/Program.cs` plus endpoint files under `src/MathLearning.Api/Endpoints/`.

## Inventory rules

Update this file whenever routes, payload/response semantics, auth policy, idempotency or canonical/legacy status changes. Do not mark behavior verified only from documentation.

Auth legend:

| Value | Meaning |
|---|---|
| Public | No authentication required. |
| Auth | Authenticated user required. |
| Admin | Exact admin role/policy required. |
| Content author | Admin or content-author policy required. |
| Mixed | Inspect owning endpoint file. |
| Legacy | Compatibility-only surface. |

---

## Runtime / health / observability

| Method | Route | Auth | Owner | Notes |
|---|---|---|---|---|
| GET | `/` | Public | `Program.cs` | Basic API running response. |
| GET | `/health` | Public | `Program.cs` | ASP.NET health checks. |
| GET | `/health/background-jobs` | Public | `Program.cs` | Background-job startup state. |
| GET | `/api/health/background-jobs` | Public | `Program.cs` | API alias. |
| GET | `/metrics` | Public/internal | `Program.cs` | Process metrics; public-detail minimization remains BACKEND-TEST-026. |
| GET | `/api/health/` | Public | `HealthEndpoints.cs` | Basic liveness. |
| GET | `/api/health/db` | Public | `HealthEndpoints.cs` | DB/schema summary; detail minimization pending. |
| GET | `/api/health/ready` | Public | `HealthEndpoints.cs` | Readiness plus data counts; detail minimization pending. |
| GET | `/api/health/schema` | Public | `HealthEndpoints.cs` | Migration/schema detail; minimization pending. |
| GET | `/health/schema` | Public | `HealthEndpoints.cs` | Canonical schema-health alias. |
| GET | `/api/idempotency/observability/*` | Admin | `IdempotencyObservabilityEndpoints.cs` | Safe idempotency telemetry. |
| GET | `/api/monitoring/jobs` | Public/internal | `Program.cs` | Mock monitoring payload; protect/remove under BACKEND-TEST-026. |
| GET | `/api/monitoring/logs` | Admin (`UiTokensAdminPolicy`) | `MonitoringLogEndpoints.cs` | Redacted log file read. |
| GET | `/api/monitoring/logs-advanced` | Admin (`UiTokensAdminPolicy`) | `MonitoringLogEndpoints.cs` | Redacted bounded filtered log read. |
| GET | `/api/logs/recent` | Admin (`UiTokensAdminPolicy`) | `LoggingEndpoints.cs` | Redacted DB log read. |

---

## Authentication

Owner: `AuthEndpoints.cs`

| Method | Route | Auth | Status | Notes |
|---|---|---|---|---|
| POST | `/auth/mobile/register` | Public | Canonical mobile | Creates Identity user, profile and tokens with generic registration failures, real email parsing, and confirmed managed/no-email provisioning. |
| POST | `/auth/login` | Public | Canonical | Lockout-aware login and refresh-token issuance with generic 401/429 contract. |
| POST | `/api/auth/login` | Public | Compatibility alias | Same handler and lockout/throttle contract as `/auth/login`. |
| POST | `/auth/refresh` | Public | Canonical | Single-use token rotation with generic 401/429 contract. Model length drift remains BACKEND-TEST-012. |
| POST | `/auth/logout` | Public | Canonical | Revokes supplied refresh token. |
| POST | `/auth/revoke-all` | Auth | Canonical | Revokes all current-user refresh tokens and invalidates existing access tokens by rotating the user security stamp. |
| POST | `/auth/register` | Public/legacy | Legacy | Generic conflict/failure contract, real email parsing, and confirmed managed/no-email provisioning. |

---

## Users / profile / settings

Owner: `UserEndpoints.cs`

| Method | Route | Auth | Status | Notes |
|---|---|---|---|---|
| GET | `/api/users/profile` | Auth | Canonical mobile | Current profile and appearance. |
| PUT | `/api/users/profile` | Auth | Canonical mobile | Update current profile. |
| GET | `/api/users/stats` | Auth | Canonical | Current-user statistics. |
| GET | `/api/users/search` | Auth | Canonical | Bounded search; public identity allowlist tested. |
| GET | `/api/user/profile/{userId}` | Auth | Public-profile read | Privacy allowlist tested. |
| GET | `/api/users/{userId}/profile` | Auth | Compatibility alias | Profile-by-id alias. |
| GET | `/api/user/coins` | Auth | Legacy read | Current coin/progress summary. |
| GET | `/api/user/daily-hints` | Auth | Legacy read | Daily hints alias. |
| GET | `/api/user/hints/daily` | Auth | Legacy read | Daily hints alias. |
| GET | `/users/{userId}/settings` | Auth | Canonical settings | Route user must equal auth user. |
| PATCH | `/users/{userId}/settings` | Auth | Canonical settings | Route user must equal auth user. |
| POST | `/users/{id}/avatar` | Auth | Legacy upload | Owner-only, size/type/content validation. |
| GET | `/users/{id}/avatar/{fileName}` | Auth | Legacy read | Owner-only; static avatar bypass blocked. |

---

## Quiz and SRS

Owners: `QuizEndpoints.cs`, `SrsEndpoints.cs`

| Method | Route | Auth | Status | Notes |
|---|---|---|---|---|
| POST | `/api/quiz/start` | Auth | Canonical | Question count normalized to 1..25; pre-answer question shape omits answer key and full solution material. |
| GET | `/api/quiz/questions` | Auth | Legacy/mobile content | Count normalized to 1..25; pre-answer question shape omits answer key and full solution material. |
| POST | `/api/quiz/questions` | Auth | Legacy/mobile content | Posted question request; count bounded; pre-answer question shape omits answer key and full solution material. |
| POST | `/api/quiz/next-question` | Auth | Canonical/adaptive | Next question; pre-answer question shape omits answer key and full solution material. |
| POST | `/api/quiz/answer` | Auth | Canonical P0 mutation | Ledger used when operation keys are supplied. Missing-key decision remains BACKEND-TEST-013. |
| POST | `/api/quiz/offline-submit` | Auth | Canonical offline | Auth-scoped replay path. Durable analytics handoff remains BACKEND-TEST-022. |
| POST | `/api/quiz/batch-submit` | Auth | Legacy alias | Adapter to offline-submit. |
| POST | `/api/quiz/srs/update` | Auth | Canonical P0 mutation | Ledger used when operation keys are supplied. |
| GET | `/api/quiz/srs/daily` | Auth | Canonical SRS read | Due cards with fallback padding; pre-answer question shape omits answer key and full solution material. |
| GET | `/api/quiz/srs/mixed` | Auth | Canonical SRS read | Due plus random mix; pre-answer question shape omits answer key and full solution material. |
| GET | `/api/quiz/srs/streak` | Auth | SRS read | Current streak. |

---

## Practice sessions

Owner: `PracticeSessionEndpoints.cs`

| Method | Route | Auth | Status | Notes |
|---|---|---|---|---|
| POST | `/api/practice/session/start` | Auth | Canonical | Starts user-owned session. |
| POST | `/api/practice/session/{sessionId:guid}/answer` | Auth | Canonical | Ownership enforced. |
| POST | `/api/practice/session/{sessionId:guid}/complete` | Auth | Canonical | Ownership enforced. |

---

## Progress

Owner: `ProgressEndpoints.cs`

| Method | Route | Auth | Status | Notes |
|---|---|---|---|---|
| GET | `/api/progress/overview` | Auth | Canonical mobile | Attempts, accuracy, streak and freezes. |
| GET | `/api/progress/weak-areas` | Auth | Canonical | Lowest-accuracy subtopics. |
| GET | `/api/progress/topics` | Auth | Canonical | Topic progress. |
| GET | `/api/topics/progress` | Auth | Legacy alias | Topic-progress alias. |
| POST | `/api/progress/sync` | Auth | Mobile sync | Server-verifiable settlement; legacy completed/day payloads are rejected. |

---

## Leaderboard

Owner: `LeaderboardEndpoints.cs`

| Method | Route | Auth | Status | Notes |
|---|---|---|---|---|
| GET | `/api/leaderboard/student` | Auth | Canonical | String-safe rank/pagination contract. Cursor is versioned and bound to normalized `scope` + `period`; invalid or mismatched cursors return `400`. |
| GET | `/api/leaderboard/friends` | Auth | Canonical | Friends leaderboard; `/api/leaderboard/rivals` remains the compatibility alias. |
| GET | `/api/leaderboard/global` | Auth | Legacy canonical read | Legacy global leaderboard read surface. |
| GET | `/api/leaderboard/schools` | Auth | Canonical | School aggregate leaderboard. |
| GET | `/api/leaderboard/schools/{schoolId}` | Auth | Canonical | School leaderboard details / neighbors. |
| GET | `/api/leaderboard/schools/history/{schoolId}` | Auth | Canonical | School leaderboard history. |

---

## Economy / seasons / shop

Owner: `EconomySettlementEndpoints.cs`

| Method | Route | Auth | Status | Notes |
|---|---|---|---|---|
| POST | `/api/economy/coins/spend` | Auth | Canonical P0 | Idempotent economy transaction. |
| POST | `/api/economy/hints/use` | Auth | Canonical P0 | Free hint or coin debit. |
| GET | `/api/economy/rewards/preview` | Auth | Canonical read | Must not mutate. |
| POST | `/api/economy/rewards/claim` | Auth | Canonical P0 | Idempotent reward claim. |
| POST | `/api/shop/streak-freeze/purchase` | Auth | Canonical P0 | Idempotent purchase. |
| POST | `/api/seasons/daily-run-claim` | Auth | Canonical P0 | Season Daily Run XP. |
| POST | `/api/seasons/milestones/{milestoneId}/claim` | Auth | Canonical P0 | Season milestone claim. |
| POST | `/api/admin/economy/rewards/grant` | Admin | Admin | Actor from auth, target from body. |

---

## Cosmetics and Daily Run

Owners: `CosmeticsEndpoints.cs`, `AvatarEndpoints.cs`, `DailyRunEndpoints.cs`

| Method | Route | Auth | Status | Notes |
|---|---|---|---|---|
| GET | `/api/cosmetics/catalog` | Auth | Canonical mobile | Published catalog, ETag support. |
| GET | `/api/cosmetics/inventory` | Auth | Canonical mobile | Current-user inventory/fragments. |
| GET | `/api/cosmetics/avatar` | Auth | Canonical mobile | Current equipped slots. |
| PUT | `/api/cosmetics/avatar` | Auth | Canonical mobile | Ownership-validated equip. |
| POST | `/api/cosmetics/items/{itemKey}/claim` | Auth | Canonical P0 | Consume server-issued cosmetic item entitlement via cosmetics ledger. |
| POST | `/api/cosmetics/fragments/grant` | Auth | Canonical P0 | Daily Run server-derived grant or consume server-issued fragment entitlement via cosmetics ledger. |
| POST | `/api/daily-run/chest/claim` | Auth | Canonical P0 | Server-authoritative Policy B idempotency. |

Legacy avatar routes remain compatibility-only. Do not expand them for new mobile behavior.

---

## Hints / coins / powerups legacy surfaces

| Route family | Auth | Status | Notes |
|---|---|---|---|
| `/api/hints/*` | Auth | Canonical read + deprecated aliases | `POST /api/economy/hints/use` is the only paid-hint settlement path; `GET /api/hints/questions/{id}/*` is read-only and legacy `/api/questions/{id}/hint/*` aliases return `410 Gone`. |
| `/api/coins/*` | Auth | Legacy read + removed mutations | `POST /api/coins/earn` and `POST /api/coins/spend` return `410 Gone`; keep `/balance`, `/history`, and `/leaderboard` as compatibility read models only. |
| `/api/powerups/*` | Auth | Legacy read + removed mutation | `POST /api/powerups/streak-freeze/buy` returns `410 Gone`; canonical purchase is `/api/shop/streak-freeze/purchase`. |

---

## Analytics / recommendations / explanations

| Method/family | Auth | Owner | Notes |
|---|---|---|---|
| `/api/analytics/*` | Auth | `AnalyticsEndpoints.cs` | Claim-derived user scope. Page capped at 100; page size preserves prior clamp semantics. HTTP contracts covered by BACKEND-TEST-029. Database-level paging remains BACKEND-TEST-045. |
| GET `/api/recommendations/practice` | Auth | `AnalyticsEndpoints.cs` | Claim-derived user scope and bounded paging. |
| GET `/api/explanations/problem/{problemId}` | Auth | `ExplanationEndpoints.cs` | Blank language defaults to `en`; stable safe not-found response. |
| POST `/api/explanations/generate` | Auth | `ExplanationEndpoints.cs` | Validator short-circuit and safe not-found/500 tests added. Input/cost hardening remains BACKEND-TEST-043. |
| POST `/api/explanations/mistake-analysis` | Auth | `ExplanationEndpoints.cs` | Validator and safe-error tests added. Input/cost hardening remains BACKEND-TEST-043. |

---

## Question authoring / sync

| Method/family | Auth | Owner | Notes |
|---|---|---|---|
| POST `/api/questions/validate` | Content author | `QuestionAuthoringEndpoints.cs` | Dry-run validation. |
| POST `/api/questions/preview` | Content author | `QuestionAuthoringEndpoints.cs` | Safe preview. |
| POST `/api/questions/save-draft` | Content author | `QuestionAuthoringEndpoints.cs` | Persists draft. |
| POST `/api/questions/publish` | Content author | `QuestionAuthoringEndpoints.cs` | Publishes version. |
| GET `/api/questions/{id}/versions` | Content author | `QuestionAuthoringEndpoints.cs` | Version history. |
| GET `/api/questions/{id}/validation` | Content author | `QuestionAuthoringEndpoints.cs` | Latest validation. |
| POST `/api/questions/{id}/revalidate` | Content author | `QuestionAuthoringEndpoints.cs` | Revalidation. |
| GET `/api/admin/sync/dead-letters` | Admin | `AdminSyncController.cs` | Sync dead-letter list now includes the dead-letter ID. |
| POST `/api/admin/sync/dead-letters/{deadLetterId}/redrive` | Admin | `AdminSyncController.cs` | Redrive by dead-letter ID; sync identity is scoped per user/device. |
| `/api/sync/*` | Auth | `SyncEndpoints.cs` | Reject payload/auth user mismatch. |

`QuestionEndpoints.MapQuestionEndpoints` remains defined but unwired; decision remains BACKEND-TEST-027.

---

## Maintenance

| Method | Route | Auth | Owner | Notes |
|---|---|---|---|---|
| POST | `/api/maintenance/rebuild-indexes` | Admin (`UiTokensAdminPolicy`) | `MaintenanceEndpoints.cs` | Injected shared service, cancellation and in-process non-overlap. Distributed lock/audit remains BACKEND-TEST-042. |
| GET | `/api/maintenance/index-health` | Admin (`UiTokensAdminPolicy`) | `MaintenanceEndpoints.cs` | Read-only injected health query; positive admin contract tested. |
| GET | `/api/maintenance/index-stats` | Admin (`UiTokensAdminPolicy`) | `MaintenanceEndpoints.cs` | Read-only; no longer invokes rebuild or `ANALYZE`. Contract verifies zero rebuild calls. |

---

## Bug reports

| Method | Route | Auth | Owner | Notes |
|---|---|---|---|---|
| POST | `/api/bugs/report` | Auth | `BugEndpoints.cs` | Authenticated creation; input/storage hardening remains BACKEND-TEST-025. |
| GET | `/api/bugs/mine` | Auth | `BugEndpoints.cs` | Current-user rows only; page capped at 1,000. Invalid page size preserves default 50. |
| GET | `/api/bugs/` | Admin (`UiTokensAdminPolicy`) | `BugEndpoints.cs` | Global list; page capped at 1,000. Invalid page size preserves default 20. |
| GET | `/api/bugs/{id:guid}` | Admin (`UiTokensAdminPolicy`) | `BugEndpoints.cs` | Read any report. |
| PATCH | `/api/bugs/{id:guid}` | Admin (`UiTokensAdminPolicy`) | `BugEndpoints.cs` | Update status/assignee. |

Endpoint and service layers both normalize bug-report paging for defense-in-depth.

---

## Canonical mobile P0 mutation checklist

| Endpoint | Operation type / policy | Evidence area |
|---|---|---|
| `POST /api/quiz/answer` | `quiz_answer` | Quiz answer idempotency and mobile-contract tests. |
| `POST /api/quiz/srs/update` | `srs_update` | SRS idempotency and mobile-contract tests. |
| `POST /api/daily-run/chest/claim` | `daily_run_chest_claim`, Policy B | Daily Run idempotency tests. |
| `POST /api/seasons/daily-run-claim` | `season_daily_run_claim` | Economy contract/idempotency tests. |
| `POST /api/seasons/milestones/{milestoneId}/claim` | `season_milestone_claim` | Economy contract/idempotency tests. |
| `POST /api/cosmetics/fragments/grant` | `cosmetics_fragment_grant` | Cosmetics contract/idempotency tests. |
| `POST /api/cosmetics/items/{itemKey}/claim` | `cosmetics_item_claim` | Cosmetics contract/idempotency tests. |
| `POST /api/cosmetics/purchase` | `cosmetics_shop_purchase` | Cosmetics purchase/idempotency tests. |
| `POST /api/economy/coins/spend` | `economy_coins_spend` | Economy idempotency/contract tests. |
| `POST /api/economy/hints/use` | `economy_hint_use` | Economy idempotency/contract tests. |
| `POST /api/economy/rewards/claim` | `economy_reward_claim` | Economy idempotency/contract tests. |
| `POST /api/shop/streak-freeze/purchase` | `shop_streak_freeze_purchase` | Economy idempotency/contract tests. |
