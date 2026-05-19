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
