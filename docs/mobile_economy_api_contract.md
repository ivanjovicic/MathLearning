# Mobile Economy API Contract (Backend-Authoritative)

## Purpose
These endpoints are the authoritative settlement layer for authenticated users.  
Client-side caches and local storage are no longer a source of truth for economy/reward progress.

## Idempotency Rules
- Every mutation request must include `idempotencyKey`.
- Idempotency scope: `UserId + TransactionType + IdempotencyKey`.
- Same key + same payload:
  - returns previously settled result.
  - response marks replay as already processed (`alreadyProcessed=true` or `alreadyClaimed=true` where applicable).
- Same key + different payload:
  - `409` with `errorCode = "idempotency_conflict"`.
- Failed transactions are terminal and replay the same failure response.

## Error Shape
Business errors return:
```json
{
  "success": false,
  "errorCode": "insufficient_balance|not_eligible|idempotency_conflict|invalid_season|...",
  "message": "..."
}
```

## Endpoints

### 1) `POST /api/economy/coins/spend`
Request:
```json
{
  "idempotencyKey": "string",
  "amount": 10,
  "reason": "hint|shop|other",
  "metadata": {}
}
```
Response includes authoritative `coins`, `freeHints`, and `spentCoins`.

### 2) `POST /api/economy/hints/use`
Request:
```json
{
  "idempotencyKey": "string",
  "questionId": 123,
  "hintType": "clue|formula|eliminate|solution",
  "costCoins": 10
}
```
Server validates hint type/cost and returns authoritative `coins`, `freeHints`, `spentCoins`, `usedFreeHint`.

### 3) `POST /api/economy/rewards/claim`
Request:
```json
{
  "idempotencyKey": "string",
  "rewardId": "string",
  "rewardType": "daily|level|streak|generic",
  "coins": 10,
  "xp": 0,
  "metadata": {}
}
```
Single-use rewards are guarded by `rewardId`; duplicate rewardId claims do not mint again.

### 4) `POST /api/shop/streak-freeze/purchase`
Request:
```json
{
  "idempotencyKey": "string",
  "quantity": 1
}
```
Response returns authoritative `coins`, `streakFreezeCount`, `spentCoins`.

### 5) `POST /api/seasons/daily-run-claim`
Request:
```json
{
  "idempotencyKey": "string",
  "transactionId": "daily-run-claim-id",
  "seasonId": 1,
  "xp": 25
}
```
Server validates season and daily-run claim provenance, then returns authoritative season state.

### 6) `POST /api/seasons/milestones/{milestoneId}/claim`
Request:
```json
{
  "idempotencyKey": "string",
  "seasonId": 1
}
```
Server validates milestone unlock/claim state and settles reward atomically with claim state.
`cosmetic_fragment` rewards are explicit in contract (`fragmentName`, `fragmentCopies`) and are not silently mapped to item unlocks.

### 7) `POST /api/cosmetics/items/{itemId}/claim`
Request:
```json
{
  "idempotencyKey": "string",
  "source": "season|dailyRun|shop|admin|reward",
  "metadata": {}
}
```
Server resolves ownership; response returns refreshed `inventory` + `fragmentProgress`.

### 8) `POST /api/cosmetics/fragments/grant`
Request:
```json
{
  "idempotencyKey": "string",
  "fragmentName": "Comet Frame Fragment",
  "copies": 1,
  "source": "dailyRun|season|reward",
  "metadata": {}
}
```
Server increments fragment progress once per idempotency key and may unlock mapped item server-side.

## Flutter Methods to Replace
- `CoinProvider.trySpendCoins`
- `CoinProvider.useHint`
- `CoinProvider.claimLocalRewardOnce`
- `CoinProvider.addCoins`
- `StreakFreezeProvider.add`
- `SeasonProvider.awardDailyRunXp`
- `SeasonProvider.claimMilestone`
- `CosmeticsService.unlockItem`
- `CosmeticsService.grantDailyRunFragment`

## Local Fallback Semantics
- Client local flags/SharedPreferences may only be used for in-flight UX guards and retry metadata.
- They must not mint coins/xp or unlock cosmetics before server confirmation.

## Cross-Device Correctness
Cross-device correctness depends on backend idempotency + authoritative refresh:
1. Reuse stable idempotency keys on retries.
2. Refresh authoritative balances/progress after settlement responses.
3. Do not assume device-local state is authoritative across devices.

## Server Observations & Recommendations (review notes)

- Observation: The current backend implementation (as of this review) accepts client-supplied `coins` and `xp` in `POST /api/economy/rewards/claim` and applies them to the user's profile.
  - Risk: This lets a malicious client mint coins/xp by crafting reward requests.
  - Recommendation: Treat `rewardId` as the single canonical input and resolve the award amounts server-side from a trusted reward catalog or rule engine. The server should ignore or reject client-supplied `coins`/`xp` unless the caller is an authorized admin.

- Observation: `POST /api/seasons/daily-run-claim` uses server-side `DailyRunChestClaim` rows (by `transactionId`) as authority. This matches the desired contract: server-side provenance is used to determine awarded XP.
  - Recommendation: Keep `DailyRunChestClaim` or equivalent as the canonical authority for daily-run claims.

- Observation: `POST /api/cosmetics/fragments/grant` is implemented idempotently and includes a threshold-based unlock path. The endpoint uses idempotency + inventory checks to avoid double-unlock.
  - Recommendation: Keep fragment unlocks driven by server-side fragment progress and enforce uniqueness on `UserCosmeticInventories` where possible.

- Observation: There are no `POST /api/coins/earn` or legacy `/api/coins/spend` (top-level) routes discovered in the current codebase; the mobile runtime should use the new `/api/economy/*` endpoints. If legacy/compat endpoints exist in other deployments, document them as admin/legacy-only.

- Observation: There is no `/api/cosmetics/purchase` route in the codebase. Clarify whether `shop` endpoints (for purchases) are the canonical way to buy cosmetics, or whether a legacy alias should be supported.
  - Recommendation: Document which endpoint(s) represent the canonical shop purchase flow (for example `/api/shop/streak-freeze/purchase` and potential `/api/shop/cosmetics/purchase`), or add an explicit alias if clients expect `/api/cosmetics/purchase`.

- Idempotency & auth enforcement: All newly introduced endpoints are under authenticated route groups and validate `idempotencyKey`. The server maps idempotency payload conflicts to `409` (`idempotency_conflict`). Business failures return `409` (or `400`) and do not commit the terminal success state. Existing integration tests assert retry/no-double-grant behavior for coins, hints, fragments, seasons, and shop purchases.

- Action items:
  1. Implement server-side reward catalog / rule evaluation for `rewards/claim` and stop applying client-supplied `coins`/`xp`.
  2. Clarify shop/cosmetics purchase routing and document legacy endpoints (if any) as admin-only.
  3. Add an integration test that proves `rewards/claim` ignores client-supplied `coins`/`xp` (or rejects non-authoritative payloads) after the catalog is implemented.

These notes were added after running the backend review and executing the targeted economy endpoint integration tests.
