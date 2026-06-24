# Mobile Contract / Idempotency Handoff

Last aligned: 2026-06-24  
Repo role: backend verification / implementation handoff for `ivanjovicic/MathLearning`.

This backend repo must be verified against the Flutter mobile contract maintained in:

- `ivanjovicic/Mathlearning-Mobile-App/docs/mobile_api_contract.md`
- `ivanjovicic/Mathlearning-Mobile-App/docs/mobile_backend_contract_status.md`
- `ivanjovicic/Mathlearning-Mobile-App/docs/backend_idempotency_implementation_plan.md`
- `ivanjovicic/Mathlearning-Mobile-App/docs/RELEASE_CHECKLIST.md`

Do not mark a mobile endpoint production-safe from mobile tests alone. Backend endpoint code, migrations,
and integration/contract tests must prove the behavior in this repository.

---

## 1. Required idempotency scope

Default backend ledger scope for authenticated retryable mutations:

```text
userId + operationType + operationId/idempotencyKey
```

Definitions:

- `userId`: authenticated user id from bearer token / server auth context. Do not trust request body user id for ledger scope.
- `operationType`: logical mutation type, such as `quiz_answer` or `cosmetics_fragment_grant`.
- `operationId`: stable mobile operation identity.
- `idempotencyKey`: stable backend dedupe key. It may be identical to `operationId` for transaction-root flows.

If backend chooses a stricter policy, update both backend tests and mobile docs in the mobile repo.

---

## 2. Required backend behavior

For every idempotent mutation:

| Case | Required behavior |
|---|---|
| First request | Execute domain mutation once, store ledger result, return normal success. |
| Duplicate same user + operation type + same keys + equivalent payload | Return same logical result or `alreadyProcessed` / `alreadyClaimed`; do not mutate again. |
| Same scope + same keys + different payload | Return `409 idempotency_conflict`; do not mutate. |
| Domain mutation fails before commit | Roll back domain mutation and ledger result; retry may succeed later. |
| Same operation id for different users | Allowed; each user has separate ledger scope. |
| Same operation id for different operation types | Allowed unless backend documents a stricter policy. |

Ledger write and domain mutation should be in the same database transaction.

---

## 3. P0 endpoints to verify first

| Endpoint | Operation type | Why it is P0 | Required tests |
|---|---|---|---|
| `POST /api/quiz/answer` | `quiz_answer` | Offline replay can duplicate progress/coins/rewards if not deduped. | first success, duplicate same payload, conflict different answer, rollback, different user |
| `POST /api/quiz/srs/update` | `srs_update` | Offline replay can advance SRS schedule twice. | first success, duplicate, conflict, rate-limit/retry behavior |
| `POST /api/daily-run/chest/claim` | `daily_run_chest_claim` | Chest reward must settle once per transaction. | first claim, duplicate transaction, conflict different date/payload |
| `POST /api/seasons/daily-run-claim` | `season_daily_run_claim` | Season XP must be awarded once per Daily Run transaction. | first claim, duplicate, prerequisite Daily Run transaction validation |
| `POST /api/seasons/milestones/{milestoneId}/claim` | `season_milestone_claim` | Milestone reward must not duplicate. | first claim, duplicate/alreadyClaimed, different user |
| `POST /api/cosmetics/fragments/grant` | `cosmetics_fragment_grant` | Fragment progress and unlock thresholds must not double-count. | first grant, duplicate, conflict different fragment/copies |
| `POST /api/cosmetics/items/{itemKey}/claim` | `cosmetics_item_claim` | Inventory ownership must stay unique. | first claim, duplicate/alreadyClaimed, conflict different item/source |
| `POST /api/economy/coins/spend` | `economy_coin_spend` | Coin debit must not double-charge. | first spend, duplicate, conflict, insufficient balance duplicate |
| `POST /api/economy/hints/use` | `economy_hint_use` | Hint cost must not double-charge. | first use, duplicate, conflict different hint/question |
| `POST /api/economy/rewards/claim` | `economy_reward_claim` | Rewards must not settle twice. | first claim, duplicate/alreadyProcessed, conflict |
| `POST /api/shop/streak-freeze/purchase` | `shop_streak_freeze_purchase` | Purchase must not double-charge. | first purchase, duplicate, conflict |

---

## 4. Suggested ledger schema

Use the actual database naming conventions in this repo, but preserve the semantics.

```sql
CREATE TABLE idempotency_ledger (
  id UUID PRIMARY KEY,
  user_id UUID NOT NULL,
  operation_type VARCHAR(128) NOT NULL,
  operation_id VARCHAR(128) NOT NULL,
  idempotency_key VARCHAR(256) NOT NULL,
  endpoint VARCHAR(128) NOT NULL,
  payload_hash CHAR(64) NOT NULL,
  result_json JSONB NOT NULL,
  status VARCHAR(32) NOT NULL,
  http_status SMALLINT NOT NULL,
  created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE UNIQUE INDEX ux_idempotency_user_type_operation
  ON idempotency_ledger (user_id, operation_type, operation_id);

CREATE UNIQUE INDEX ux_idempotency_user_type_key
  ON idempotency_ledger (user_id, operation_type, idempotency_key);
```

Payload hash should be SHA-256 over canonical idempotency-relevant fields only. Exclude auth headers,
logging timestamps, and response-only fields.

---

## 5. Handler algorithm

```text
BEGIN TRANSACTION

1. Resolve user id from authenticated server context.
2. Resolve operationType for the endpoint.
3. Require operationId and/or idempotencyKey according to endpoint contract.
4. Compute canonical payload hash.
5. Look up existing ledger row by userId + operationType + operationId/idempotencyKey with lock.
6. If matching row exists and hash matches: return stored logical result / alreadyProcessed.
7. If matching row exists and hash differs: return 409 idempotency_conflict without mutation.
8. Execute domain mutation.
9. Store ledger row with result in the same transaction.
10. Commit.
```

Rollback on domain failure must leave no completed ledger row.

---

## 6. Backend verification checklist

For each P0 endpoint:

- [ ] Endpoint exists and matches mobile route.
- [ ] Authenticated user id comes from bearer token / server auth context.
- [ ] `operationType` is included in ledger scope or stricter policy is documented.
- [ ] `operationId` / `idempotencyKey` is accepted and validated.
- [ ] Payload hash includes only stable mutation fields.
- [ ] First request mutates domain tables exactly once.
- [ ] Duplicate same payload returns same logical result or `alreadyProcessed` / `alreadyClaimed`.
- [ ] Same keys with different payload returns `409 idempotency_conflict`.
- [ ] Rollback test proves no completed ledger row is stored when domain mutation fails.
- [ ] Integration/contract tests are committed in this repo.
- [ ] Mobile repo `docs/mobile_backend_contract_status.md` is updated with backend PR/commit/test evidence.

---

## 7. Suggested test layout

Use the repo's existing test project structure. Suggested grouping:

```text
tests/MathLearning.Tests/Idempotency/
  QuizAnswerIdempotencyTests.cs
  SrsUpdateIdempotencyTests.cs
  DailyRunChestIdempotencyTests.cs
  SeasonClaimIdempotencyTests.cs
  CosmeticsIdempotencyTests.cs
  EconomyIdempotencyTests.cs
  LedgerRollbackTests.cs
```

Prefer HTTP/integration tests where possible so routing, auth, model binding, transactions, and persistence are tested together.

---

## 8. Mobile status update rule

After backend verification, update mobile repo status only with evidence:

```text
ivanjovicic/Mathlearning-Mobile-App/docs/mobile_backend_contract_status.md
```

A backend row can move from `Backend verification required` to verified only when this repo has:

- backend PR/commit id
- test file names
- endpoint/migration evidence
- known limitations, if any

---

## 9. Maintenance

Update this handoff when:

- mobile contract changes endpoint route, payload, or operation type
- backend idempotency scope changes
- backend tests prove or disprove an endpoint behavior
- a new reward/economy/cosmetics mutation is added
- release checklist criteria change
