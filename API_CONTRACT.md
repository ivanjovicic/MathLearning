# MathLearning API Contract (auto-generated)

Generated: 2026-05-04

Purpose: concise list of backend endpoints and request/response contract hints for frontend (Flutter) verification.

Notes:
- `Auth`: "Required" means the endpoint expects an authenticated user (JWT in Authorization header "Bearer <token>"). "Anonymous" means public.
- Path parameters are shown as `{name:type}`. Query parameters and default values are shown where present.
- `Body` indicates the expected request DTO (when present) or primitive/query binding.
- `Response` is a short summary or DTO name where available. When anonymous objects are returned, key fields are listed.

---

## Adaptive (/api/adaptive)
Auth: Required

- POST /api/adaptive/session/start
  - Auth: Required
  - Body: none
  - Response: ApiResult / start session payload (session id, metadata)

- POST /api/adaptive/session/answer
  - Auth: Required
  - Body: `AdaptiveAnswerRequest`
  - Response: ApiResult (answer result, score/xp info)

- GET /api/adaptive/path
  - Auth: Required
  - Response: ApiResult (adaptive path / lesson sequence)

- GET /api/adaptive/recommendations
  - Auth: Required
  - Query: `page` (int, default 1), `pageSize` (int, default 5)
  - Response: ApiResult (recommendations list)

- GET /api/adaptive/reviews/due
  - Auth: Required
  - Response: ApiResult (due reviews list)

---

## Analytics (/api/analytics, /api/recommendations)
Auth: Required

- GET /api/analytics/weakness?page=1&pageSize=5
  - Response: 200 OK { weakTopics: [{ topicId, topicName, accuracy, weaknessLevel, confidence }], page, pageSize, returned }

- GET /api/analytics/weakness/details?page=1&pageSize=10
  - Response: { weakTopics: [...], weakSubtopics: [...], page, pageSize, returnedTopics, returnedSubtopics }

- GET /api/recommendations/practice?page=1&pageSize=10
  - Response: `PracticeRecommendationsResponse` (Recommendations list, Page/PageSize, Returned)

---

## Auth (/auth and /api/auth)
Auth: Mixed (login/refresh/register are anonymous; revoke-all requires auth)

- POST /auth/mobile/register
  - Auth: Anonymous
  - Body: `MobileRegisterRequest` (username, email, password, displayName?)
  - Response: `MobileRegisterResponse` { Success, Message, Tokens: TokenResponse, Profile: UserProfileDto }

- POST /auth/login  (alias: POST /api/auth/login)
  - Auth: Anonymous
  - Body: `LoginRequest` (Username, Password)
  - Response: `TokenResponse` { AccessToken, RefreshToken, ExpiresIn, UserId, Username }
  - Errors: 401 for invalid credentials

- POST /auth/refresh
  - Auth: Anonymous
  - Body: `TokenRequest` { RefreshToken }
  - Response: `TokenResponse`

- POST /auth/logout
  - Auth: (expects user context) - Body: `RevokeTokenRequest` { RefreshToken }
  - Response: 200 OK { message }

- POST /auth/revoke-all
  - Auth: Required
  - Response: 200 OK { message, revokedCount }

- POST /auth/register
  - Auth: Anonymous (admin-style registration)
  - Body: `RegisterRequest`
  - Response: `TokenResponse`

- GET /auth/test
  - Auth: Anonymous
  - Response: simple health JSON

---

## Avatar & Cosmetics (/api/cosmetics and aliases)
Auth: Mixed (most endpoints require auth; some read endpoints AllowAnonymous)

- GET /api/cosmetics/items?category=&rarity=&seasonId=
  - Auth: Required
  - Response: catalog array

- GET /api/cosmetics/inventory?category=
  - Auth: Required
  - Response: inventory array

- GET /api/cosmetics/avatar
  - Auth: Required
  - Response: user's avatar config

- PUT /api/cosmetics/avatar
  - Auth: Required
  - Body: `UpdateAvatarConfigRequest`
  - Response: updated avatar

- POST /api/cosmetics/equip
  - Auth: Required
  - Body: `EquipCosmeticRequest`
  - Response: Ok / updated avatar state

- POST /api/cosmetics/equip-batch
  - Auth: Required
  - Body: `EquipCosmeticBatchRequest`

- POST /api/cosmetics/unequip
  - Auth: Required
  - Body: `EquipCosmeticRequest` (CosmeticItemId=null)

- POST /api/cosmetics/purchase
  - Auth: Required
  - Body: `PurchaseCosmeticRequest`

- GET /api/cosmetics/seasons?activeOnly=
  - Auth: Anonymous
  - Response: seasons list

- GET /api/cosmetics/reward-track?seasonId=&trackType=
  - Auth: Required
  - Response: reward track object or 404

- POST /api/cosmetics/reward-track/claim
  - Auth: Required
  - Body: `ClaimRewardTrackTierRequest`

- GET /api/cosmetics/avatar/{userId}
  - Auth: Anonymous
  - Response: public appearance for given user

- GET /api/avatar/me
  - Auth: Required
  - Response: current user's avatar

- GET /api/profile/{userId}/appearance
  - Auth: Anonymous
  - Response: public appearance

---

## Bugs (/api/bugs)
Auth: Mixed (reporting is allowed but handler checks for userId)

- POST /api/bugs/report
  - Auth: logically requires user (handler checks userId)
  - Body: `BugReportRequest`
  - Response: 201 Created { id, ... }

- GET /api/bugs/mine
  - Auth: Requires Authorization
  - Query: page, pageSize
  - Response: paginated bug reports

- GET /api/bugs
  - Auth: Admin (RequireAuthorization on adminGroup)
  - Query: page, pageSize, status?, severity?

- GET /api/bugs/{id:guid}
  - Auth: Admin

- PATCH /api/bugs/{id:guid}
  - Auth: Admin
  - Body: `UpdateBugStatusRequest`

---

## Coins (/api/coins)
Auth: Required

- GET /api/coins/balance
  - Auth: Required
  - Response: { coins, totalEarned, totalSpent, level, xp, streak }

- POST /api/coins/earn?amount={int}&reason={string}
  - Auth: Required
  - Body: none (amount is primitive bound from query/form)
  - Response: { message, reason, newBalance, totalEarned }

- POST /api/coins/spend?amount={int}&reason={string}
  - Auth: Required
  - Response: Ok or 402 Insufficient coins

- GET /api/coins/history
  - Auth: Required
  - Response: transaction-like list (hint spends etc.)

- GET /api/coins/leaderboard?limit=10
  - Auth: Required
  - Response: leaderboard list

---

## Explanations (/api/explanations)
Auth: Required

- GET /api/explanations/problem/{problemId:int}?lang=
  - Response: structured explanation steps DTO

- POST /api/explanations/generate
  - Body: `GenerateExplanationRequest`
  - Response: generated explanation DTO

- POST /api/explanations/mistake-analysis
  - Body: `MistakeAnalysisRequest`
  - Response: analysis DTO

---

## Health (/api/health)
Auth: Anonymous

- GET /api/health/
  - Response: { status, timestamp }

- GET /api/health/db
  - Response: detailed DB connectivity + schema summary

- GET /api/health/ready
  - Response: readiness including schema and counts

---

## Hints (legacy under /api/questions and new under /api/hints)
Auth: Required

- GET /api/questions/{id:int}/hint/formula
  - Response: { formula }

- GET /api/questions/{id:int}/hint/clue
  - Response: { clue }

- POST /api/questions/{id:int}/hint/eliminate
  - Response: { remainingOptions, eliminatedOption }

- GET /api/hints/questions/{id}/formula
  - Same as above but charges coins and records usage
  - Response includes { formula, available, cost, remainingCoins }

- GET /api/hints/questions/{id}/clue
- POST /api/hints/questions/{id}/eliminate
- GET /api/hints/questions/{id}/solution
- GET /api/hints/coins  (GetUserCoins) => returns user's coin summary
- GET /api/hints/stats (hint stats)
- GET /api/hints/history
- GET /api/hints/question/{questionId} (hints summary per question)

---

## Leaderboard (/api/leaderboard)
Auth: Required

- GET /api/leaderboard?scope=global&period=all_time&limit=50&cursor=
  - Response: `LeaderboardResponseDto` (Items, Me, NextCursor)

- GET /api/leaderboard/schools?period=week&limit=50&cursor=
- GET /api/leaderboard/schools/{schoolId:int}
- GET /api/leaderboard/global
- GET /api/leaderboard/friends
- GET /api/leaderboard/student
- POST /api/leaderboard/admin/add-xp/{userId} (Admin)
- POST /api/leaderboard/admin/reset-xp/{userId} (Admin)

---

## Logging (/api/logs)
Auth: RequireAuthorization (admin intended)

- GET /api/logs/recent?level=&limit=
- GET /api/logs/level/{level}
- GET /api/logs/search?query=&from=&to=&level=&limit=
- GET /api/logs/stats
- DELETE /api/logs/cleanup?daysToKeep={30}
- GET /api/logs/{id}
- GET /api/logs/errors/recent
- GET /api/logs/distribution

---

## Maintenance (/api/maintenance)
Auth: RequireAuthorization (admin intended)

- POST /api/maintenance/rebuild-indexes
- GET /api/maintenance/index-health
- GET /api/maintenance/index-stats

---

## Powerups (/api/powerups)
Auth: Required

- POST /api/powerups/streak-freeze/buy
  - Response: { coins, streakFreezeCount, cost, max }

---

## Practice Session (/api/practice/session)
Auth: Required

- POST /api/practice/session/start
  - Body: `StartPracticeSessionRequest`
  - Response: `StartPracticeSessionResponse`

- POST /api/practice/session/{sessionId:guid}/answer
  - Body: `SubmitPracticeAnswerRequest`
  - Response: `SubmitPracticeAnswerResponse`

- POST /api/practice/session/{sessionId:guid}/complete
  - Response: `CompletePracticeSessionResponse`

---

## Progress (/api/progress)
Auth: Required

- GET /api/progress/overview
  - Response: `ProgressOverviewDto` (totalAttempts, accuracy, streak, etc.)

- GET /api/progress/weak-areas
  - Response: list of `WeakAreaDto`

- GET /api/progress/topics
  - Response: topics progress

- POST /api/progress/sync  (JSON body)
  - Body: free-form JSON { completed: bool, day: "YYYY-MM-DD" }
  - Response: { success, syncedAt }

---

## Question Authoring (/api/questions - authoring)
Auth: Required (authoring)

- POST /api/questions/validate
  - Body: `QuestionAuthoringRequest`
  - Response: validation result

- POST /api/questions/preview
  - Body: `QuestionAuthoringRequest`

- POST /api/questions/save-draft
  - Body: `SaveQuestionDraftRequest`

- POST /api/questions/publish
  - Body: `PublishQuestionRequest`
  - Response: published flag

- GET /api/questions/{id:int}/versions
- GET /api/questions/{id:int}/validation
- POST /api/questions/{id:int}/revalidate

---

## Questions (/api/questions)
Auth: Required

- GET /api/questions?lang=&subtopicId=&limit=20
  - Response: list of `QuestionDto` (id, type, text, options[], correctAnswerId, difficulty, hints, explanation)

- GET /api/questions/{id}
  - Response: `QuestionDto`

---

## Quiz (/api/quiz)
Auth: Required

- POST /api/quiz/start
  - Body: `StartQuizRequest` (SubtopicId, QuestionCount)
  - Response: `QuizResponse` { quizId, questions[] }

- GET /api/quiz/questions?topic=&subtopicId=&count=10
- POST /api/quiz/questions (payload)
- POST /api/quiz/next-question (Body: `NextQuestionRequest`)
- POST /api/quiz/answer (JSON body) - expects fields: questionId, answer, timeSpentSeconds, hintUsed, quizId/sessionId, clientId
  - Response: `SubmitAnswerResponse` { isCorrect, explanation?, steps?, awardedXp, totalXpAfterAward }

- POST /api/quiz/offline-submit (Body: `OfflineBatchSubmitRequest`)
- POST /api/quiz/batch-submit (legacy JSON payload)
- POST /api/quiz/srs/update (Body: `SrsUpdateDto`)
- GET /api/quiz/srs/daily
- GET /api/quiz/srs/mixed
- GET /api/quiz/srs/streak

---

## Sync (/api - sync group)
Auth: Required

- POST /api/devices/register
  - Body: `RegisterSyncDeviceRequest`
  - Response: device registration info

- POST /api/sync
  - Body: `SyncRequestDto` (full sync payload)
  - Response: `SyncResponseDto`

- GET /api/offline/bundle?subtopicId=&questionCount=100
  - Response: offline bundle for device

- GET /api/offline/bundle/manifest?subtopicId=&questionCount=100
  - Response: bundle manifest

- GET /api/sync/checkpoint?deviceId={string}
  - Response: device sync state

- GET /api/sync/metrics (admin)

---

## Users (/api/users, /api/user, /users)
Auth: Required

- GET /api/user/profile/{userId}
  - Admin-style information (XP/level/streak)

- GET /api/user/coins
  - Legacy alias -> current user's coins

- GET /api/user/daily-hints and /api/user/hints/daily
  - Response: { usedToday, dailyLimit, remaining }

- GET /api/users/profile
  - Response: `UserProfileDto` (UserId, Username, DisplayName, Coins, Level, Xp, Streak, AvatarUrl, Appearance)

- PUT /api/users/profile
  - Body: `UpdateProfileRequest` { DisplayName }
  - Response: updated `UserProfileDto`

- GET /api/users/stats
  - Response: profile + stats object

- GET /api/users/search?query=&limit=10
  - Response: list of users with appearance map

- GET /users/{id:int}/settings
  - Auth: required; `id` must match userId
  - Response: `UserSettingsDto`

- PATCH /users/{id:int}/settings
  - Body: `UpdateUserSettingsRequest`
  - Response: updated `UserSettingsDto`

- POST /users/{id:int}/avatar (multipart/form-data file)
  - Upload avatar file; returns { avatarUrl }

- GET /users/{id:int}/avatar/{fileName}
  - Returns file stream (authorized check that id matches user)

---

## Misc / Program-level
- GET /metrics
- GET /api/monitoring/jobs
- GET /api/monitoring/logs
- GET /api/monitoring/logs-advanced?search=&level=
- GET /

---

If you want, I can:
- generate example JSON request/response samples for selected endpoints (useful for Flutter), or
- produce an OpenAPI/Swagger-style spec (partial) from these maps,
- or commit & push this file (I will now commit and push it if you want).


---

End of contract file.
