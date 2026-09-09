# BACKEND-TEST-049 - Question authoring snapshot truth and atomic revalidate repair

Priority: P1
Status: Done 75% — Run log: `.ai/runs/2026-08-01-BACKEND-TEST-049-evidence.md`; Validation: QuestionAuthoringSnapshotTruth 3/3 + QuestionAuthoring|QuestionVersionConcurrency|QuestionEditorUiSmoke; Residual risk: UpdateQuestion split SaveChanges and PG concurrency matrix deferred; Commit: self
Run mode: question-authoring integrity investigation + relational failure-injection tests

## Problem

`QuestionAuthoringService.CreateQuestionSnapshot` only captures a subset of the authored question state. The snapshot omits fields such as hint metadata, render/format choices, semantics alt text, translation rows, publish metadata and deletion flags, so `PreviousSnapshotJson` cannot reconstruct the full prior authoring state.

`MathQuestionAuthoringService.CreateDraftFromQuestionAsync` also persists a rebuilt draft and current-draft pointer before the validation result/cache are durably saved. A failure after the first `SaveChangesAsync` can leave a live draft pointer without a matching validation result or cache row.

This is a distinct authoring-history and repair-truth bug, not just a generic validation issue.

## Risks

- question history and audit snapshots can lie about the actual authored content;
- revalidate can leave the current draft pointer ahead of durable validation state;
- preview cache may point at content that never finished committing;
- rollback or compare-diff tooling cannot recover the true previous question shape.

## Inspect first

- `src/MathLearning.Infrastructure/Services/QuestionAuthoring/QuestionAuthoringService.cs`
- `src/MathLearning.Infrastructure/Services/QuestionAuthoring/MathQuestionAuthoringService.cs`
- `src/MathLearning.Infrastructure/Persistance/Configurations/QuestionAuthoringConfigurations.cs`
- `src/MathLearning.Domain/Entities/Question.cs`
- `tests/MathLearning.Tests/Services/QuestionAuthoringPipelineTests.cs`
- `tests/MathLearning.Tests/Services/QuestionAuthoringVersionConcurrencyTests.cs`
- `tests/MathLearning.Tests/Endpoints/QuestionAuthoringEndpointsIntegrationTests.cs`
- `docs/BACKEND_SECOND_PASS_RISK_PREVENTION_RULES.md`
- `docs/backend_contract_gap_report.md`

## Required investigation

1. Decide the canonical authored-history shape for questions.
2. Either persist a full immutable authored payload or expand the snapshot to include every field needed to reconstruct prior question state.
3. Make revalidate/create-draft/update flows atomic enough that a failure after draft creation cannot leave the current pointer or preview cache ahead of durable validation state.
4. Remove or justify any split `SaveChanges` pattern that can produce partially visible authoring state.
5. Preserve current mobile/admin contract behavior while fixing the backend history truth.

## Required tests

- snapshot of a question with translations, hints, formats, render modes and semantics alt text round-trips the full authored state;
- revalidate failure after draft creation leaves no current-draft pointer pointing at an incomplete commit;
- preview cache is absent or rolled back when the durable authoring transaction fails;
- repeated revalidate does not silently lose history rows or create partial state;
- concurrent save/publish/revalidate keeps current pointers and audit rows aligned.

## Validation

```text
dotnet test tests/MathLearning.Tests/MathLearning.Tests.csproj --filter "QuestionAuthoring|QuestionVersionConcurrency|QuestionEditorUiSmoke"
```

