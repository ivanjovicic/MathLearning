# Backend route compatibility audit

Last aligned: 2026-07-01  
Prompt: `BE-PERF-008`

## Scope

This audit classifies the current backend route surface into:

- canonical mobile routes
- legacy / compatibility aliases
- admin-only / internal support routes
- adjacent / future surfaces that should stay isolated from the mobile contract until they have evidence

It does **not** remove routes. It only documents ownership, auth behavior, evidence, and the safest deprecation direction.

## Canonical mobile routes

| Route family | Owner file | Auth | Mobile caller / evidence | Deprecation plan |
|---|---|---|---|---|
| `/auth/login` | `AuthEndpoints.cs` | Public | `AuthDevSeedLoginTests.cs`; auth contract coverage | Keep canonical. Freeze `/api/auth/login` as the compatibility alias. |
| `/api/quiz/start` | `QuizEndpoints.cs` | Auth | `QuizStartContractIntegrationTests.cs` | Keep canonical. |
| `/api/quiz/answer` | `QuizEndpoints.cs` | Auth | `QuizAnswerIdempotencyTests.cs`, `MobileMutationContractIntegrationTests.cs` | Keep canonical. |
| `/api/quiz/offline-submit` | `QuizEndpoints.cs` | Auth | `OfflineBatchSubmitCompatibilityTests.cs`, `MobileMutationContractIntegrationTests.cs` | Keep canonical. Freeze `/api/quiz/batch-submit` as the thin adapter. |
| `/api/quiz/srs/update` | `QuizEndpoints.cs` | Auth | `SrsUpdateIdempotencyTests.cs`, `MobileMutationContractIntegrationTests.cs` | Keep canonical. |
| `/api/progress/overview` | `ProgressEndpoints.cs` | Auth | mobile progress integration coverage | Keep canonical. |
| `/api/progress/topics` | `ProgressEndpoints.cs` | Auth | `MobileApiRouteContractTests.cs` | Keep canonical. Freeze `/api/topics/progress` as the legacy alias. |
| `/api/economy/*` | `EconomySettlementEndpoints.cs` | Auth | `EconomyOperationIdIdempotencyTests.cs`, `MobileEconomyContractIntegrationTests.cs`, `MobileMutationContractIntegrationTests.cs` | Keep canonical. |
| `/api/shop/streak-freeze/purchase` | `EconomySettlementEndpoints.cs` | Auth | economy contract/idempotency tests | Keep canonical. |
| `/api/cosmetics/catalog|inventory|avatar|items/{itemKey}/claim|fragments/grant` | `CosmeticsEndpoints.cs` | Auth | `MobileCosmeticsContractIntegrationTests.cs`, `MobileCosmeticsApiIntegrationTests.cs` | Keep canonical. |
| `/api/daily-run/chest/claim` | `DailyRunEndpoints.cs` | Auth | `DailyRunChestClaimIdempotencyTests.cs`, `MobileMutationContractIntegrationTests.cs` | Keep canonical. |
| `/api/leaderboard/friends` | `LeaderboardEndpoints.cs` | Mixed/Auth | `LeaderboardEndpointsIntegrationTests.cs` | Keep canonical. Freeze `/api/leaderboard/rivals` as the compatibility alias. |
| `/api/leaderboard/global` | `LeaderboardEndpoints.cs` | Mixed/Auth | `LeaderboardEndpointsIntegrationTests.cs` | Keep canonical. |
| `/api/leaderboard/schools*` | `LeaderboardEndpoints.cs` | Mixed/Auth | `LeaderboardEndpointsIntegrationTests.cs` | Keep canonical. |
| `/api/leaderboard/student` | `LeaderboardEndpoints.cs` | Mixed/Auth | `StudentLeaderboardStringIdentityIntegrationTests.cs`, `LeaderboardCursorCodecTests.cs` | Keep canonical. String-safe v2 cursor is bound to normalized scope/period and bad cursors return `400`. |
| `/api/users/profile` | `UserEndpoints.cs` | Auth | user/profile contract tests | Keep canonical mobile profile bootstrap. |
| `/api/cosmetics/avatar` | `CosmeticsEndpoints.cs` | Auth | `MobileCompatibilityEndpointsIntegrationTests.cs`, `MobileCosmetics*` tests | Keep canonical avatar surface. |

## Legacy / compatibility aliases

| Route family | Owner file | Auth | Evidence | Deprecation plan |
|---|---|---|---|---|
| `/api/auth/login` | `AuthEndpoints.cs` | Public | `AuthDevSeedLoginTests.cs` | Freeze; prefer `/auth/login`. |
| `GET`/`POST /api/quiz/questions` | `QuizEndpoints.cs` | Auth | `QuizStartContractIntegrationTests.cs` | Freeze; do not expand. Prefer `/api/quiz/start` for new mobile work. |
| `/api/quiz/batch-submit` | `QuizEndpoints.cs` | Auth | `OfflineBatchSubmitCompatibilityTests.cs` | Freeze; prefer `/api/quiz/offline-submit`. |
| `/api/user/profile/{userId}` | `UserEndpoints.cs` | Auth | `MobileCompatibilityEndpointsIntegrationTests.cs` | Freeze; keep only until all consumers are off the alias. |
| `/api/users/{userId}/profile` | `UserEndpoints.cs` | Auth | `MobileCompatibilityEndpointsIntegrationTests.cs` | Freeze; alias-equivalent to `/api/user/profile/{userId}`. |
| `/api/user/daily-hints` | `UserEndpoints.cs` | Auth | `MobileApiRouteContractTests.cs` | Legacy read alias; do not expand. |
| `/api/user/hints/daily` | `UserEndpoints.cs` | Auth | `MobileApiRouteContractTests.cs` | Legacy read alias; do not expand. |
| `/api/topics/progress` | `ProgressEndpoints.cs` | Auth | inventory + route compatibility policy | Freeze; prefer `/api/progress/topics`. |
| `/api/profile/{userId}/appearance` | `CosmeticsEndpoints.cs` / `AvatarEndpoints.cs` | Auth | `MobileCompatibilityEndpointsIntegrationTests.cs` | Freeze; prefer `/api/cosmetics/avatar/{userId}`. |
| `/api/avatar/me` | `AvatarEndpoints.cs` | Auth | `MobileCompatibilityEndpointsIntegrationTests.cs` | Keep temporarily as legacy read shape only. |
| `/api/leaderboard/rivals` | `LeaderboardEndpoints.cs` | Mixed/Auth | `LeaderboardEndpointsIntegrationTests.cs`, `MobileApiRouteContractTests.cs` | Freeze; prefer `/api/leaderboard/friends`. |
| `/api/coins/*` | `CoinEndpoints.cs` | Auth | legacy backend coverage only | Freeze; new mobile settlement work must use `/api/economy/*`. |
| `/users/{id}/avatar` | `UserEndpoints.cs` | Auth | inventory and avatar upload caveat | Legacy/profile upload caveat; do not expand. |

### Duplicate backend work pairs

These routes either hit the same handler or exist only as compatibility aliases. Keeping them separate is intentional, but they should not be expanded without a deprecation plan:

- `/auth/login` and `/api/auth/login`
- `/api/quiz/offline-submit` and `/api/quiz/batch-submit`
- `/api/leaderboard/friends` and `/api/leaderboard/rivals`
- `/api/user/profile/{userId}` and `/api/users/{userId}/profile`
- `/api/profile/{userId}/appearance` and `/api/cosmetics/avatar/{userId}`
- `/api/user/daily-hints` and `/api/user/hints/daily`
- `/api/progress/topics` and `/api/topics/progress`
- `/api/health/background-jobs` and `/health/background-jobs`
- `/api/health/schema` and `/health/schema`

## Admin-only and internal support routes

| Route family | Owner file | Auth | Evidence | Notes |
|---|---|---|---|---|
| `/api/admin/economy/rewards/grant` | `EconomySettlementEndpoints.cs` | Admin | `EconomySettlementEndpointsIntegrationTests.cs` | Actor from auth; target user from request. |
| `/api/leaderboard/admin/add-xp/{userId}` | `LeaderboardEndpoints.cs` | Admin | leaderboard endpoint coverage is limited | Keep admin-only. Add explicit audit tests only if the route becomes release-critical. |
| `/api/leaderboard/admin/reset-xp/{userId}` | `LeaderboardEndpoints.cs` | Admin | leaderboard endpoint coverage is limited | Keep admin-only. |
| `/api/idempotency/observability/*` | `IdempotencyObservabilityEndpoints.cs` | Admin | `IdempotencyObservabilityEndpointsTests.cs` | Safe observability surface only. |
| `/api/monitoring/jobs` | `Program.cs` | Public/internal | smoke/manual support | Support-only payload. |
| `/api/monitoring/logs` | `Program.cs` | Public/internal | smoke/manual support | Support-only log reader. |
| `/api/monitoring/logs-advanced` | `Program.cs` | Public/internal | smoke/manual support | Support-only filtered log reader. |
| `/api/maintenance*` | `MaintenanceEndpoints.cs` | Admin/internal | maintenance coverage is route-specific | Keep isolated from mobile. |
| `/api/logs/*` | `LoggingEndpoints.cs` | Admin/internal | logging/admin support | Admin logging support surface. |
| `/api/health/*` | `HealthEndpoints.cs` / `Program.cs` | Public | health/readiness tests and smoke steps | Support / runtime evidence, not mobile contract. |
| `/metrics` | `Program.cs` | Public/internal | startup/observability smoke | Runtime metrics only. |
| `/api/bug/*` | `BugEndpoints.cs` | Mixed | bug/support route coverage is limited | Support surface; keep separate from mobile-critical flows. |

## Adjacent / future surfaces

These routes are live, but they are not part of the canonical mobile settlement surface and should stay isolated until they have stronger evidence:

- `/api/adaptive/*` in `AdaptiveEndpoints.cs`
- `/api/analytics/*` in `AnalyticsEndpoints.cs`
- `/api/explanations/*` in `ExplanationEndpoints.cs`
- `/api/adaptive/session/start` and `/api/adaptive/session/answer` remain POST-only and should not be treated as mobile contract routes

Evidence:

- `MobileApiRouteContractTests.cs` locks unsupported mobile routes as absent or POST-only.
- `MobileCompatibilityEndpointsIntegrationTests.cs` locks the intended aliases (`/api/adaptive/review`, `/api/users/{userId}/profile`, `/api/profile/{userId}/appearance`, `/api/avatar/me` shape difference).

## Follow-up prompts

Keep these as follow-up-only items; do not remove routes in this audit:

1. Add a consumer-evidence pass for the remaining compatibility aliases that still exist only for older mobile clients.
2. Open a dedicated removal prompt only after log / repo evidence shows no callers for a legacy alias.
3. Re-audit the adjacent `/api/adaptive/*`, `/api/analytics/*`, and `/api/explanations/*` surfaces if they become mobile-critical.

## Related evidence

- `docs/API_ENDPOINT_INVENTORY.md`
- `docs/backend_contract_gap_report.md`
- `tests/MathLearning.Tests/Endpoints/MobileApiRouteContractTests.cs`
- `tests/MathLearning.Tests/Endpoints/MobileCompatibilityEndpointsIntegrationTests.cs`
- `tests/MathLearning.Tests/Endpoints/LeaderboardEndpointsIntegrationTests.cs`
- `tests/MathLearning.Tests/Endpoints/AuthDevSeedLoginTests.cs`
- `tests/MathLearning.Tests/Endpoints/OfflineBatchSubmitCompatibilityTests.cs`

