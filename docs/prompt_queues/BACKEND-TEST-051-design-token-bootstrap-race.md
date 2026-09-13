# BACKEND-TEST-051 - Design-token bootstrap race and startup ownership

Priority: P1
Status: Prompt-ready
Run mode: design-token bootstrap investigation + startup/concurrency tests

## Problem

`DesignTokenPlatformService.EnsureInitializedAsync` seeds the first current design-token version when no current row exists, but it does not claim startup ownership. On a cold or freshly restored database, two API replicas can both observe an empty state and race to insert the initial `1.0.0` current version.

The only visible guard is a unique current-version index, so the loser can fail with a startup-time uniqueness exception instead of cleanly no-oping. That makes design-token bootstrap nondeterministic under multi-replica startup.

## Risks

- first boot or restore can fail depending on replica timing;
- multiple nodes can compete to create the same initial current version;
- startup may report an error even though one valid bootstrap already succeeded;
- operators cannot safely bring up more than one instance against an empty catalog.

## Inspect first

- `src/MathLearning.Infrastructure/Services/DesignTokens/DesignTokenPlatformService.cs`
- `src/MathLearning.Infrastructure/Persistance/Configurations/DesignTokenVersionConfiguration.cs`
- `src/MathLearning.Api/Program.cs`
- `src/MathLearning.Infrastructure/Services/DesignTokens/DesignTokenVersionManager.cs`
- `docs/BACKEND_COLD_START_BUDGET.md`
- related design-token endpoint/service tests

## Required investigation

1. Decide whether initial design-token bootstrap must be single-owner, retryable or fully idempotent.
2. Add a concurrency-safe startup claim or startup retry path for empty databases.
3. Preserve the current public read behavior after one bootstrap succeeds.
4. Keep the startup path bounded so an already-initialized database remains fast.
5. Document the bootstrap ownership contract in the queue/docs if an explicit operator step is chosen.

## Required tests

- two concurrent empty-database initializations create exactly one current version;
- bootstrap is a no-op when a current version already exists;
- startup failure does not leave a partial current-version row behind;
- cached/current-token reads still work after bootstrap;
- initialization stays bounded for already-seeded databases.

## Validation

```text
dotnet test tests/MathLearning.Tests/MathLearning.Tests.csproj --filter "DesignToken|DesignTokens|AdminTokens|Startup"
```

