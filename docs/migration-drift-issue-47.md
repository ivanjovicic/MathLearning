# Issue #47 — Migration drift audit & safe repair

## Root cause
Seven legacy `ApiDbContext` migration classes ship without a `[Migration]` attribute or a Designer file. EF never discovers them, so `database update` skips them silently. As a result, the `UserAnswerAudits` XP columns and indexes were in the model snapshot but never existed physically. Note that `20260215165029_AddSchoolsAndFacultiesTables` *does* create the table itself (`CREATE TABLE IF NOT EXISTS "UserAnswerAudits"`). The objects that are missing are only the later XP columns and indexes. The readiness guard only compared migration history, so it reported Ready on a schema that was physically incomplete.

## Audit matrix
| Migration | Expected | Actual (dryrun/prod-copy) | Missing | Type mismatch | Constraint/index mismatch | Data risk | Priority |
|---|---|---|---|---|---|---|---|
| UserAnswerAudits XP (unregistered legacy classes) | 14 cols + 6 idx | 7 base cols, base idx | ClientId, CreatedAt, IsFirstTimeCorrect, IsOffline, Reason, Source, TotalXpAfterAward | none on existing cols (validated) | UX_UserAnswerAudits_FirstCorrect_PerQuestion, IX_User_Question_CreatedAt, IX_UserId/QuestionId/CreatedAt | quiz answer settlement 500s / XP dedup not enforced | P1 — repaired |
| economy_transactions idempotency | OperationId + UX index | present | — | — | — | — | guard only |
| AddPerformanceOptimizations (CONCURRENTLY) | perf indexes | partially absent (`user_cosmetic_inventory` table missing) | perf indexes | — | — | perf only | P3 — deferred, non-transactional |
| UserQuestionAttempts indexes | indexes | absent | indexes | — | — | perf only | P3 — deferred |
| Root AppDbContext 20260414000001_AddOptionOrder | registered | no Designer/attribute | n/a (other context) | — | — | needs separate analysis | P2 — finding, untouched |
| 20260118125140_InitIdentityAndDomain (prod history) | AppDbContext legacy | present in history | — | — | — | allowlisted | info |

## Patch
- `20261101185317_RepairUserAnswerAuditXpSchema`: a registered migration that runs guarded raw SQL. It sets `lock_timeout=5s` and `statement_timeout=60s`, validates the types of existing columns and the definition of any existing UX index, and stops if it finds duplicate first-correct rows. It then runs `ADD COLUMN IF NOT EXISTS` ×7 and `CREATE [UNIQUE] INDEX IF NOT EXISTS`. It contains no DROP, no CONCURRENTLY and does not touch history. Down is a no-op (forward-only). The model snapshot is unchanged.
- `DatabaseSchemaVersionGuard`: when the history is in sync, it also checks the critical physical columns (information_schema) **and the semantics of the critical indexes in `pg_index`**. An index with the right name is accepted only when `indisvalid` and `indisready` are true, `indisunique` has the expected value, the ordered key columns match exactly, there are no expression keys, and the predicate (`pg_get_expr(indpred)`) matches exactly. The specs live in `CriticalIndexSpecs`; a test asserts that they match the compiled model. Any deviation returns `Mismatch` with entries like `index:UX_…[indisvalid=false]` or `index:UX_…[unique=false; predicate=<none>]`. Before this fix, the guard checked `pg_indexes` by name only, and a same-name non-unique index without a predicate was accepted (proven by `artifacts/migrations/issue-47-invalid-index-probe.sql`).
- Tests: `MigrationDriftRegressionTests` (pure index evaluator: valid, missing, invalid, not ready, non-unique, missing or wrong predicate, wrong columns, expression keys, wrong table; real Postgres: a same-name wrongly defined index and an INVALID index left by a failed `CREATE UNIQUE INDEX CONCURRENTLY` are both rejected; allowlist of unregistered classes, repair migration registered and last, SQL safety, model contains the critical objects, a DB-gated physical-schema check) and `QuizAnswerSettlementPostgresTests` (real Postgres: first answer, replay, 409 conflict, XP dedup, rollback when a transaction fails).

## Evidence (Neon project silent-salad-97245148; prod br-fragrant-hat-agwg4cyi was only read)
- Control snapshot: `drift-repair-control-20261101` (br-cool-heart-ag596w8b). Test branch: `drift-repair-test-20261101` (br-long-wildflower-ag47ytjq).
- `dotnet ef database update` on the test branch: exit 0, 7152 ms wall time. Repair DDL: **114 ms**; history insert: 38 ms. The table had 0 rows.
- Before → after: UserAnswerAudits went from 7 to 14 columns and gained 6 indexes (the UX index has `WHERE "IsFirstTimeCorrect" = true`). History went from 62 to 63 rows. The md5 of the earlier history rows is unchanged (4280dec7…). The row-count fingerprint excluding history is unchanged (cb23a264…, equal to the parent).
- Empty DB: applying all 62 migrations exits 0. A rerun applies nothing and exits 0.
- Tests:
  - targeted suite: 34/34 pass;
  - full suite: 1268 passed, 2 failed, exit 1. Both failures are `Leaderboard_WeeklyXp_OnlyCountsThisWeek`, which also fails on origin/main (baseline exit 1), so it is a pre-existing Sunday week-boundary flake;
  - DatabaseSchema category on the repaired branch: 7/7, exit 0;
  - on the unrepaired branch: 2 failures, exit 1. This is the expected detection;
  - Postgres settlement test: 1/1, exit 0. With the repair migration unregistered it fails, exit 1.
- API started with `Database__StartupMode=ValidateExact` against the repaired branch: `Status=Ready Pending=0 UnknownApplied=0`. `/health` returns 200 and `/api/health/ready` returns 200 `{"status":"Ready"}`. With a stale dll (repair migration unregistered), the guard correctly reported `Mismatch UnknownApplied=20261101185317`.

## Release artifact (exact, committed)
`artifacts/migrations/issue-47-repair-idempotent.sql` was generated with:
```
dotnet ef migrations script 20260923145307_AddDesignTokenDraftUniqueness 20261101185317_RepairUserAnswerAuditXpSchema --idempotent \
  --project src/MathLearning.Infrastructure --startup-project src/MathLearning.Api --context ApiDbContext \
  -o artifacts/migrations/issue-47-repair-idempotent.sql
```
- Generation exit code: 0. The file has 93 lines, SHA256 `1D9F8C9F72BD7AAEFB3FBACBB3F247A5671166A5C958315791184929E5A7D78C`. Structure: `START TRANSACTION`, then `DO $EF$` (guarded repair, which contains `SET LOCAL` timeouts and a nested `DO $repair$`), then `DO $EF$` (history insert, 8.0.32), then `COMMIT`.
- Line endings: git stores the file with LF (`core.autocrlf`). The LF blob from git has SHA256 `6E8EDF3176C464E28F92D9BCF53E0E65FF26205DCE6C0E2078AF8AE53FFEA4FE`. The Windows CRLF checkout (the copy that was executed) has `1D9F8C9F…D78C`. The content is identical; only CR/LF differ.
- Before applying it to prod, verify the hash: `Get-FileHash artifacts/migrations/issue-47-repair-idempotent.sql` or `sha256sum …`.

### Execution evidence (the exact file, applied twice)
Branch `drift-artifact-test2-20261101` (br-steep-cloud-agya580a) was created fresh from the unrepaired control state (PG 17). Command: `psql "<url>" -X -v ON_ERROR_STOP=1 -f artifacts/migrations/issue-47-repair-idempotent.sql`.

| Step | Exit | Result |
|---|---|---|
| verify before (`issue-47-verify.sql`) | 0 | 62 history rows, last `20260923145307`; 7 columns; PK only; relfilenode 65761; 0 rows |
| run 1 | 0 | 877 ms; `START TRANSACTION / DO / DO / COMMIT` |
| run 2 (rerun) | 0 | 472 ms; no-op (the history guard skips both blocks) |
| verify after | 0 | 63 rows, last `20261101185317` (8.0.32); md5 of the 62 earlier rows unchanged (`85d2a0e3…`); 14 columns with the correct types and defaults; 6 indexes, all `indisvalid` and `indisready`; UX is unique `WHERE ("IsFirstTimeCorrect" = true)`; relfilenode **unchanged** (65761), so there was no rewrite |
| DB-gated tests (`SCHEMA_VALIDATION_REQUIRED=1`, `POSTGRES_PROVIDER_VALIDATION_REQUIRED=1`) | 0 | 40/40 |
| API `Database__StartupMode=ValidateExact` | — | `Status=Ready PendingCount=0 UnknownAppliedCount=0`; `/health` 200; `/api/health/ready` 200 `{"status":"Ready"}` |

A first run on `drift-artifact-test-20261101` (br-muddy-heart-ags6svbn) was mistakenly wrapped in an extra psql transaction. It also exited 0 twice and reached the same end state, but the clean run above is the evidence.

## Lock and rewrite risk (corrected)
- **There is no table rewrite.** On PG ≥ 11, `ADD COLUMN … DEFAULT <non-volatile expr>` is a catalog-only change ("fast default"). `NOW()` is **STABLE** (`provolatile='s'`), not volatile: it is evaluated once at ALTER time, and that value is stored in `pg_attribute.attmissingval` (`atthasmissing=t`) for all existing rows. Only volatile defaults such as `clock_timestamp()` or `random()` force a rewrite. `artifacts/migrations/issue-47-fastdefault-probe.sql` measured this on 200k rows inside a rolled-back transaction: relfilenode stayed the same after `DEFAULT now()` and the constant defaults, there was 1 distinct value, and it changed only after `clock_timestamp()`. The earlier statement in this document that "NOW() forces a rewrite" was wrong. Note that existing rows get the **ALTER-time timestamp** as `CreatedAt`, not their historical time.
- **Locks.** Every `ALTER TABLE ADD COLUMN` takes an ACCESS EXCLUSIVE lock, briefly, because it is metadata only. However, it waits behind any long-running transaction on the table, and while it waits it blocks new readers and writers. `lock_timeout=5s` bounds that wait: on timeout the whole transaction fails and nothing is applied.
- **Index build (the real risk).** `CREATE [UNIQUE] INDEX` without CONCURRENTLY takes a SHARE lock, which **blocks INSERT, UPDATE and DELETE for the whole build**. Build time grows with row count. The locks are held until `COMMIT` of the single transaction. `statement_timeout=60s` caps each statement. The UNIQUE build fails if there are duplicate first-correct rows; the guard aborts first with a clear error. The measured prod copy has 0 rows (114 ms), so the risk is negligible **only if prod is still that size**. Recount immediately before release. For more than ~100k rows, plan a maintenance window or a separate CONCURRENTLY rollout.

## Backup and recovery (corrected)
A snapshot is a **recovery point, not an automatic or lossless rollback**. Restoring it discards every write made after the snapshot. Down is a no-op, so it is not a rollback path, and nobody edits `__EFMigrationsHistory` by hand.
1. Right before release, create a Neon branch snapshot of prod (`pre-47-repair-<date>`) and record its LSN or timestamp.
2. If a problem occurs, **do not restore prod in place**. Restore or branch the snapshot into an **isolated** branch or endpoint.
3. Validate the isolated copy: history, schema (`issue-47-verify.sql`), the API in ValidateExact mode against it, and the critical flows.
4. **Reconcile post-snapshot writes.** Export the rows written to prod after the snapshot point (UserAnswerAudits, economy_transactions, users and progress, etc.) and decide how to re-apply or merge them into the copy. Record what was reconciled.
5. Only after explicit approval, **cut over** (switch the connection or endpoint, or restore the primary), then re-verify readiness.
Because the repair is additive (new columns and indexes), the preferred response to a problem is usually roll-forward (a corrective migration), not a restore.

## Controlled production release (requires separate approval)
1. Confirm the runtime DB host of the production API.
2. Read-only preflight: `SELECT count(*) FROM "UserAnswerAudits"`, the duplicate first-correct query, the current history tail, and `issue-47-verify.sql`.
3. Take the snapshot (above) and verify the artifact SHA256.
4. In the maintenance window: `psql "<prod>" -X -v ON_ERROR_STOP=1 -f artifacts/migrations/issue-47-repair-idempotent.sql`, then run `issue-47-verify.sql` (check that all indexes are valid and ready).
5. Deploy the API with the guard and confirm `/api/health/ready` returns Ready.

## CI baseline and test exception
- PR head 186b6ac, run [41340823715](https://github.com/ivanjovicic/MathLearning/actions/runs/41340823715), job `database-suite` (124950280036): build OK and validate-schema-from-zero OK. The test step failed 2 of 1271, both `LeaderboardDataTests.Leaderboard_WeeklyXp_OnlyCountsThisWeek` (Expected 1, Actual 0). Because of that, the later artifact and smoke steps were skipped.
- Green main baseline: the Database Validation run [41016701507](https://github.com/ivanjovicic/MathLearning/actions/runs/41016701507) on main `ce1f2f2` (Friday 2026-10-30, 07:53 CET) **passed**.
- Reproduced on clean `origin/main` `ce1f2f2c5e07f4d32a7fd3f1ecd96331a76a08e8` on Sunday 2026-11-01 (19:38Z): the same test fails 2 of 2, exit 1. So it is pre-existing and independent of this PR.
- Root cause (test bug): the test computes `weekStart = UtcNow.Date.AddDays(-(int)DayOfWeek + 1)`. On Sunday (`DayOfWeek=0`) that gives *tomorrow*, so 0 rows are counted. The production code handles Sunday correctly (`XpTrackingService`/`XpResetProcessor`: `dow==0 ? 6 : dow-1`; `SchoolLeaderboardPeriods`: `(dow+6)%7`).
- Exception: this PR does **not** change or skip the test. The Sunday failure is accepted as a known flake for this PR. Follow-up: fix the test's week-start calculation in a separate PR. The artifact and smoke CI steps on the new head still need a run on a non-Sunday day (or after the test fix).

## Deferred
Perf indexes (CONCURRENTLY, outside any transaction; `user_cosmetic_inventory` is missing), UserQuestionAttempts indexes, AddOptionOrder (root context), and the weekly-XP test flake (Sunday week-start bug in the test, see above).
