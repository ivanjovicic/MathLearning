# BACKEND-TEST-019 Evidence

Prompt ID: BACKEND-TEST-019
Queue: `docs/prompt_queues/backend_test_coverage.md`
Agent/tool: ChatGPT via GitHub connector
Model provider: OpenAI
Model name/id: GPT-5.5 Thinking
Run mode: relational service tests
Started from queue status: new P0/P1 coverage gap found by BACKEND-TEST-AUDIT-002

## Goal

Add direct relational coverage for `QuizAttemptIngestService`: empty input, durable attempt writes, topic/subtopic aggregation, accumulation into existing statistics, input normalization, scheduler ordering, and rollback after SQL has been issued.

## Relevant prior mistakes read

- BACKEND-MISTAKE-EVIDENCE-001
- BACKEND-MISTAKE-AUDIT-001
- BACKEND-MISTAKE-VALIDATION-001

## Guardrails

- Use SQLite relational transactions rather than EF InMemory for rollback proof.
- Assert durable state from a fresh context.
- Inject failure from `SavedChangesAsync` so SQL has already executed inside the transaction.
- Assert the weakness-analysis scheduler is called only after successful commit.
- Do not claim current ingest is idempotent; that is a separate confirmed gap.

## Files inspected

- `src/MathLearning.Api/Services/QuizAttemptIngestService.cs`
- `src/MathLearning.Api/Services/WeaknessAnalysisScheduler.cs`
- `src/MathLearning.Domain/Entities/WeaknessAnalysisModels.cs`
- `src/MathLearning.Infrastructure/Persistance/ApiDbContext.cs`
- `tests/MathLearning.Tests/Helpers/TestDbContextFactory.cs`

## Validation

Implementation in progress. No executable .NET environment is available in this connector session.

## Completion

15%
