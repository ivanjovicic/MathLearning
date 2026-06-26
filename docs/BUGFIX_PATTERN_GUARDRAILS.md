# Backend Bugfix Pattern Guardrails

Last aligned: 2026-06-27

Guidance for agents fixing bugs in `ivanjovicic/MathLearning`.

This file captures recurring backend bugfix themes: auth-user scope, idempotency, mobile contract drift, migrations, route compatibility, validation bounds, and safe observability.

## Core rule

Every backend bug fix should add or update the smallest regression test that proves the bug cannot return.

If a test is not practical in the focused scope, say why and record the risk.

## Before editing a backend bug

1. Identify the owning endpoint/service/entity/test group.
2. Search for an existing integration or contract test in the same area.
3. Decide whether the bug is auth-scope, idempotency, contract shape, migration/schema, validation, transaction, or logging related.
4. Keep endpoint code thin; put business behavior in the owning service/ledger when one exists.
5. Pick the narrowest `dotnet test` command before editing.

## Common backend bug patterns

### Auth-user scope

- Mobile-facing mutations must use the authenticated server user id.
- Do not trust `userId` from request body as mutation authority.
- Admin actor and target user must stay separate.
- Test cross-user access and same-user success.

### Idempotency and duplicate retry

- Stable operation keys must survive mobile retry and app restart.
- Same key and same payload should replay the settled result.
- Same key with different payload should return a conflict when that route uses generic idempotency.
- Domain failure must not leave a completed ledger row.
- Add tests for first request, duplicate, conflict, and different-user isolation.

### Transactions and ledgers

- Domain mutation and idempotency ledger write should be in the same transaction where applicable.
- Unique indexes must match the documented idempotency scope.
- Do not create a new ledger pattern without documenting why.
- Test rollback behavior when domain work fails.

### Mobile contract shape

- Mobile contract changes need backend endpoint tests or smoke evidence.
- Keep response shape stable for mobile until a cross-repo cleanup prompt updates both sides.
- Test missing optional fields, validation errors, auth errors, and legacy-compatible routes where supported.

### Legacy route compatibility

- Prefer canonical routes documented in `API_ENDPOINT_INVENTORY.md`.
- Do not expand legacy aliases unless compatibility is the goal.
- If an alias remains, it must call the same service/policy as the canonical route.
- Add tests proving alias behavior matches the intended canonical behavior or returns the documented deprecation response.

### Request validation and bounds

- Validate counts, ids, language/difficulty values, and required fields at the API boundary.
- Keep invalid input behavior consistent: either normalize deliberately or return a documented error.
- Add tests for zero, negative, too large, malformed, missing, and normal values.

### Migrations and schema drift

- Inspect existing migrations and model snapshot before adding a migration.
- Do not assume auto-migration in production.
- Ensure nullable/default decisions match existing data.
- Add or update docs for new tables, indexes, or backfill assumptions.

### Observability and logs

- Do not log tokens, passwords, emails, raw answers, or full request payloads.
- Log safe metadata only: endpoint, operation type, safe operation suffix/hash, status, and category.
- Admin/observability endpoints must keep the correct policy.

## Regression test mapping

| Bug area | Minimum useful test |
|---|---|
| Auth scope | Same-user success and cross-user rejection. |
| Idempotency | First, duplicate, conflict, and different-user cases. |
| Transaction failure | Domain failure does not mark ledger complete. |
| Mobile contract | HTTP contract test for response/error shape. |
| Legacy alias | Alias matches canonical behavior or documented deprecation. |
| Validation bounds | Zero, negative, too large, missing, malformed, and normal values. |
| Migration/schema | Model/migration test or documented manual verification. |
| Logging | Test or review proving sensitive fields are not logged. |

## Commit message guidance

Prefer specific messages:

- `fix(auth): enforce profile ownership`
- `test(idempotency): guard duplicate quiz replay`
- `fix(contract): preserve mobile quiz response shape`
- `fix(validation): cap quiz question count`
- `docs(migration): record rollback notes`

Avoid vague messages such as `fix bugs` for completed prompt work.

## Final response for bugfix work

Use this format:

```text
Bug class:
Regression test:
Validation:
Commit:
Residual risk:
```

If no regression test was added, explain why.
