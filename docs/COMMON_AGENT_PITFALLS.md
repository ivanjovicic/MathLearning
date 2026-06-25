# Common Backend Agent Pitfalls

Last aligned: 2026-06-25  
Repo: `ivanjovicic/MathLearning`

Read this before implementing broad backend changes. These are the mistakes that waste tokens or create regressions in this project.

---

## 1. Treating request body user id as authority

Wrong:

```text
Use request.UserId to decide which profile/rewards/session to mutate.
```

Correct:

```text
Use authenticated server user id from HttpContext/auth claims. If route/body user id exists, validate it against auth user or require admin policy.
```

Relevant evidence:

- `MutationUserScopeIntegrationTests.cs`
- `SyncServiceTests.cs`
- `docs/backend_contract_gap_report.md` U2 section

---

## 2. Adding idempotency without payload conflict behavior

Wrong:

```text
Only check whether idempotencyKey exists, then return success for all duplicates.
```

Correct for generic ledgers:

```text
Compare canonical payload hash. Same keys + same payload = replay. Same keys + different payload = 409 idempotency_conflict.
```

Daily Run chest is the documented exception. It uses domain-table Policy B.

---

## 3. Inventing a new ledger pattern

This repo already has documented patterns:

- shared `IdempotencyLedger` for Quiz/SRS
- `economy_transactions` for economy/season/shop
- `cosmetics_idempotency_ledger` for cosmetics
- `daily_run_chest_claims` Policy B for Daily Run chest

Do not add a new table/service pattern unless the task explicitly requires it and docs/tests are updated.

---

## 4. Expanding legacy routes

Avoid making new mobile behavior depend on legacy routes:

- `/api/coins/*`
- legacy `/api/avatar/*` mutation paths
- old cosmetics unlock/purchase style routes

Prefer canonical routes:

- `/api/economy/*`
- `/api/cosmetics/*`
- `/api/quiz/srs/update`
- `/api/daily-run/chest/claim`

---

## 5. Forgetting endpoint inventory updates

If a route changes, update:

```text
docs/API_ENDPOINT_INVENTORY.md
```

If the route is mobile-facing, also update mobile docs after backend evidence exists.

---

## 6. Overclaiming CI or test status

Do not write:

```text
CI green
```

unless a real GitHub Actions run was found and checked.

Safe wording when connector cannot find a run:

```text
No GitHub Actions evidence found via connector.
```

---

## 7. Logging sensitive payloads

Never log:

- passwords
- JWTs or refresh tokens
- emails if avoidable
- raw answer payloads
- full idempotency payload JSON
- personal data from sync envelopes

Use safe metadata: endpoint, operation type, status, safe id suffix/hash.

---

## 8. Moving business logic into endpoint lambdas

Endpoint files are already large in places. Do not make this worse.

Prefer:

- service methods for domain logic
- helpers for idempotency begin/replay/conflict
- tests at service + contract level

Endpoint lambdas should remain HTTP boundary code.

---

## 9. Breaking Daily Run chest policy

Daily Run chest is not a generic idempotency ledger flow.

Current policy:

- same transaction id replays original claim
- same day/new transaction returns already claimed/replay behavior
- generic same-key/different-payload `idempotency_conflict` is not required unless policy changes

Do not “fix” it into generic semantics without updating docs and tests.

---

## 10. Assuming production auto-migrates

Startup migration behavior is environment-sensitive.

Before migration work:

- inspect startup mode
- inspect existing migrations
- avoid destructive changes
- document schema-critical changes
- run or document narrow validation command

---

## 11. Duplicating mobile contract truth

Backend docs may summarize mobile behavior, but canonical mobile payload contracts live in:

```text
ivanjovicic/Mathlearning-Mobile-App/docs/mobile_api_contract.md
```

If backend and mobile docs disagree, inspect current code/tests and update both sides with evidence.

---

## 12. Making huge unfocused commits

Prefer one focused prompt = one focused commit.

A good commit has:

- small scope
- relevant tests/docs
- clear commit message
- final response with validation and residual risk
