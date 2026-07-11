# Backend Failing-Test Follow-up Queue — 2026-07-11

Target repo: `ivanjovicic/MathLearning`  
Source run: `.ai/runs/2026-07-11-BACKEND-FAILING-TESTS-001-evidence.md`  
Reviewed branch head before queue publication: `f4f8fd175f5beb9d7e1eb5b6f74376401f5831c6`

## Result of the failing-test repair pass

The backend test project is green after the provider-aware test/runtime repairs from `BACKEND-FAILING-TESTS-001`:

- focused previously failing group: 72 passed, 0 failed;
- complete test project: 995 passed, 0 failed;
- Release build: passed.

One independent workflow blocker remains. It is not a unit/integration-test failure and must not be hidden by weakening or skipping the schema gate.

## Active prompt

| ID | Priority | Status | Purpose |
|---|---:|---|---|
| BACKEND-MIGRATION-001 | P0 | Prompt-ready | Repair the historical cosmetics FK-name mismatch so clean PostgreSQL schema validation, upgraded-database compatibility, idempotent migration generation and startup smoke all pass. |

---

## BACKEND-MIGRATION-001 — Repair cosmetics migration FK-name drift safely

Priority: P0  
Run mode: migration-safety-first, PostgreSQL validation  
Canonical provider prerequisite: linked to `BACKEND-TEST-032`; do not create a competing general PostgreSQL fixture/lane implementation.

### Confirmed failure

A clean PostgreSQL migration chain fails in `20260624133144_AlignCosmeticsMobileDataModel` with PostgreSQL error `42704` because it tries to drop the hard-coded constraint:

```text
FK_user_avatar_configs_UserProfiles_UserId
```

The earlier migration `20260309091241_AddCosmeticSystem` created `user_avatar_configs` and its foreign keys through raw PostgreSQL SQL without explicit matching EF constraint names. PostgreSQL therefore generated different names. The later migration assumes EF-generated names and cannot apply to a clean database.

### Goal

Make the complete migration chain safe for both:

1. a brand-new empty PostgreSQL database; and
2. databases that already applied the cosmetics migrations under either provider-generated or EF-style constraint names.

Do not change mobile payloads, cosmetics semantics, deletion behavior or public API contracts.

### Read first

- `AGENTS.md`
- `docs/AGENT_SHARED_OPERATING_STANDARD.md`
- `docs/AGENT_RUN_LOG_ENFORCEMENT.md`
- `docs/BACKEND_CRITICAL_RISK_PREVENTION_RULES.md`
- `.ai/runs/2026-07-11-BACKEND-COVERAGE-EXPANSION-001-evidence.md`
- `.ai/runs/2026-07-11-BACKEND-FAILING-TESTS-001-evidence.md`
- `src/MathLearning.Infrastructure/Migrations/Api/20260309091241_AddCosmeticSystem.cs`
- `src/MathLearning.Infrastructure/Migrations/Api/20260624133144_AlignCosmeticsMobileDataModel.cs`
- both corresponding designer files and `ApiDbContextModelSnapshot.cs`
- `scripts/db/validate-schema.ps1`
- `.github/workflows/database-validation.yml`

### Required work

1. Create `.ai/runs/<date>-BACKEND-MIGRATION-001-evidence.md` before modifying migrations.
2. Record the exact starting `main` SHA and the failing Database Validation run/job/error.
3. Inspect `pg_constraint`, table definitions and the generated migration SQL to enumerate every FK/PK/index name assumed by `AlignCosmeticsMobileDataModel`, not only the first failing constraint.
4. Choose the smallest safe strategy and document why:
   - explicitly name the raw-SQL constraints in the earlier historical migration so future clean databases match the later migration; or
   - replace brittle hard-coded drops in the later migration with PostgreSQL-safe conditional/introspective SQL that identifies the intended constraints by table, referenced table and columns;
   - use a combination only when required for both clean and upgraded paths.
5. Do not merely add a new later migration if a clean database cannot reach it because the earlier chain already fails.
6. Preserve exact FK delete actions and uniqueness semantics when constraints are recreated.
7. Add migration/schema regression coverage that fails on the original mismatch and proves the selected correction.
8. Validate a clean database from zero with PostgreSQL 16:

```text
pwsh ./scripts/db/validate-schema.ps1 \
  -ConnectionString "Host=localhost;Port=5432;Username=postgres;Password=postgres;Database=postgres;Pooling=true;" \
  -DatabaseName "mathlearning_schema_validation"
```

9. Validate an upgraded-path fixture representing a database immediately before `AlignCosmeticsMobileDataModel`, including the actual provider-generated constraint names. Apply the remaining migrations and verify the final schema.
10. Generate and inspect the idempotent artifact:

```text
dotnet ef migrations script --idempotent \
  --project src/MathLearning.Infrastructure \
  --startup-project src/MathLearning.Api \
  --context ApiDbContext \
  --output artifacts/migrations/api-idempotent.sql
```

11. Run:

```text
dotnet restore MathLearning.slnx
dotnet build MathLearning.slnx -c Release --no-restore
dotnet test tests/MathLearning.Tests/MathLearning.Tests.csproj -c Release --no-build
```

12. Run the startup schema/readiness smoke used by `Database Validation`.
13. Re-run the exact GitHub `Database Validation` workflow against the final commit and record:
    - clean schema result;
    - full test count;
    - coverage artifact presence;
    - idempotent SQL artifact presence;
    - startup smoke result.
14. Update the central queue and evidence with the final commit SHA and workflow run ID.

### Safety constraints

- Do not disable, skip or convert the schema-from-zero gate into a warning.
- Do not drop constraints by name alone unless their existence and identity are checked.
- Do not use broad `DROP CONSTRAINT` loops that can remove unrelated user/profile/cosmetics constraints.
- Do not rewrite production migration history without proving already-upgraded database behavior.
- Do not claim PostgreSQL correctness from SQLite or InMemory tests.
- Do not regenerate the entire migration history or snapshot as a shortcut.

### Completion criteria

Complete only when all of the following are true for the exact final commit:

- clean PostgreSQL migration chain passes;
- upgraded-path migration passes;
- final FK/PK/index/delete-action assertions pass;
- Release build passes;
- all backend tests pass;
- idempotent migration artifact is generated and reviewed;
- startup schema/readiness smoke passes;
- GitHub `Database Validation` is green and linked from evidence.

### Expected evidence

The run log must include:

- original and corrected constraint inventory;
- clean and upgraded database commands/results;
- generated SQL artifact path and review notes;
- exact test count;
- workflow run ID and final commit SHA;
- residual risks and rollback notes;
- explicit confirmation that no mobile contract sync was required.
