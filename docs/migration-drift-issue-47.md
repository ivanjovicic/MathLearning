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
- `DatabaseSchemaVersionGuard`: when the history is in sync, it also checks the critical physical columns and indexes (information_schema / pg_indexes). If any are missing, it returns `Mismatch` with `MissingCriticalObjects`.
- Tests: `MigrationDriftRegressionTests` (allowlist of unregistered classes, repair migration registered and last, SQL safety, model contains the critical objects, a DB-gated physical-schema check) and `QuizAnswerSettlementPostgresTests` (real Postgres: first answer, replay, 409 conflict, XP dedup, rollback when a transaction fails).

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

## Lock-risk estimate
Adding a column with a constant or volatile default (NOW()) on PG ≥11: constant defaults only change metadata. `NOW()` forces a table rewrite, but the measured table size is 0 rows (prod copy), so total DDL time was 114 ms. Each statement holds an ACCESS EXCLUSIVE / SHARE lock for milliseconds and is bounded by lock_timeout=5s. If the lock cannot be taken, the migration fails cleanly and nothing is applied. **Re-measure the prod row count of UserAnswerAudits right before rollout.**

## Backup / rollback
1. Before rollout, create a Neon branch snapshot of prod (`pre-47-repair-<date>`).
2. Down is forward-only (no-op). The added objects are inert additive columns and indexes.
3. To roll back, restore prod from the snapshot branch (Neon restore). Never edit `__EFMigrationsHistory` by hand.

## Controlled production release (requires separate approval)
```
dotnet ef migrations script 20260923145307_AddDesignTokenDraftUniqueness 20261101185317_RepairUserAnswerAuditXpSchema --idempotent \
  --project src/MathLearning.Infrastructure --startup-project src/MathLearning.Api --context ApiDbContext -o repair-47.sql
```
Steps:
1. Confirm the runtime DB host.
2. Take the snapshot.
3. Check `SELECT count(*) FROM "UserAnswerAudits"` and look for duplicate first-correct rows.
4. Apply repair-47.sql in a maintenance window.
5. Deploy the API with the guard and verify that `/api/health/ready` returns Ready.

## Deferred
Perf indexes (CONCURRENTLY, outside any transaction; `user_cosmetic_inventory` is missing), UserQuestionAttempts indexes, AddOptionOrder (root context), and the weekly-XP test flake.
