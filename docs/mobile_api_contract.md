# Mobile API Contract

Backend contract for the MathLearning Flutter runtime. Economy settlement mutations are documented in [mobile_economy_api_contract.md](./mobile_economy_api_contract.md).

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

### `GET /api/cosmetics/catalog`

Published cosmetic metadata for active, non-hidden items. Payload is identical for every authenticated user and is safe to cache.

Query params (optional):
- `category`
- `rarity`
- `seasonId`

Response:
```json
{
  "catalogVersion": "catalog-20260520120000",
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
