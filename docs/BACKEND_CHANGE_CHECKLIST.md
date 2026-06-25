# Backend Change Checklist

Last aligned: 2026-06-25  
Repo: `ivanjovicic/MathLearning`

Use this checklist before every backend commit.

---

## 1. Scope

- [ ] Confirm target repo is `ivanjovicic/MathLearning`.
- [ ] Confirm whether the task is docs-only, endpoint, service, migration, test, or release work.
- [ ] Read `AGENTS.md` and the relevant quickstart section.
- [ ] Inspect the files you will change before editing.
- [ ] Keep the change focused; do not refactor unrelated areas.

---

## 2. Endpoint changes

- [ ] Update `docs/API_ENDPOINT_INVENTORY.md` if route/method/auth/status changes.
- [ ] Verify auth policy: public/auth/admin/mixed.
- [ ] Verify mobile-facing payloads against mobile contract docs.
- [ ] Add or update route/auth/contract tests if the route is mobile-facing.
- [ ] Do not expand legacy `/api/coins/*` or `/api/avatar/*` paths unless explicitly required.

---

## 3. Auth / user-scope changes

- [ ] Mutation scope comes from authenticated server user id.
- [ ] Request body `userId` is not trusted for user-owned mutations.
- [ ] Route `userId` either equals auth user or endpoint is admin-only.
- [ ] Admin actor and target user remain separate.
- [ ] Cross-user regression test added or updated if scope changed.

Relevant tests:

- `MutationUserScopeIntegrationTests.cs`
- `UserSettingsEndpointsIntegrationTests.cs`
- `PracticeSessionServiceIntegrationTests.cs`
- `SyncServiceTests.cs`

---

## 4. Idempotency changes

- [ ] Operation type is explicit and documented.
- [ ] Scope matches intended policy.
- [ ] First request mutates domain once.
- [ ] Duplicate same payload replays settled result.
- [ ] Same key/different payload returns `409 idempotency_conflict` where generic ledger semantics apply.
- [ ] Daily Run chest remains documented Policy B unless intentionally changed.
- [ ] Domain mutation and ledger write are transactional where applicable.
- [ ] Rollback path does not leave a completed ledger row.
- [ ] Tests cover duplicate/conflict/different-user/rollback.

Relevant tests:

- `tests/MathLearning.Tests/Idempotency/*`
- `tests/MathLearning.Tests/Contracts/MobileMutationContractIntegrationTests.cs`

---

## 5. Economy / rewards / cosmetics

- [ ] Economy settlement uses `/api/economy/*`, not legacy `/api/coins/*`.
- [ ] Coin spend cannot double-debit.
- [ ] Reward claim cannot double-settle.
- [ ] Hint use cannot double-charge.
- [ ] Streak-freeze purchase cannot double-charge.
- [ ] Cosmetics claim/grant uses idempotency service.
- [ ] Cosmetics mutation response reflects persisted inventory state.
- [ ] Avatar update rejects unowned items.

---

## 6. Migrations / schema

- [ ] Inspect `ApiDbContext` and existing migrations first.
- [ ] Add indexes matching lookup scope.
- [ ] Avoid destructive schema changes unless explicitly required and tested.
- [ ] Document new contract-critical tables/indexes.
- [ ] Consider deploy order and production startup mode.

---

## 7. Observability

- [ ] No raw answers, passwords, tokens, emails, or full payload JSON in logs.
- [ ] Idempotency observability uses safe metadata only.
- [ ] Admin/observability endpoints remain admin-protected.
- [ ] Correlation/request logging behavior is not broken.

---

## 8. Tests / validation

- [ ] Run the narrowest relevant tests first.
- [ ] If tests cannot run, document why.
- [ ] Do not claim CI green without Actions evidence.
- [ ] If only docs changed, validate referenced paths/test names/commands exist.

Suggested validation wording:

```text
Validation: <command> — <passed/failed/not run>
Reason if not run: <reason>
CI: No GitHub Actions evidence found via connector
```

---

## 9. Docs to update

Update as applicable:

- `docs/API_ENDPOINT_INVENTORY.md`
- `docs/ARCHITECTURE_OVERVIEW.md`
- `docs/backend_contract_gap_report.md`
- `docs/mobile_contract_idempotency_handoff.md`
- `README.md`
- cross-repo mobile docs after backend evidence changes

---

## 10. Final response checklist

Include:

- [ ] commit SHA
- [ ] files changed
- [ ] validation command/result or reason skipped
- [ ] risk / remaining gap
- [ ] next recommended prompt/task
