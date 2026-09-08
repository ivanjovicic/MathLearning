# direct-user-request Evidence

Evidence format: v2
Prompt ID: direct-user-request
Queue: user-assigned
Agent/tool: Codex terminal
Model provider: OpenAI
Model name/id: GPT-5 Codex
Client/IDE: Codex
Run mode: known-fix
Token budget: medium
Started at UTC: 2026-09-08T12:59:00Z
Completed at UTC: 2026-09-08T13:04:00Z
Elapsed time: 5 minutes
Relevant prior mistakes read: BACKEND-MISTAKE-VALIDATION-001, BACKEND-MISTAKE-XREPO-001
How this run avoids prior mistakes: isolated clean worktree, exact migration owner, no production SQL execution, and explicit environment-proof classification.
Owner/hypothesis: the existing redrive column migration is empty or historical repair SQL was changed after application; a new explicit idempotent migration will repair drift before the worker runs.
Files inspected: 10
Files changed: 2
Searches: 4
Validation runs: 3
Failed retries: 1

## Outcome

- Added a forward-only EF migration that executes `ADD COLUMN IF NOT EXISTS "LastRedriveAttemptAtUtc" timestamp with time zone NULL`.
- Added a focused migration-script test preventing future omission of the column from generated deployment SQL.
- No production database was modified; deployment must apply the migration before the new binary is started.

## Changed paths

- `src/MathLearning.Infrastructure/Migrations/Api/20260908130000_EnsureSyncDeadLetterRedriveSchema.cs`
- `tests/MathLearning.Tests/Infrastructure/DatabaseSchemaValidationTests.cs`
- `.ai/runs/2026-09-08-direct-user-sync-deadletter-schema-evidence.md`

## Validation

Validation run: `dotnet test tests/MathLearning.Tests/MathLearning.Tests.csproj --filter FullyQualifiedName~RedriveSchemaRepairMigrationEmitsMissingColumnSql` -> blocked; restore failed with `There is not enough space on the disk`.
Validation run: `git diff --check` -> pass.
Validation not run: PostgreSQL integration/schema-from-zero validation -> not run - no free disk and no safe production database access.

## Exceptions and learning

Mistakes observed: BACKEND-MISTAKE-VALIDATION-001 new; prevention=check free disk before NuGet/build output
Waste: validation environment; one initial `--no-restore` attempt lacked generated assets
Missed: focused test execution and production migration application remain pending
Follow-up: operator/release owner must apply the generated migration to production and verify the column before deploying the worker
Residual risk: the code and migration are prepared, but production remains unverified until the reviewed migration is applied.
Documentation impact: none - `docs/database-migrations.md` already requires reviewed production migration application before binary deployment.
Cross-repo impact: yes - backend fixed; mobile repository was inspected only and not changed.

## Delivery

State: Needs validation
Branch/PR: `codex/fix-sync-deadletter-schema`; PR not created
Commit SHA: self
Completion %: 79
