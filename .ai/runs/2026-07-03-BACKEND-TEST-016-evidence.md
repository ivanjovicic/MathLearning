# BACKEND-TEST-016 Evidence

Prompt ID: BACKEND-TEST-016
Queue: `docs/prompt_queues/backend_test_coverage.md`
Agent/tool: ChatGPT via GitHub connector
Model provider: OpenAI
Model name/id: GPT-5.5 Thinking
Run mode: relational transaction-helper tests
Started from queue status: new P0 gap found during coverage audit

## Goal

Protect `ApiDbTransactionHelpers.ExecuteWithSerializableRetryAsync` directly: commit, rollback after SQL was issued, retry after EF concurrency conflict, retry exhaustion, and cancellation.

## Guardrails

- Use SQLite relational provider, not EF InMemory, for transaction proof.
- Assert durable state from a fresh context.
- Avoid sleeps and timing-based races.
- Do not claim PostgreSQL provider equivalence.

## Files inspected

- `src/MathLearning.Api/Endpoints/ApiDbTransactionHelpers.cs`
- `src/MathLearning.Domain/Entities/ApplicationLog.cs`
- `docs/BACKEND_TEST_COVERAGE_STRATEGY.md`

## Validation

Pending implementation. No executable .NET environment is available in this connector session.

## Completion

15%
