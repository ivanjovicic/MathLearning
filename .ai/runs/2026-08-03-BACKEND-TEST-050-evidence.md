# BACKEND-TEST-050 Evidence

Evidence format: v2
Prompt ID: BACKEND-TEST-050
Queue: user-assigned
Agent/tool: unknown-not-exposed
Model provider: unknown-not-exposed
Model name/id: unknown-not-exposed
Client/IDE: unknown-not-exposed
Run mode: known-fix
Token budget: medium
Started at UTC: 2026-08-03T08:54:07Z
Completed at UTC: 2026-08-03T09:04:38Z
Elapsed time: 10m 31s
Relevant prior mistakes read: BACKEND-MISTAKE-EVIDENCE-001, BACKEND-MISTAKE-VALIDATION-001, BACKEND-MISTAKE-CONTENT-001, BACKEND-MISTAKE-CONTENT-002
How this run avoids prior mistakes: apply BACKEND-MISTAKE-EVIDENCE-001; apply BACKEND-MISTAKE-VALIDATION-001; apply BACKEND-MISTAKE-CONTENT-001; apply BACKEND-MISTAKE-CONTENT-002
Owner/hypothesis: DesignTokenVersionManager.CreateDraftVersionIdentity + EnsureDraftVersionAsync; falsifier=same-second drafts collide on UX_DesignTokenVersion_Version
Files inspected: 10
Files changed: 7
Searches: 4
Validation runs: 2
Failed retries: 1

## Outcome
- Draft versions use draft-{yyyyMMddHHmmss}-{8hex} within Version max length 32.
- EnsureDraftVersionAsync retries on unique conflicts and reuses an existing Draft.
- DesignTokenDraftVersion focused tests 4/4 green.

## Changed paths
- src/MathLearning.Application/Services/DesignTokenServices.cs
- src/MathLearning.Infrastructure/Services/DesignTokens/DesignTokenVersionManager.cs
- src/MathLearning.Infrastructure/Services/DesignTokens/DesignTokenPlatformService.cs
- tests/MathLearning.Tests/Services/DesignTokenDraftVersionTests.cs
- docs/prompt_queues/BACKEND-TEST-050-design-token-draft-version-race.md
- docs/prompt_queues/backend_test_coverage.md
- docs/prompt_queues/README.md
- .ai/runs/2026-08-03-BACKEND-TEST-050-evidence.md

## Validation
Validation run: dotnet test ... --filter FullyQualifiedName~DesignTokenDraftVersion => 4/4; filter DesignToken|DesignTokens|AdminTokens => 4/4
Validation not run: none - filtered unique Draft index and Upsert token-replace concurrency deferred

## Exceptions and learning
Mistakes observed: none
Waste: none
Missed: unique filtered index on Status=Draft; UpsertDraft token-replace concurrency under InMemory/SQLite
Follow-up: BACKEND-TEST-051 design-token bootstrap race
Residual risk: Two concurrent creators can still insert two Draft rows with distinct versions until a unique Draft status constraint exists.
Documentation impact: updated docs/prompt_queues/BACKEND-TEST-050-design-token-draft-version-race.md, backend_test_coverage.md, README.md
Cross-repo impact: no

## Delivery
State: Needs merge
Branch/PR: cursor/backend-test-050-design-token-version-fa87
Commit SHA: self
Completion %: 75
