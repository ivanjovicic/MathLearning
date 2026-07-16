# BACKEND-API-DB-019 — Replace silent startup catalog mutation with versioned cosmetics data ownership

Repository: `ivanjovicic/MathLearning`  
Queue: `docs/prompt_queues/backend_api_db_residuals_pass3_2026_07_16.md`  
Priority: P1 catalog integrity, startup truth and reward availability  
Run mode: catalog source-of-truth decision + migration/bootstrap/readiness tests  
Scope exclusion: do not edit `src/MathLearning.Admin/**`

## Problem evidence

- `Program.cs` calls `CosmeticStartupSeeder.EnsureCosmeticItemsAsync` during every API startup.
- The seeder embeds a large production catalog in raw SQL inside application source.
- Most rows use `ON CONFLICT DO NOTHING`, so corrected names/prices/assets/rules may never reach existing databases.
- Fragment rows use `ON CONFLICT DO UPDATE` and overwrite `FragmentLabel`, `FragmentsRequired`, `IsDefault` and `UnlockType` on every start.
- The seeder catches every exception and logs only a warning; the API continues even when required catalog rows are missing or invalid.
- Catalog/inventory/default-ownership and Daily Run fragment settlement depend on those rows/labels.

Expected invariant: cosmetics catalog data has one versioned, auditable source of truth. Startup never silently rewrites operator-approved mutable product data, and readiness cannot report healthy when required default/reward dependencies are absent.

## Deduplication / owner boundary

- `BACKEND-MIGRATION-001` owns historical cosmetics FK migration correctness.
- `BACKEND-API-DB-009` owns server-issued entitlements and server-priced purchases.
- `BACKEND-API-DB-015` owns pending economy/cosmetics operation recovery.
- This prompt owns catalog seed/deployment/version/audit/readiness behavior only.
- Preserve current Admin/content tooling contracts without editing the Blazor Admin project.

## Inspect first

- `src/MathLearning.Api/Program.cs`
- `src/MathLearning.Api/Startup/CosmeticStartupSeeder.cs`
- cosmetic entities/EF mappings/migrations/snapshot
- `CosmeticPlatformService.Public.cs`, reward/default ownership services and cache/version builders
- Daily Run chest/fragment settlement and server reward catalog
- schema/readiness health checks and database-validation workflow
- current cosmetics migration/entitlement/pending-operation prompts/tests

Maximum initial reads: 14 files. Search budget: 5 exact searches for seeded keys, fragment labels, defaults, catalog version and startup/readiness dependencies.

## Required decision before coding

Choose one canonical production catalog mechanism:

1. versioned idempotent data migrations with explicit upgrade steps; or
2. a versioned catalog manifest/import process with checksum/version/history and an explicit operator command/job.

Do not retain two competing production owners. Application startup may verify required catalog state, but it must not be the silent product-data editor.

Create a matrix before edits:

```text
catalog field -> immutable identity | deploy-managed value | operator-managed value -> update owner -> conflict behavior -> audit/version record
```

At minimum classify key, name, category, rarity, asset paths/version, unlock type/condition, coin price, default flag, fragment label/requirement, active/hidden state, release/retirement and season association.

## Required implementation

1. Remove unconditional production mutation from normal API startup or restrict it to an explicit safe bootstrap mode for empty Development/Test databases.
2. Add one catalog version/checksum/history record that identifies exactly which catalog revision is installed.
3. Make clean-database and upgraded-database installation deterministic. Do not use `NOW()` where reproducible release data requires a stable authored timestamp.
4. Preserve operator-managed fields during deployment. A versioned migration/import must update only fields explicitly owned by that revision and record the change.
5. Define rename/retirement behavior. Stable keys referenced by inventory/reward history must not be deleted or silently repurposed.
6. Validate required defaults and every server-issued fragment/reward reference:
   - required default categories exist;
   - fragment label maps to exactly one active item with valid requirement;
   - shop/reward items have coherent active/hidden/release/price/unlock fields;
   - asset/version metadata is present according to the mobile contract.
7. Add a readiness/capability check. Missing required catalog state must return a stable unhealthy/degraded reason or disable cosmetics settlement truthfully; warning-only startup success is insufficient.
8. Cache invalidation/catalog version must change when deploy-managed data changes and remain stable when nothing changes.
9. Make the bootstrap/import multi-replica safe with database transaction/advisory lock/version uniqueness or an explicitly single-owner deployment step.
10. Log only catalog version/checksum and bounded validation categories, not entire payloads or sensitive configuration.
11. Coordinate migration order with BACKEND-MIGRATION-001 and provider schema validation. Never edit old applied migrations destructively.
12. Update architecture, endpoint inventory/readiness docs and any deployment runbook.

## Failure-mode matrix

- clean PostgreSQL database;
- upgraded database with all historical migrations;
- operator changes a mutable field, then API restarts;
- source manifest changes a deploy-managed field;
- stable key is renamed/removed/reused;
- required default item is absent/inactive/hidden;
- Daily Run fragment label has zero or multiple matches;
- item price/unlock type is contradictory;
- two API replicas/bootstrap jobs start concurrently;
- import fails halfway;
- older app version starts after newer catalog revision;
- database catalog is newer than application-supported revision;
- cache contains old version while DB update commits;
- readiness checks schema but not required catalog data.

## Required tests

### Data/version tests

- clean install produces exact deterministic version/checksum and required item set;
- upgraded install converges to the same expected revision without duplicate rows;
- reapplying the same revision is a no-op;
- operator-managed field survives ordinary API restart/deployment;
- explicit deploy-managed update changes only reviewed fields and writes history;
- old/stable inventory references remain valid after retirement/rename policy;
- no applied historical migration is modified.

### Dependency/readiness tests

- every server-issued Daily Run/reward fragment label resolves exactly once;
- default ownership can initialize every required category;
- missing/invalid catalog returns unhealthy/degraded capability and cannot perform misleading settlement;
- catalog cache/version invalidates once after successful update and not on no-op startup;
- startup verification failure is safe and bounded.

### Concurrency/provider tests

- two concurrent bootstrap/import attempts result in one installed revision;
- fail-after-SQL rollback leaves no partial visible revision;
- PostgreSQL advisory/unique/transaction behavior is proven;
- database newer/older than supported application version follows the documented compatibility rule.

## Validation

```text
dotnet test tests/MathLearning.Tests/MathLearning.Tests.csproj -c Release --filter "FullyQualifiedName~CosmeticCatalog|FullyQualifiedName~CosmeticStartup|FullyQualifiedName~DailyRunFragment|FullyQualifiedName~DatabaseSchema"
dotnet build MathLearning.slnx -c Release
dotnet ef migrations has-pending-model-changes --project src/MathLearning.Infrastructure/MathLearning.Infrastructure.csproj --startup-project src/MathLearning.Api/MathLearning.Api.csproj --context ApiDbContext
```

Run both fresh and upgraded PostgreSQL migration/catalog validation. Record exact installed version/checksum and compare required-key/fragment invariants. A SQLite/InMemory test alone is not sufficient.

## Owned paths

- cosmetics catalog production source-of-truth/version/history;
- startup verification/bootstrap-mode boundary;
- required catalog readiness/capability validation;
- catalog deployment/cache invalidation tests and docs.

## Avoid paths / non-goals

- Blazor Admin UI/pages;
- cosmetic entitlement/purchase/idempotency redesign;
- pending-operation recovery;
- historical FK migration repair owned by BACKEND-MIGRATION-001;
- broad visual/product catalog redesign;
- silently overwriting prices, names or unlock rules at API startup;
- treating a warning log as readiness proof.

## Stop / handoff conditions

Stop for product/operations review if field ownership (deploy-managed vs operator-managed) cannot be established. Preserve data and mark the prompt blocked rather than guessing overwrite rules. Stop and split if asset publishing/CDN versioning requires a separate deployment system.

## Completion gate

No Done while normal production startup mutates catalog data, required catalog failures are warning-only, clean/upgraded databases diverge, operator fields are overwritten, fragment/default invariants lack PostgreSQL proof, or catalog version/cache/readiness docs disagree. Done requires verified main delivery, fresh+upgrade provider evidence and synchronized queue/run log.

Evidence: `.ai/runs/<date>-BACKEND-API-DB-019-evidence.md`