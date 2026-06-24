# Backend Contract Gap Report

Last aligned: 2026-06-24  
Repo: `ivanjovicic/MathLearning`  
Role: current backend implementation snapshot against the mobile contract / idempotency handoff.

Related:

- [`README.md`](../README.md)
- [`mobile_contract_idempotency_handoff.md`](mobile_contract_idempotency_handoff.md)
- Mobile repo: `ivanjovicic/Mathlearning-Mobile-App/docs/mobile_api_contract.md`
- Mobile repo: `ivanjovicic/Mathlearning-Mobile-App/docs/mobile_backend_contract_status.md`
- Mobile repo: `ivanjovicic/Mathlearning-Mobile-App/docs/stabilization_status.md`

> **Snapshot note**
>
> This document records the backend gap analysis at the time it was written. It is not the canonical
> mobile contract. The canonical route/payload contract remains in the mobile repo, while implementation
> evidence must come from backend code, migrations, and tests in this repo.

---

## 1. Executive verdict

Economy and cosmetics settlement are largely implemented. The largest contract gap is offline-replay P0
idempotency for:

- `POST /api/quiz/answer`
- `POST /api/quiz/srs/update`
- `POST /api/daily-run/chest/claim`

Cosmetics/economy are substantially covered, but Quiz/SRS have no HTTP-level idempotency ledger and no
contract tests proving duplicate/conflict/rollback behavior.

---

## 2. P0 endpoint status

| Endpoint | Route exists | Handoff idempotency status | Notes |
|---|---:|---|---|
| `POST /api/quiz/answer` | Yes | **No** | No ledger, no `operationId`/`idempotencyKey` handling, no `409 idempotency_conflict`. |
| `POST /api/quiz/srs/update` | Yes | **No** | Same gap as quiz answer; no HTTP-layer idempotency. |
| `POST /api/daily-run/chest/claim` | Yes | **Partial** | Dedupe via `DailyRunChestClaims` + lock; no general ledger and no payload-hash conflict behavior. |
| `POST /api/seasons/daily-run-claim` | Yes | **Yes** | Uses `economy_transactions`, type `season_daily_run_claim`. |
| `POST /api/seasons/milestones/{id}/claim` | Yes | **Yes** | Uses `season_milestone_claim`. |
| `POST /api/cosmetics/fragments/grant` | Yes | **Yes** | Uses `cosmetics_idempotency_ledger`. |
| `POST /api/cosmetics/items/{itemKey}/claim` | Yes | **Yes** | Uses `cosmetics_idempotency_ledger`. |
| `POST /api/economy/coins/spend` | Yes | **Mostly** | Ledger exists; `operationId` is not wired through handlers. |
| `POST /api/economy/hints/use` | Yes | **Mostly** | Same economy ledger limitation. |
| `POST /api/economy/rewards/claim` | Yes | **Mostly** | Same economy ledger limitation. |
| `POST /api/shop/streak-freeze/purchase` | Yes | **Mostly** | Same economy ledger limitation. |

---

## 3. Route inventory

### Found canonical cosmetics/economy routes

| Endpoint | Status | Location |
|---|---|---|
| `GET /api/cosmetics/catalog` | Found | `CosmeticsEndpoints.cs` |
| `GET /api/cosmetics/inventory` | Found | `CosmeticsEndpoints.cs` |
| `GET /api/cosmetics/avatar` | Found | `CosmeticsEndpoints.cs` |
| `PUT /api/cosmetics/avatar` | Found; 403 on unowned | `CosmeticsEndpoints.cs` |
| `POST /api/cosmetics/items/{itemKey}/claim` | Found | `CosmeticsEndpoints.cs` |
| `POST /api/cosmetics/fragments/grant` | Found | `CosmeticsEndpoints.cs` |
| `GET /api/economy/rewards/preview` | Found | `EconomySettlementEndpoints.cs` |
| `POST /api/economy/rewards/claim` | Found | `EconomySettlementEndpoints.cs` |
| `POST /api/economy/coins/spend` | Found | `EconomySettlementEndpoints.cs` |
| `POST /api/economy/hints/use` | Found | `EconomySettlementEndpoints.cs` |
| `POST /api/shop/streak-freeze/purchase` | Found | `EconomySettlementEndpoints.cs` |
| `POST /api/seasons/daily-run-claim` | Found | `EconomySettlementEndpoints.cs` |
| `POST /api/seasons/milestones/{milestoneId}/claim` | Found | `EconomySettlementEndpoints.cs` |
| `POST /api/daily-run/chest/claim` | Found | `DailyRunEndpoints.cs` |

### Correctly missing deprecated routes

| Deprecated endpoint | Status | Note |
|---|---|---|
| `POST /api/cosmetics/unlock` | Missing | Correct; mobile contract marks it deprecated/unsupported. |
| `POST /api/cosmetics/fragments/daily-run` | Missing | Correct; use `POST /api/cosmetics/fragments/grant`. |

### Legacy / parallel routes still present

| Endpoint | Status | Note |
|---|---|---|
| `POST /api/coins/earn`, `POST /api/coins/spend` | Found | Deprecated; mobile should use `/api/economy/*`. |
| `POST /api/avatar/purchase` and related avatar routes | Found | Parallel legacy cosmetics path in `AvatarEndpoints.cs`. |
| `GET /api/avatar/me`, `GET /api/profile/{id}/appearance` | Found | Uses `IAvatarAppearanceReader`; good for profile/leaderboard appearance reads. |

---

## 4. Handler/service status

| Handler / service | Status | Gap |
|---|---|---|
| `CosmeticsEndpoints` + `CosmeticsEndpointHelpers` | Found | Mutation inventory in response can be empty because inventory may be loaded with `AsNoTracking()` before `SaveChanges`. |
| `CosmeticsIdempotencyService` | Found | Supports `operationId` + `idempotencyKey` + payload hash. |
| `CosmeticsFragmentService` | Found | Unlocks at threshold. |
| `DailyRunCosmeticsSettlement` | Found | Daily-run fragment authority exists. |
| `AvatarAppearanceReader` | Found | Profile/leaderboard appearance path is wired. |
| `MobileAvatarSlots` / `CosmeticPlatformService.Mobile` | Found | Full slot dictionary. |
| `EconomyTransactionService` + `EconomyEndpointHelpers` | Found | `TryBeginAsync` never passes `operationId`, so the column/index is unused. |
| `DailyRunEndpoints` chest claim | Found | Uses in-memory `SemaphoreSlim` + table uniqueness, not full ledger semantics. |
| `QuizEndpoints` answer / offline batch | Found | Stores `ClientId` on audit only; no dedupe before XP/progress mutation. |
| `SrsEndpoints` + `SrsService` | Found | No HTTP-layer idempotency. |
| Unified idempotency handler from handoff §5 | Missing | Current behavior is split across economy, cosmetics, and domain tables. |
| `CosmeticItemClaimRequest` binding | Partial | No direct `sourceType`/`sourceEvent`; conflict only if source or metadata differs. |

---

## 5. Migrations / tables

| Migration / table | Status | Purpose |
|---|---|---|
| `20260624133144_AlignCosmeticsMobileDataModel` | Found | Cosmetic item fragment fields, user cosmetics, user avatar, fragment progress, cosmetics ledger. |
| `20260624133927_AddDailyRunChestFragmentCopies` | Found | `DailyRunChestClaim.FragmentCopies`. |
| `20260624120000_AddEconomyTransactionOperationId` | Found | `economy_transactions.OperationId` + unique index. |
| `20260519150428_AddEconomySettlementAuthority` | Found | Base `economy_transactions` ledger. |
| `20260519160310` / `20260519174703` | Found | Reward catalog + admin grants. |
| Unified handoff `idempotency_ledger` table | Missing | Current design is split across `economy_transactions`, `cosmetics_idempotency_ledger`, and domain tables. |
| Quiz/SRS idempotency migration | Missing | No ledger table for `quiz_answer` / `srs_update`. |

---

## 6. Ledger status

| Ledger | Table | Scope | Used by | Status |
|---|---|---|---|---|
| Economy ledger | `economy_transactions` | `userId + transactionType + idempotencyKey` | Economy endpoints | Found; `OperationId` column exists but handlers do not populate it. |
| Cosmetics ledger | `cosmetics_idempotency_ledger` | `userId + operationType + operationId` and `userId + operationType + idempotencyKey` | Claim, fragment grant | Found; matches handoff semantics. |
| Daily Run chest | `daily_run_chest_claims` | `userId + transactionId`; `userId + day` | Chest claim | Partial; replay works, but no payload-hash conflict behavior. |
| Quiz answer | None | None | `POST /api/quiz/answer` | Missing. |
| SRS update | None | None | `POST /api/quiz/srs/update` | Missing. |

---

## 7. Behavior gaps vs handoff

| Requirement | Economy | Cosmetics | Daily-run chest | Quiz/SRS |
|---|---|---|---|---|
| Duplicate → `alreadyProcessed` / `alreadyClaimed` | Yes | Yes | `alreadyClaimed` only | No |
| Different payload → `409 idempotency_conflict` | Rewards yes; coins/hints/shop need tests | Yes | No | No |
| Ledger + domain in same DB transaction | Yes | Yes | Partial | No |
| Rollback leaves no completed ledger row | No dedicated test | No dedicated test | N/A | N/A |

---

## 8. Test status

### Found tests

| Area | Files | Coverage |
|---|---|---|
| Cosmetics API | `MobileCosmeticsApiIntegrationTests.cs` | Claim, replay, conflict, fragments, avatar, inventory. |
| Cosmetics contract | `MobileCosmeticsContractIntegrationTests.cs` | Catalog, inventory, avatar, daily-run tx idempotency. |
| Economy contract | `MobileEconomyContractIntegrationTests.cs` | Rewards, cosmetics payloads, season daily-run, milestone, fragment flow. |
| Economy settlement | `EconomySettlementEndpointsIntegrationTests.cs` | Coins, hints, rewards, preview, streak-freeze, season, cosmetics. |
| Daily Run chest | `DailyRunChestClaimEndpointTests.cs`, `DailyRunEndpointsIntegrationTests.cs` | Retry, concurrency, day/transaction rules. |
| Route smoke | `MobileApiRouteContractTests.cs` | 401 route checks, not settlement semantics. |
| SRS / quiz idempotency | `SrsServiceTests.cs` unit only | No HTTP idempotency coverage. |

### Missing tests

| Required test group | Status |
|---|---|
| `tests/MathLearning.Tests/Idempotency/*` folder | Missing |
| `QuizAnswerIdempotencyTests` — first, duplicate, conflict, rollback, different user | Missing |
| `SrsUpdateIdempotencyTests` | Missing |
| `DailyRunChestIdempotencyTests` — conflict different date/payload | Missing / partial coverage elsewhere |
| `LedgerRollbackTests` | Missing |
| CoinSpend / HintUse / StreakFreeze same key different payload → `409` | Missing |
| Cosmetics mutation response inventory array correctness | Missing |
| Mobile `mobile_backend_contract_status.md` evidence update | Pending after backend implementation evidence exists |

---

## 9. Contract-level mismatches

| Mismatch | Status / impact |
|---|---|
| `sourceEvent` on item claim | Not in `CosmeticItemClaimRequest` or idempotency hash; conflict only works through metadata/source differences. |
| Economy `operationId` | Migration + service support exists, but `EconomyEndpointHelpers.TryBeginAsync` does not pass it. |
| Economy idempotency scope wording | Mobile economy docs may say `UserId + RewardType + IdempotencyKey`; backend uses fixed transaction type strings such as `economy_reward_claim`. |
| Dual cosmetics paths | Mobile contract uses `/api/cosmetics/*`; legacy `/api/avatar/*` remains active. |
| Backend handoff local availability | `docs/mobile_contract_idempotency_handoff.md` is on `origin/main`; local clones must pull to see it. |

---

## 10. Recommended first implementation PR

### PR title

```text
Add quiz answer idempotency ledger and integration tests
```

### Why this first

`POST /api/quiz/answer` is the highest-risk P0 gap because offline replay can double-apply XP, progress,
coins, or rewards. It is a high-traffic mutation with no backend ledger and no HTTP idempotency tests.
Cosmetics/economy are already substantially covered.

### Scope

1. **Migration**
   - Add `quiz_idempotency_ledger`, or extend a shared ledger if the repo chooses that direction.
   - Required unique indexes:
     - `(user_id, operation_type, operation_id)`
     - `(user_id, operation_type, idempotency_key)`
2. **Handler**
   - Wrap `POST /api/quiz/answer` in the handoff algorithm from `mobile_contract_idempotency_handoff.md`.
   - Accept `operationId` + `idempotencyKey`, or map an existing client id only if it exactly matches the mobile contract.
3. **Transaction**
   - Ledger write + answer/progress/XP mutation in one DB transaction.
   - Failed domain mutation must leave no completed ledger row.
4. **Tests**
   - Add `tests/MathLearning.Tests/Idempotency/QuizAnswerIdempotencyTests.cs` covering:
     - first success
     - duplicate same payload → same result / `alreadyProcessed`
     - same keys, different answer → `409 idempotency_conflict`
     - rollback on domain failure
     - different user isolation
5. **Docs**
   - Update `docs/mobile_contract_idempotency_handoff.md` if scope/details change.
   - After merge, update mobile repo `docs/mobile_backend_contract_status.md` with commit and test evidence.

---

## 11. Recommended follow-up PR order

1. Add `POST /api/quiz/srs/update` ledger + tests.
2. Formalize `POST /api/daily-run/chest/claim` on ledger, or document the domain-table policy and add conflict tests.
3. Wire `operationId` through economy `TryBeginAsync` and add conflict tests for coins/hints/shop.
4. Fix cosmetics mutation response inventory array and add regression test.
5. Add `sourceEvent` to claim idempotency canonical payload if mobile sends it outside metadata.

---

## 12. Maintenance

Update this report when:

- backend idempotency implementation changes
- any P0 endpoint moves from missing/partial to verified
- backend tests are added for one of the missing groups
- the mobile contract changes operation types or payload fields
- legacy routes are removed or intentionally kept with documentation
