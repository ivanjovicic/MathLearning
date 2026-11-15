# BACKEND-MIGRATION-002 - Validate critical audit column shapes at startup

Prompt contract: v2
Prompt admission: v3
Repository: ivanjovicic/MathLearning
Prompt ID: BACKEND-MIGRATION-002
Queue: docs/prompt_queues/backend_migration_shape_guard.md
Run lane: implementation
Token budget: medium
Timebox: 30 minutes; split before exceeding the limits

Task:

Extend `DatabaseSchemaVersionGuard` so `ValidateExact` rejects wrong PostgreSQL type, nullability, or character length for the seven critical `UserAnswerAudits` columns already checked for physical existence. It must report a bounded mismatch and must not alter the schema. Preserve the existing migration and model contract.

Problem evidence:

- `FindMissingCriticalObjectsAsync` in `DatabaseSchemaVersionGuard.cs` currently materializes only table/column names from `information_schema.columns` and accepts each expected column when its name is present.
- `20261101185317_RepairUserAnswerAuditXpSchema` validates types and selected lengths while applying, but a later same-name physical-schema change is not checked by that migration again.

Deduplication check:

- Current queue and completed migration evidence were checked; `BACKEND-MIGRATION-001` is archived and addressed constraint-name drift, not startup column shape.
- Open PR #50 concerns index-maintenance behavior and does not touch the schema guard or migration-drift tests.
- Open issue #47 owns diagnosis of a production quiz 500 and still lacks server-log evidence; this prompt requires no incident evidence and does not claim that production has this drift.

Priority rationale:

P2: startup can report `Ready` when a critical audit column has the right name but a shape incompatible with the compiled model. No current production occurrence is established.

Dependencies/collisions:

- No code owner collision was found in the current queue, merged migration repair, or open PR #50.
- Proof requires a disposable PostgreSQL database/schema or the repository PostgreSQL CI environment; production access is not a dependency.

Owner boundary:

The API startup schema guard owns the behavior. Infrastructure migration history and the EF model are reference contracts only and are outside this prompt's edit scope.

Queue placement:

`docs/prompt_queues/backend_migration_shape_guard.md`, one P2 `Ready` row after the current higher-priority database-operations queues.

Source of truth:

- `src/MathLearning.Api/Services/DatabaseSchemaVersionGuard.cs` owns startup readiness and currently checks these seven columns by name only.
- `src/MathLearning.Infrastructure/Persistance/ApiDbContext.cs`, `src/MathLearning.Domain/Entities/UserAnswerAudit.cs`, and `src/MathLearning.Infrastructure/Migrations/Api/20261101185317_RepairUserAnswerAuditXpSchema.cs` define the expected column shape.
- `tests/MathLearning.Tests/Infrastructure/MigrationDriftRegressionTests.cs` owns PostgreSQL migration-drift proof.
- `docs/migration-drift-issue-47.md` owns durable issue-47 schema-guard behavior documentation.

Interpretation before work:

The current guard can accept a same-name column with a type, length, or nullability that no longer matches the compiled schema after migration history is current. The migration validates existing shapes when applied, but does not detect later drift at startup. Current evidence is static; no live database is claimed as affected.

Ambiguity rule:

Use PostgreSQL catalog values and the compiled EF model/migration as authority. If their expected PostgreSQL representation differs, stop and record the exact mismatch for the owner; do not modify the model or migration in this prompt.

Risk/ownership model:

`DatabaseSchemaVersionGuard` owns this startup check. It may only report `Mismatch`; it must not auto-repair. Do not inspect or mutate production data. Do not change public API or quiz settlement behavior.

Failure-mode matrix:

- A valid migrated schema remains `Ready`.
- A wrong type, wrong bounded-string length, or nullable required column returns `Mismatch` with a safe column-specific reason.
- Nullable `ClientId` with its configured maximum length remains valid.
- Unrelated missing migration history and critical-index behavior remain unchanged.

Execution packet:

1. Read the four named source owners and the focused regression fixture; inspect at most 8 source/test files and search at most 8 targeted patterns.
2. Add a PostgreSQL-backed regression using an isolated test schema/database. First run it against the current guard and preserve the failing result for a wrong type and a required column made nullable.
3. Implement the smallest shape check, then rerun the same red cases plus a valid-schema case, a wrong-length case, and existing critical-index cases.
4. Run the focused migration-drift tests and the repository-required PostgreSQL/schema-from-zero validation; update the mapped issue-47 documentation and compact v2 evidence.
5. Stop after 30 minutes, 8 inspected source/test files, a second owner, or any need for production access or a migration/model change.

Owned paths:

- `src/MathLearning.Api/Services/DatabaseSchemaVersionGuard.cs`
- `tests/MathLearning.Tests/Infrastructure/MigrationDriftRegressionTests.cs`
- `docs/migration-drift-issue-47.md`
- `.ai/runs/<date>-BACKEND-MIGRATION-002-evidence.md`

Avoid paths:

- `src/MathLearning.Infrastructure/Migrations/**` and model/snapshot changes;
- production databases, deployment configuration, API contracts, quiz settlement and unrelated schema guards;
- generated documentation registry output.

Documentation impact:

Update `docs/migration-drift-issue-47.md` only if the implemented startup guard behavior changes; otherwise record a specific no-impact reason in the run log.

Acceptance criteria:

- The startup guard compares PostgreSQL `data_type`, `character_maximum_length`, and `is_nullable` against the seven existing critical-column expectations.
- A focused PostgreSQL regression is demonstrated failing before the guard change and passing afterward for wrong type and unexpected nullability; wrong length and valid schema are also covered.
- No schema mutation is introduced, the existing migration/model remains aligned, and current critical-index checks still pass.
- The migration-drift focused tests and required PostgreSQL schema-from-zero validation pass.

Proof required:

- Focused test-first PostgreSQL red/green output, including a valid schema and at least one counterexample per checked shape category.
- `dotnet ef migrations has-pending-model-changes` under the built configuration.
- Focused migration-drift tests plus PostgreSQL schema-from-zero/provider validation; record any unavailable proof and do not mark `Done`.
- Changed-documentation/evidence validators and exact `origin/main` verification after delivery.

Test-first contract:

- Pre-change proof: the isolated PostgreSQL regression must fail because the current guard reports `Ready` for a same-name wrong-type column and a required column made nullable.
- Post-change proof: the same focused test is expected to pass; those cases return `Mismatch` and a valid schema remains `Ready`.
- Counterexamples: nullable `ClientId` with length 128 remains accepted, while `Reason` with length 63 and a required boolean made nullable are rejected.
- Focused test command: `python scripts/run_guarded.py --timeout-seconds 180 -- dotnet test tests/MathLearning.Tests/MathLearning.Tests.csproj -c Release --filter FullyQualifiedName~MigrationDriftRegressionTests`
- Run the focused `MigrationDriftRegressionTests` and PostgreSQL schema-from-zero validation; no InMemory-only proof is accepted.

Validation:

- `python scripts/run_guarded.py --timeout-seconds 180 -- dotnet test tests/MathLearning.Tests/MathLearning.Tests.csproj -c Release --filter FullyQualifiedName~MigrationDriftRegressionTests`
- `python scripts/run_guarded.py --timeout-seconds 180 -- dotnet ef migrations has-pending-model-changes --configuration Release`
- The canonical PostgreSQL/schema-from-zero validation selected by `.ai/VALIDATION_SELECTOR.md`.
- `python scripts/check_documentation_health.py --full-links`, `python scripts/validate_agent_prompt.py --changed-from <base-sha>`, `python scripts/validate_agent_evidence.py --changed-from <base-sha> --verify-git`, `python scripts/analyze_agent_runs.py --changed-from <base-sha> --fail-on-regression`, and `python scripts/validate_agent_system.py`.

Completion gate:

Focused provider proof passes, compact evidence is synchronized, the bounded changes are committed and pushed to `origin/main`, and the exact resulting main SHA is recorded. CI is follow-up evidence, not a delivery gate.

Stop conditions:

Stop and split if the model or migration must change, another table/owner is required, production access is requested, or the red result cannot be isolated to the current guard.

Evidence:

Current audit evidence: `.ai/runs/2026-11-10-BACKEND-ANALYSIS-001-evidence.md`. The mismatch is classified as a static risk; this prompt owns its runtime proof and any fix.
