# BACKEND-TEST-049 Evidence

Evidence format: v2
Prompt ID: BACKEND-TEST-049
Queue: user-assigned
Agent/tool: unknown-not-exposed
Model provider: unknown-not-exposed
Model name/id: unknown-not-exposed
Client/IDE: unknown-not-exposed
Run mode: known-fix
Token budget: medium
Started at UTC: 2026-08-01T07:21:42Z
Completed at UTC: 2026-08-01T07:25:31Z
Elapsed time: 3m 49s
Relevant prior mistakes read: BACKEND-MISTAKE-EVIDENCE-001, BACKEND-MISTAKE-VALIDATION-001, BACKEND-MISTAKE-CONTENT-001, BACKEND-MISTAKE-CONTENT-002
How this run avoids prior mistakes: apply BACKEND-MISTAKE-EVIDENCE-001; apply BACKEND-MISTAKE-VALIDATION-001; apply BACKEND-MISTAKE-CONTENT-001; apply BACKEND-MISTAKE-CONTENT-002
Owner/hypothesis: QuestionAuthoringService snapshot + MathQuestionAuthoringService draft create; falsifier=snapshot omits authored fields or revalidate leaves CurrentDraftId without validation/cache
Files inspected: 10
Files changed: 7
Searches: 4
Validation runs: 2
Failed retries: 0

## Outcome
- CreateQuestionSnapshot now captures hints, formats, render modes, semantics, publish/delete metadata and translations.
- CreateDraftFromQuestionAsync stages draft pointer, validation and preview cache in one SaveChanges; failure leaves no partial pointer.
- Focused QuestionAuthoringSnapshotTruth 3/3 and broader authoring filter 72/72 green.

## Changed paths
- src/MathLearning.Infrastructure/Services/QuestionAuthoring/QuestionAuthoringService.cs
- src/MathLearning.Infrastructure/Services/QuestionAuthoring/MathQuestionAuthoringService.cs
- tests/MathLearning.Tests/Services/QuestionAuthoringSnapshotTruthTests.cs
- docs/prompt_queues/BACKEND-TEST-049-question-authoring-history-truth.md
- docs/prompt_queues/backend_test_coverage.md
- docs/prompt_queues/README.md
- .ai/runs/2026-08-01-BACKEND-TEST-049-evidence.md

## Validation
Validation run: dotnet test ... --filter QuestionAuthoringSnapshotTruth => 3/3; filter QuestionAuthoring|QuestionVersionConcurrency|QuestionEditorUiSmoke => 72/72
Validation not run: none - UpdateQuestion split SaveChanges and PostgreSQL concurrency deferred

## Exceptions and learning
Mistakes observed: none
Waste: none
Missed: UpdateQuestionAsync still uses split SaveChanges; PG concurrent revalidate matrix
Follow-up: BACKEND-TEST-049 residual UpdateQuestion atomicity / PG concurrency
Residual risk: UpdateQuestion split SaveChanges can still leave partial option/step state; concurrent revalidate not PostgreSQL-proven.
Documentation impact: updated docs/prompt_queues/BACKEND-TEST-049-question-authoring-history-truth.md, backend_test_coverage.md, README.md
Cross-repo impact: no - admin/authoring backend history only

## Delivery
State: Needs merge
Branch/PR: cursor/backend-test-049-authoring-snapshot-fa87
Commit SHA: self
Completion %: 75
