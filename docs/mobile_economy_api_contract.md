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
  "errorCode": "insufficient_balance|not_eligible|unknown_reward|idempotency_conflict|invalid_season|...",
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
`rewardId` is the canonical input. `rewardType` is validation/context only.
For authenticated mobile callers, request `coins` and `xp` are legacy compatibility fields and are ignored as settlement authority.
The server resolves the actual grant from a trusted reward catalog/rule set keyed by `rewardId` and current server-side user state.
Unknown `rewardId` values return `409` with `errorCode = "unknown_reward"`.
Single-use rewards are guarded by `rewardId`; duplicate rewardId claims do not mint again.
The initial catalog is persisted server-side in `economy_reward_definitions` as data-driven regex matchers plus JSON eligibility/grant rules.

Initial server-side reward catalog:
- `daily:{non-empty-id}` -> fixed daily reward of `20` coins and `15` xp.
- `level:{n}` -> eligible only when server-side `profile.Level >= n`; grants `n * 10` coins, minimum `10`, and `0` xp.
- `streak:{n}` -> eligible only when server-side `profile.Streak >= n`; grants `n * 5` coins, clamped to `10..500`, and `0` xp.
- `generic:onboarding_bonus` -> `50` coins, `0` xp.
- `generic:starter_bonus` -> `25` coins, `0` xp.
- `generic:welcome_back` -> `15` coins, `10` xp.

Retry note: legacy `coins`/`xp` fields still participate in the request payload for idempotency. Retries must reuse the exact same payload.

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

## Server Notes

- `POST /api/economy/rewards/claim` is now server-authoritative for authenticated callers. Client-supplied `coins` and `xp` are not trusted as settlement authority.
- Admin-only reward overrides are exposed through a separate authenticated admin endpoint: `POST /api/admin/economy/rewards/grant`. They are not supported by the mobile runtime endpoint.
- Admin overrides are audited in a dedicated `admin_economy_reward_grants` table and duplicate `grantId` values for the same user do not mint twice.
- `POST /api/seasons/daily-run-claim` uses server-side `DailyRunChestClaim` provenance as authority.
- `POST /api/cosmetics/fragments/grant` is idempotent and only unlocks a mapped cosmetic item once after the fragment threshold is reached.
- All new economy settlement endpoints require auth and `idempotencyKey`.
- Same `idempotencyKey` with a different request payload returns `409` with `errorCode = "idempotency_conflict"`.
- Business failures do not mint rewards or mutate balances/progress.
