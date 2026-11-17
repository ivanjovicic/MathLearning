# Issue #47 — Migration drift audit & safe repair

## Historical root cause (confirmed 2026-11-01)
At the time of the incident, seven legacy `ApiDbContext` migration classes shipped without a `[Migration]` attribute or a Designer file. EF did not discover them, so `database update` skipped them silently. As a result, the `UserAnswerAudits` XP columns and indexes were in the model snapshot but did not exist physically. `20260215165029_AddSchoolsAndFacultiesTables` *did* create the table itself (`CREATE TABLE IF NOT EXISTS "UserAnswerAudits"`); the missing objects were the later XP columns and indexes. The readiness guard only compared migration history, so it reported Ready on a schema that was physically incomplete. The incident logs confirmed PostgreSQL `42703` for the missing `UserAnswerAudits.ClientId` column.

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
- `DatabaseSchemaVersionGuard`: when migration history is in sync, it checks the PostgreSQL type, character maximum length and nullability of the seven critical `UserAnswerAudits` columns against `CriticalColumnSpecs`, plus the presence of other critical columns and **the semantics of critical indexes in `pg_index`**. An index with the right name is accepted only when `indisvalid` and `indisready` are true, `indisunique` has the expected value, the ordered key columns match exactly, there are no expression keys, and the predicate (`pg_get_expr(indpred)`) matches exactly. The column and index specs are tested against the compiled model. Deviations return `Mismatch` with bounded column- or index-specific details. Before this fix, same-name columns with a wrong type, length or nullability could pass startup validation; the provider regression tests reproduce those cases.
- Tests: `MigrationDriftRegressionTests` (pure column/index spec evaluation and model alignment; real Postgres: missing or wrong-shaped critical columns, same-name wrongly defined indexes and an INVALID index left by a failed `CREATE UNIQUE INDEX CONCURRENTLY` are rejected; allowlist of unregistered classes, repair migration registered and last, SQL safety, model contains the critical objects, and a DB-gated physical-schema check) and `QuizAnswerSettlementPostgresTests` (real Postgres: first answer, replay, 409 conflict, XP dedup, rollback when a transaction fails).

## Historical repair evidence (2026-11-01; test branches; production read-only during this audit)
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

## Historical CI baseline and Sunday test correction
- PR head 186b6ac, run [41340823715](https://github.com/ivanjovicic/MathLearning/actions/runs/41340823715), job `database-suite` (124950280036): build OK and validate-schema-from-zero OK. The test step failed 2 of 1271, both `LeaderboardDataTests.Leaderboard_WeeklyXp_OnlyCountsThisWeek` (Expected 1, Actual 0). Because of that, the later artifact and smoke steps were skipped.
- Green main baseline: the Database Validation run [41016701507](https://github.com/ivanjovicic/MathLearning/actions/runs/41016701507) on main `ce1f2f2` (Friday 2026-10-30, 07:53 CET) **passed**.
- Reproduced on clean `origin/main` `ce1f2f2c5e07f4d32a7fd3f1ecd96331a76a08e8` on Sunday 2026-11-01 (19:38Z): the same test fails 2 of 2, exit 1. So it is pre-existing and independent of this PR.
- Root cause (test fixture): its old calculation used `UtcNow.Date.AddDays(-(int)DayOfWeek + 1)`. On Sunday (`DayOfWeek=0`) that advances the start to the following Monday and excludes the Sunday fixture. Production uses Monday-start UTC logic.
- **Resolved by [PR #59](https://github.com/ivanjovicic/MathLearning/pull/59)**: both fixed-date leaderboard tests now use the production week-start helper and cover Sunday inclusion, the Monday 00:00 UTC boundary, and exclusion of the prior week; year-transition cases cover the reset policy. The exact PR head passed focused tests repeatedly, and [Database Validation run 43769009866](https://github.com/ivanjovicic/MathLearning/actions/runs/43769009866) passed its full PostgreSQL suite and validation job. [Backend Agent System run 43769009860](https://github.com/ivanjovicic/MathLearning/actions/runs/43769009860) also passed. This closes the historical test-fixture exception; it does **not** verify production recovery.

## Deferred
Perf indexes (CONCURRENTLY, outside any transaction; `user_cosmetic_inventory` is missing), UserQuestionAttempts indexes, and AddOptionOrder (root context) remain deferred. The Sunday weekly-XP test fixture issue was corrected by PR #59.

## B2-OPS production identity and settlement audit (2026-11-15; repository recheck 2026-11-17)

**Production evidence status: `BLOCKED_EXTERNAL_EVIDENCE`; deployment: NO-GO.** This section records an access-limited audit, not a current production schema or settlement verification. The [issue #47 operator comments](https://github.com/ivanjovicic/MathLearning/issues/47) confirm the historical `42703` root cause at 13:42–13:43 UTC on November 1. They do not identify the current Fly database endpoint or prove durable rows for either request. Documentation delivery in [PR #58](https://github.com/ivanjovicic/MathLearning/pull/58) is separate from production recovery; the issue remains open.

### Source and artifact evidence

- At the 2026-11-15 operations audit, `origin/main` was `59dc54d831270d751d3b7d98c8a8a3fb88a44088`. On 2026-11-17, `origin/main` is `ca3426575cb13acf4d1090e9668bb88ccb8c574c`; changes since the audit are test/evidence-only. The approved application commit `d945f51446e4d6e853bbc136134915b4c183aaf4` contains the registered `20261101185317_RepairUserAnswerAuditXpSchema` migration and `DatabaseSchemaVersionGuard`. Those migration files, the guard and `Dockerfile` have no diff between the approved app SHA and current main. The build-only workflow is a separate main-branch control file and explicitly checks out that exact app SHA. Source presence does **not** prove deployment or execution.
- The public [build-only workflow history](https://github.com/ivanjovicic/MathLearning/actions/workflows/fly-recovery-build-only.yml) still contains only [run 43711363732](https://github.com/ivanjovicic/MathLearning/actions/runs/43711363732), failed at image identity verification on the older `53a56ba` workflow revision and published no artifact. It reported local Buildx manifest `sha256:53fcaf20cbe9bf50fbd6e8b36ee82d5e67ee85450a7f674986c43dcb8a367ba3` and config `sha256:9b639d46a5569aa4e260836e1eb92599af91ad8ba945a6872f33b7057adad498`; neither identifies the Fly image or a registry-published digest. No successful corrected build, image archive or registry digest is recorded. `Deploy to Fly.io` builds separately and does not consume a build-only artifact.
- **Historical November 10 claim:** merged [PR #51](https://github.com/ivanjovicic/MathLearning/pull/51) states that the repair migration had already been applied to Neon production. That PR changed only `.github/workflows/deploy.yml` and performed no deployment. Treat this as an operator claim, not independent evidence of the current Fly release, endpoint, physical schema or successful settlement.
- The local Fly CLI did not return, even for `version`; a direct authenticated read-only Machines API request returned HTTP 403. No protected Neon/libpq read-only profile or Neon API credential was available. These are **external access limitations, not software defects**. No Fly application secret or connection string was read or output; no machine, database or production SQL was read. The documented October release v136 is historical, not proof of today's running image.
- The historical issue #47 quiz-answer investigation recorded **53/53 focused local tests**, including isolated PostgreSQL settlement and schema-guard checks ([run log](../.ai/runs/2026-11-15-USER-20261115-ISSUE47-QUIZ-ANSWER-evidence.md)). These are local/test-branch results, not evidence of production recovery; they were not rerun as part of the operations audit.

### Durable outcome classification

| Historical request | Exception/transaction evidence | Durable outcome |
|---|---|---|
| `ml-hnr95hys2g-n` | `DbUpdateException` / PostgreSQL `42703` at `IdempotencyLedgerService.CompleteAsync` before the keyed transaction's explicit commit; rollback is the expected path. | **UNKNOWN** until an account/session/operation-anchored production read confirms the ledger, answer, audit, XP and stat state. |
| `ml-hnr966knvv-o` | Same `42703`; application telemetry also says `rolled_back`. This supports but does not independently prove persistence. | **UNKNOWN** for the same reason. |

The earlier account-scoped four-hour zero counts were not tied to either operation identity. Correlation IDs are not database keys. `UserQuestionStats` is an aggregate without a per-attempt row or creation time, so compare its state to an authorized earlier snapshot or corroborating answer/XP records; a current count alone cannot prove a historical rollback.

### Release-readiness matrix

| Gate | Evidence at audit | State |
|---|---|---|
| Repository source | Current main `ca3426575cb13acf4d1090e9668bb88ccb8c574c`; approved app `d945f...` has the registered repair and guard. | Source verified; deployment unverified. |
| Application image | No successful corrected build-only run or image archive; Fly machine image digest, OCI revision label, compiled migration set and startup guard log unavailable. | **BLOCKED** |
| Database identity | Intended Neon project `silent-salad-97245148`, branch `br-fragrant-hat-agwg4cyi` from the earlier issue report; Fly's effective endpoint ID and database name unavailable. | **BLOCKED** |
| Physical schema | Expected 63 history rows (62 API plus one documented legacy Admin row), repair migration present, 14 audit columns, and six audit indexes including the PK; no current production catalog read. | **BLOCKED** |
| Settlement | No operation-anchored production query for either request. | **BLOCKED** |
| Backup/recovery | No recent independent restore, approved encrypted destination/retention, or role/RLS completeness proof recorded. | **BLOCKED** |
| Authorization | No new hosted build, production SQL write, migration, Fly deploy, or database restore authorized by this task. | **NO-GO** |

### Operator handoff — evidence still unavailable

1. A Fly operator with read-only access should record the current machine image digest, immutable revision label, startup schema-guard result and effective database endpoint ID/name from a protected session. Do not publish the connection string or any secret; a stopped machine must not be started merely for this audit.
2. A Neon operator should use an approved protected read-only profile to verify the Fly endpoint-to-project/branch mapping, then compare the seven critical audit columns, six indexes and complete `__EFMigrationsHistory`. The November 10 application claim does not substitute for this current read.
3. For each original request, inspect the idempotency ledger, answer, audit, XP-event and stat records using the authorized account/session, UTC incident window and original operation identity. Keep IDs, keys, answers and PII out of shared output; retain `UNKNOWN` unless persisted evidence supports a result.
4. Record verified backup and isolated-restore evidence before any separately approved release. Keep issue #47 open; do not infer rollback from HTTP 500, telemetry or unanchored counts.
