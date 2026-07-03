# BACKEND-TEST-018 Evidence

Prompt ID: BACKEND-TEST-018
Queue: `docs/prompt_queues/backend_test_coverage.md`
Agent/tool: ChatGPT via GitHub connector
Model provider: OpenAI
Model name/id: GPT-5.5 Thinking
Run mode: relational offline-batch atomicity tests
Started from queue status: new P0 gap found during coverage audit

## Goal

Prove `/api/quiz/offline-submit` leaves no partial session, answer, audit, stat, XP event, or profile XP when failure occurs after relational `SaveChanges` but before transaction commit.

## Guardrails

- Invoke the real authenticated endpoint.
- Use file-backed SQLite and a failure injected from `SavedChangesAsync`.
- Assert durable state from a fresh scope.
- Assert internal failure details do not leak to the client.

## Files inspected

- `src/MathLearning.Api/Endpoints/QuizEndpoints.cs`
- `src/MathLearning.Api/Endpoints/ApiDbTransactionHelpers.cs`
- `tests/MathLearning.Tests/Endpoints/OfflineBatchSubmitCompatibilityTests.cs`
- `tests/MathLearning.Tests/Helpers/CustomWebApplicationFactory.cs`

## Validation

Pending implementation. No executable .NET environment is available in this connector session.

## Completion

15%
