# BACKEND-TEST-017 Evidence

Prompt ID: BACKEND-TEST-017
Queue: `docs/prompt_queues/backend_test_coverage.md`
Agent/tool: ChatGPT via GitHub connector
Model provider: OpenAI
Model name/id: GPT-5.5 Thinking
Run mode: relational HTTP registration atomicity tests
Started from queue status: new P0 gap found during coverage audit

## Goal

Prove `/auth/mobile/register` is atomic on a relational provider when failure occurs after Identity/Profile/RefreshToken SQL has been issued, and prove retry creates exactly one account with one welcome grant.

## Guardrails

- Invoke the real HTTP endpoint.
- Use file-backed SQLite and fresh verification scopes.
- Inject failure after `SaveChanges`, before transaction commit.
- Assert no orphan Identity user, profile, refresh token, or doubled welcome coins.

## Files inspected

- `src/MathLearning.Api/Endpoints/AuthEndpoints.cs`
- `tests/MathLearning.Tests/Endpoints/AuthMobileRegistrationAtomicityTests.cs`
- `tests/MathLearning.Tests/Helpers/CustomWebApplicationFactory.cs`

## Validation

Pending implementation. No executable .NET environment is available in this connector session.

## Completion

15%
