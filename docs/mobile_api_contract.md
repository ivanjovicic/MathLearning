# Mobile API Contract

Backend contract for the MathLearning Flutter runtime. Economy settlement mutations are documented in [mobile_economy_api_contract.md](./mobile_economy_api_contract.md).

## Authentication

Auth tokens are issued by the backend. Access JWTs include the current `userId` and `security_stamp` claims, and the backend validates them against the current Identity user state on each authenticated request.

Login failures use `{ code, message, correlationId?, retryAfterSeconds? }`. Unknown usernames and wrong passwords both return `401 invalid_credentials`; incomplete accounts return `403 account_incomplete`; throttling returns `429 login_rate_limited` with `Retry-After`.

`POST /auth/password/forgot` accepts `{ email }` and always returns `202 password_reset_requested` with a generic message, regardless of account existence. `POST /auth/password/reset` accepts `{ email, token, newPassword }`; success returns `password_reset_success`, while invalid tokens return `password_reset_invalid`. Password reset tokens are delivered out-of-band and never returned by the API.

`POST /auth/refresh` is anonymous and rotates a single-use refresh token. Invalid, expired, revoked, replayed, missing-user, or security-stamp-mismatch requests return `401 refresh_invalid` with a generic message and correlation id. Refresh throttling returns `429 refresh_rate_limited`, includes `Retry-After`, and does not disclose token or account state.

`POST /auth/logout` is anonymous and idempotent. It returns `204 No Content` for an active, revoked, or unknown refresh token; unknown-token responses are intentionally indistinguishable from successful logout. Unexpected server failures use the standard safe error response and are correlated server-side.

For relational providers, reset commits the Identity password/security-stamp update and active refresh-token revocation in one scoped transaction. Identity 8.0.12's `ResetPasswordAsync` is the authoritative security-stamp rotation path; the backend does not perform a second stamp rotation. Production forgot requests enqueue only the stable Identity user id; the Hangfire job resolves the current account, generates the reset token, and constructs the delivery URL at execution time. Test and Development use the same job owner through deterministic inline delivery.

Production delivery is disabled by default and must be enabled explicitly with `PasswordReset__Delivery__Enabled=true`, a real `PasswordReset__Delivery__SmtpHost`, `PasswordReset__Delivery__FromAddress`, and secret-backed SMTP credentials as needed. `PasswordReset__Delivery__ResetBaseUrl` must be the deployed Flutter deep-link target; no localhost or provider credentials are committed.

### `POST /auth/revoke-all`

Auth: Required.

Logout from all devices for the current user. The server revokes all active refresh tokens for that user and rotates the Identity security stamp, which immediately invalidates already-issued access tokens on the next authenticated request.

If a refresh token row is still present but its stored security stamp no longer matches the current user stamp, `POST /auth/refresh` returns `401`.

Response:
```json
{
  "message": "Revoked 2 tokens",
  "revokedCount": 2
}
```

## Cosmetics

Auth: Required for all endpoints below.

Reads do not require `idempotencyKey`. Mutations use per-user idempotency scoped to the authenticated `userId` (bearer token), not globally.

### Cosmetics mutation idempotency

Applies to `POST /api/cosmetics/items/{itemKey}/claim` and `POST /api/cosmetics/fragments/grant`.

Request body must include both `operationId` and `idempotencyKey` (they may be identical). Daily Run fragment grants may send only `transactionId`; the server accepts it as both keys.

Ledger entries are stored per `userId + transactionType + operationId` and `userId + transactionType + idempotencyKey`.

| Scenario | Response |
|----------|----------|
| First successful settlement | `200` with `success: true` |
| Retry, same keys, equivalent payload | `200` with `alreadyProcessed: true` (claim) or `alreadyClaimed: true` (item claim) |
| Same keys, different payload | `409` with `alreadyProcessed: false`, `conflict: true`, `errorCode: "idempotency_conflict"` |
| Transient `5xx` | Client retries with the same keys |

`POST /api/cosmetics/fragments/grant` always returns authoritative fragment progress:

```json
{
  "success": true,
  "alreadyProcessed": false,
  "progress": {
    "itemId": "frame_comet",
    "collectedFragments": 3,
    "requiredFragments": 5,
    "updatedAt": "2026-06-24T12:00:00Z",
    "unlockedAt": null
  },
  "unlockedItemId": null,
  "inventory": ["skin_default"],
  "fragmentProgress": { "Comet Frame Fragment": 3 }
}
```

When the fragment threshold is reached, the response also includes `unlockedItemId` and optional `unlockedInventory`.

Do **not** use legacy `POST /api/cosmetics/unlock` or `POST /api/cosmetics/fragments/daily-run`.

Readiness truthfulness: `GET /api/health/ready` returns `503` when the cosmetic catalog revision, required defaults, or required fragment labels are missing or invalid.

### `GET /api/cosmetics/catalog`

Published cosmetic metadata for active, non-hidden items. Payload is identical for every authenticated user and is safe to cache.

Query params (optional):
- `category`
- `rarity`
- `seasonId`

Response:
```json
{
  "catalogVersion": "catalog-20260716-019",
  "items": [
    {
      "key": "frame_comet",
      "name": "Comet Frame",
      "category": "frame",
      "rarity": "rare",
      "assetPath": "cosmetics/frames/comet",
      "previewAssetPath": null,
      "unlockType": "reward",
      "unlockCondition": null,
      "unlockConditionJson": null,
      "coinPrice": null,
      "seasonId": null,
      "isDefault": false,
      "isActive": true,
      "isHidden": false,
      "assetVersion": "1"
    }
  ]
}
```

Response headers:
- `ETag`: quoted `catalogVersion`
- `Cache-Control`: `private, max-age=300`

`If-None-Match` matching the current `ETag` returns `304 Not Modified`.

`catalogVersion` is the applied cosmetic catalog revision key. It changes when a new manifest revision is imported and stays stable across no-op restarts or no-op reapplications of the same revision.

### `GET /api/cosmetics/inventory`

Current user's unlocked cosmetic keys and fragment progress.

Response:
```json
{
  "itemKeys": ["skin_default", "frame_comet"],
  "fragmentProgress": {
    "Comet Frame Fragment": 2
  }
}
```

### `GET /api/cosmetics/avatar`

Current user's equipped avatar slots.

Response:
```json
{
  "slots": {
    "skin": "skin_default",
    "hair": null,
    "clothing": null,
    "accessory": null,
    "emoji": null,
    "frame": "frame_comet",
    "background": null,
    "effect": null,
    "leaderboardDecoration": null
  },
  "version": 3
}
```

### `PUT /api/cosmetics/avatar`

Persist equipped slots. Server validates category match and item ownership for every non-null slot key.

Request:
```json
{
  "slots": {
    "frame": "frame_comet",
    "effect": null
  }
}
```

`slots` is a map of slot key to cosmetic `key` or `null`.

Rules:
- Slot keys: `skin`, `hair`, `clothing`, `accessory`, `emoji`, `frame`, `background`, `effect`, `leaderboardDecoration`
- Keys omitted from `slots` are left unchanged
- Explicit `null` clears a slot
- Non-null values must reference an owned catalog `key`

Business failures:
- Unknown slot or item key: `400`
- Unowned item for slot: `403`

Response: same shape as `GET /api/cosmetics/avatar`

Business failures return `400` with `{ "error": "message" }`.

### `POST /api/cosmetics/items/{itemKey}/claim`

See [mobile_economy_api_contract.md](./mobile_economy_api_contract.md#7-post-apicosmeticsitemsitemkeyclaim).

This route now consumes a server-issued `entitlementId`; arbitrary client-declared reward sources are rejected.

Response includes refreshed `inventory` (string item keys) and `fragmentProgress`.

## Quiz and practice

### `POST /api/quiz/start`

The canonical classic quiz start request uses numeric `subtopicId` and
`questionCount`. The response is raw `{ quizId, questions }`; questions are
pre-answer safe and omit the correct answer identifier. Selection is restricted
to published, non-deleted questions with meaningful text, at least two non-empty
options, and exactly one correct option. No playable content returns `404` with
`errorCode: "NO_PLAYABLE_QUESTIONS"`.

For mobile compatibility, when `subtopicId` matches a `Topic.Id` but not a
`Subtopic.Id`, the backend resolves it across all subtopics in that topic (same
behavior as legacy `topic_<id>`). Prefer the explicit subtopic id from
`GET /api/progress/topics/{topicId}/subtopics`.

The legacy `/api/quiz/questions` route accepts explicit `subtopicId` or the
confirmed `topic_<numericId>` compatibility key. A localized skill title is
never a content identity.

All online pre-answer question routes use the same safe question shape:
`POST /api/quiz/start`, `GET|POST /api/quiz/questions`,
`POST /api/quiz/next-question`, `GET /api/quiz/srs/daily` and
`GET /api/quiz/srs/mixed` omit `correctAnswerId`, option correctness flags,
`hintFull`, `explanation` and worked `steps` entirely. `hintLight` and
`hintMedium` are progressive hints only; authoritative explanation/steps are
returned by `POST /api/quiz/answer` after server settlement for an incorrect
answer, preserving the existing feedback rule.

`POST /api/quiz/answer` requires a valid GUID in `quizId` (the legacy
`sessionId` alias is accepted during the compatibility window). Missing or
malformed session ids return `400 QUIZ_SESSION_ID_REQUIRED`; the server never
creates a replacement session during answer settlement. The session must be
owned by the authenticated user and the question must be present in the
persisted `IssuedQuestionIdsJson` membership captured at quiz start. Unknown,
foreign, expired (24 hours after start), completed, or non-issued sessions
return the stable `404 QUIZ_SESSION_NOT_FOUND` contract without reward or
analytics writes. A classic session permits repeated attempts while it remains
active; after every issued question has one settled answer, the session is
treated as completed. Idempotent requests replay the original settled body
before the completed-session check.

### `GET /api/progress/topics` and `GET /api/progress/topics/{topicId}/subtopics`

Topic progress now includes:
- `playableQuestionCount`
- `canStartQuiz` (`unlocked && playableQuestionCount > 0`)

Subtopic progress now includes:
- `unlocked` (topic gate)
- `canStartQuiz` (`unlocked && playableQuestionCount > 0`)

### `POST /api/progress/sync`

Progress completion is server-derived. The request must include a registered
active `deviceId`, stable `operationId`/`idempotencyKey`, and at least one
settled `quizOperationIds` or completed `practiceSessionIds` reference owned by
the authenticated user and device. The server resolves the UTC calendar day
from the settled evidence, rejects future dates and dates outside the configured
offline window, and updates daily progress/rewards idempotently. A legacy body
containing only `completed` and `day` receives `426` with
`errorCode: "progress_sync_legacy_client"` and `requiredVersion:
"progress-sync-v2"`; it cannot create a daily completion. Replaying the same
identity returns the stored response, while the same identity with different
evidence returns `409 idempotency_conflict`.

Use the subtopics route to resolve the numeric `subtopicId` for
`POST /api/quiz/start`. Avoid relying on topic ids as `subtopicId`; the backend
only accepts that shape as a compatibility fallback.

Legacy aliases: `/api/topics/progress` and `/api/topics/{topicId}/subtopics`.

### `POST /api/practice/session/start`

The response is an `ApiResult` envelope. Its `data.question.options` values are
objects with stable `id` and user-facing `text`; clients render the text and
must not stringify the option object.

## Adaptive

Auth: Required.

### `GET /api/adaptive/path`

Returns the Learning Map for the authenticated user. The successful response is the map object itself (there is no `ApiResult.data` envelope). Nodes are emitted only when the backend has user learning data; the backend never fabricates a catalog-only path for a new user.

Successful response with progress:
```json
{
  "nodes": [
    {
      "id": "topic-7-subtopic-9",
      "title": "Linear equations",
      "topicName": "Algebra",
      "topicId": 7,
      "subtopicId": 9,
      "mastery": 0.72,
      "isLocked": false,
      "recommendedDifficulty": "Medium"
    }
  ],
  "edges": [],
  "recommendedNext": "topic-7-subtopic-9",
  "generatedAt": "2026-09-10T10:00:00Z"
}
```

Successful response for a new user or a user without enough data:
```json
{
  "nodes": [],
  "edges": [],
  "recommendedNext": null,
  "generatedAt": "2026-09-10T10:00:00Z",
  "emptyReason": "not_enough_learning_data"
}
```

`401` is returned when the request has no authenticated user. Origin/service failures return `500` with the standard safe error object; internal exception messages are not exposed.

### `POST /api/adaptive/session/start`

Starts an adaptive practice session for the current authenticated user.

Request body may include:
- `topicId`
- `topic`
- `operationId`
- `idempotencyKey`

Replay-safe start behavior:
- When both `operationId` and `idempotencyKey` are present, the backend uses the shared idempotency ledger and returns the same `AdaptiveSessionDto` snapshot on retry.
- When the same keys are reused with a different normalized payload, the backend returns `409` with `errorCode: "idempotency_conflict"`.
- Different users never replay each other's sessions.
- Legacy requests without operation identity are still accepted, but they are explicitly non-retryable.

Response is the raw `AdaptiveSessionDto` JSON:
```json
{
  "adaptiveSessionId": "11111111-1111-1111-1111-111111111111",
  "createdAtUtc": "2026-07-22T12:00:00Z",
  "expiresAtUtc": "2026-07-22T12:35:00Z",
  "profileDifficulty": "Medium",
  "items": [
    {
      "adaptiveSessionItemId": "22222222-2222-2222-2222-222222222222",
      "questionId": 1,
      "topicId": 101,
      "subtopicId": 1001,
      "sourceType": "adaptive",
      "difficultyLevel": "Medium",
      "sequence": 1
    }
  ]
}
```

### `GET /api/recommendations/practice` (canonical)

Returns the authenticated user's paginated practice recommendations. `GET /api/adaptive/recommendations` remains a compatibility alias with the same response shape.

Query params: `page` (default `1`) and `pageSize` (default `10`, maximum `100`).

Successful response:
```json
{
  "recommendations": [
    {
      "practiceId": "subtopic_9_practice",
      "topicId": 7,
      "topicName": "Algebra",
      "reason": "low_accuracy",
      "priorityScore": 0.86,
      "recommendedDifficulty": "Easy",
      "subtopicId": 9,
      "id": "subtopic_9_practice",
      "title": "Linear equations - targeted drill",
      "priority": 0.86
    }
  ],
  "page": 1,
  "pageSize": 10,
  "returned": 1
}
```

`practiceId`, `topicName`, `priorityScore` and `recommendedDifficulty` are the canonical names. `id`, `title` and `priority` are retained as compatibility aliases for older clients.

### `GET /api/analytics/mastery`

Returns a raw list for the authenticated user. An empty list is a successful `200` response; there is no synthetic mastery row.

```json
[
  {
    "topicId": 7,
    "topicName": "Algebra",
    "masteryProbability": 0.82
  }
]
```

Authentication/authorization is standard for the protected route: `401` when no identity is supplied and `403` when the host authorization policy denies an authenticated identity. Service or database failures are handled as a safe `500` response with `errorCode: "INTERNAL_ERROR"` and a trace id.

## Offline bundle

Auth: Required.

Offline bundle routes resolve the content language from `UserSettings.Language` first and then the request `Accept-Language` header. The response is localized before versioning, so the manifest revision tracks the serialized content actually sent to the app.
Only published, non-deleted questions are eligible for selection.

### `GET /api/offline/bundle`

Returns the full offline bundle payload for the current user.

Query params:
- `subtopicId`
- `questionCount`

Response shape:
```json
{
  "manifest": {
    "version": "content-rev-20260730-001",
    "snapshotVersion": "user-snapshot-20260730-001",
    "generatedAtUtc": "2026-07-30T12:00:00Z",
    "questionCount": 1,
    "topicCount": 1,
    "subtopicCount": 1
  },
  "questions": [
    {
      "id": 1,
      "type": "multiple_choice",
      "text": "Deutsche Frage",
      "difficulty": 2,
      "options": [
        {
          "id": 10,
          "text": "Deutsche Option",
          "textFormat": "PlainText",
          "renderMode": "Auto",
          "semanticsAltText": "Option semantics override"
        }
      ],
      "hintLight": "DE light",
      "hintMedium": "DE medium",
      "hintFull": "DE full",
      "explanation": "DE explanation",
      "textFormat": "MarkdownWithMath",
      "explanationFormat": "MarkdownWithMath",
      "hintFormat": "MarkdownWithMath",
      "textRenderMode": "Auto",
      "explanationRenderMode": "Auto",
      "hintRenderMode": "Auto",
      "semanticsAltText": "Question semantics override"
    }
  ],
  "topics": [],
  "subtopics": [],
  "quizSequence": [1],
  "userSnapshot": {
    "xp": 100,
    "level": 1,
    "streak": 0,
    "questionProgress": []
  }
}
```

Versioning rules:
- `manifest.version` changes when any serialized learning content changes, including localized question text, options, hints, explanation, formatting, semantics metadata, topic/subtopic data and step content used by the bundle fingerprint.
- `manifest.snapshotVersion` changes when the user snapshot changes, such as profile XP/level/streak or per-question progress.
- A profile-only change must not invalidate the content revision.

### `GET /api/offline/bundle/manifest`

Returns only the manifest object with the same `version` and `snapshotVersion` semantics as the full bundle route.

## Leaderboard

Auth: Required.

### `GET /api/leaderboard/student`

Canonical student leaderboard read for mobile clients.

Query params:
- `scope`: `global|school|faculty|friends`
- `period`: `all_time|week|month|day`
- `limit`: clamped to `1..200`
- `cursor`: optional versioned pagination token
- `includeMe`: optional; includes the caller's `me` rank block when `true`

Ordering is deterministic:
- `score DESC`
- `userId ASC`

Cursor contract:
- `nextCursor` is an opaque Base64 token produced by the backend
- current student cursor payload is version `v=2`
- cursor is bound to normalized `scope` and `period`
- a cursor from another scope/period must not be reused
- malformed, oversized, missing-field or unsupported-version cursors return `400`

Stable cursor error codes:
- `invalid_cursor`
- `cursor_too_large`
- `unsupported_cursor_version`
- `cursor_context_mismatch`

### `POST /api/cosmetics/fragments/grant`

See [mobile_economy_api_contract.md](./mobile_economy_api_contract.md#8-post-apicosmeticsfragmentsgrant).

Non-Daily-Run fragment grants now require a server-issued `entitlementId`. Daily Run remains server-derived from `transactionId`.

Response includes refreshed `inventory` (string item keys) and `fragmentProgress`.
