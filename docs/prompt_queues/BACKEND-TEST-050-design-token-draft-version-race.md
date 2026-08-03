# BACKEND-TEST-050 - Design-token draft version identity and collision safety

Priority: P1
Status: Done 75% — Run log: `.ai/runs/2026-08-03-BACKEND-TEST-050-evidence.md`; Validation: DesignTokenDraftVersion 4/4 + DesignToken|DesignTokens|AdminTokens; Residual risk: filtered unique Draft status index and full Upsert concurrency matrix deferred; Commit: self
Run mode: design-token versioning investigation + relational concurrency tests

## Problem

`DesignTokenPlatformService.EnsureDraftVersionAsync` generates draft versions with `draft-{DateTime.UtcNow:yyyyMMddHHmmss}`. The `DesignTokenVersion` model enforces a unique `Version`, but the timestamp only has second resolution, so two draft creations in the same second can collide.

There is no visible retry or lease around this allocation path, so rapid admin edits or automation can fail on a unique-constraint race instead of creating distinct drafts.

## Risks

- concurrent draft creation can fail nondeterministically;
- publish/rollback flows may point at the wrong draft identity;
- version strings stop being reliable identifiers for a single admin action;
- UI or audit tooling may misread the latest draft after a collision retry.

## Inspect first

- `src/MathLearning.Infrastructure/Services/DesignTokens/DesignTokenPlatformService.cs`
- `src/MathLearning.Infrastructure/Services/DesignTokens/DesignTokenVersionManager.cs`
- `src/MathLearning.Infrastructure/Persistance/Configurations/DesignTokenVersionConfiguration.cs`
- `src/MathLearning.Api/Controllers/AdminTokensController.cs`
- `src/MathLearning.Domain/Entities/DesignTokenVersion.cs`
- related design-token endpoint/service tests
- current design-token docs and queue ownership

## Required investigation

1. Decide whether draft versions are human-readable labels or durable unique identities.
2. Replace timestamp-only draft version generation with a transaction-safe allocator or a separate revision identity field.
3. Preserve the current unique index semantics or add a new one only with documented migration/test proof.
4. Ensure publish/rollback cannot accidentally reuse or collide with draft identities.
5. If time-based readability is desired, add a suffix that stays unique under concurrency and remains bounded.

## Required tests

- two rapid `UpsertDraftAsync` calls create distinct draft identities;
- concurrent draft creation does not throw unique-constraint exceptions;
- publish and rollback still select the intended draft/version;
- version display remains stable for admin/UI consumers;
- no duplicate current-version row appears after concurrent admin operations.

## Validation

```text
dotnet test tests/MathLearning.Tests/MathLearning.Tests.csproj --filter "DesignToken|DesignTokens|AdminTokens"
```

