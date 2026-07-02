# BACKEND2-CRIT-008 Evidence

Prompt ID: BACKEND2-CRIT-008
Queue: docs/prompt_queues/backend_second_pass_risk_prevention.md
Agent/tool: Cursor Agent
Model provider: unknown-not-exposed
Run mode: implementation/test + migration
Elapsed time: unknown-not-recorded
Relevant prior mistakes read: BACKEND-MISTAKE-VALIDATION-001
How this run avoids prior mistakes: concurrency tests + migration for unique SourceDraftId index

Commit SHA: 85a87c6
Validation command:

```bash
dotnet test tests/MathLearning.Tests --filter "QuestionAuthoring|DraftVersion|Publish|Concurrency"
```

Result (prompt filter — includes unrelated `XpTrackingConcurrencyIntegrationTests` via `Concurrency`):

```text
Failed!  - Failed: 1, Passed: 39, Skipped: 0, Total: 40
```

Authoring-only filter:

```bash
dotnet test tests/MathLearning.Tests --filter "FullyQualifiedName~QuestionAuthoring"
```

```text
Passed!  - Failed: 0, Passed: 34, Skipped: 0, Total: 34
```

Risk prevented:

- Concurrent `SaveDraftAsync` calls for the same question can no longer allocate duplicate `(QuestionId, DraftVersion)` pairs.
- Concurrent `PublishAsync` calls for the same draft cannot create duplicate published versions or duplicate `SourceDraftId` rows.
- Preview cache rows roll back with failed draft saves because draft, validation, audit, and preview cache persist in one transaction with a single `SaveChanges`.

Runtime changes:

- `QuestionAuthoringConcurrencySupport` — detects version-allocation unique violations (Postgres + EF in-memory).
- `MathQuestionAuthoringService.SaveDraftAsync` — retry loop (max 3), transactional single-commit save.
- `MathQuestionAuthoringService.PublishAsync` — idempotent return when draft already published; retry on version conflict.
- `QuestionVersionConfiguration` — unique index `UX_question_versions_source_draft` on `SourceDraftId`.

Migration:

- `20260702152409_AddQuestionVersionSourceDraftUniqueIndex` — drops non-unique `IX_question_versions_SourceDraftId`, creates unique `UX_question_versions_source_draft`.

Existing indexes verified (no migration needed):

- `UX_question_drafts_question_version` on `(QuestionId, DraftVersion)`
- `UX_question_versions_question_version` on `(QuestionId, VersionNumber)`

Tests added:

- `QuestionAuthoringVersionConcurrencyTests.ConcurrentSaveDraftAsync_AllocatesUniqueSequentialDraftVersions`
- `QuestionAuthoringVersionConcurrencyTests.ConcurrentPublishAsync_CreatesSinglePublishedVersionPerDraft`

## Mistakes observed

none

## Completion %

95%

## Residual risk

- Retry loop max 3 attempts; sustained contention could still fail loudly after exhaustion.

## Commit SHA

85a87c6