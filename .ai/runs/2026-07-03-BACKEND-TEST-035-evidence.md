# BACKEND-TEST-035 Evidence

Prompt ID: BACKEND-TEST-035
Queue: `docs/prompt_queues/backend_test_followups_2026_07_03.md`
Agent/tool: ChatGPT via GitHub connector
Model provider: OpenAI
Model name/id: GPT-5.5 Thinking
Run mode: test-auth infrastructure regression tests
Started from queue status: Prompt-ready

## Goal

Directly prove the three `TestAuthHandler` contracts used by endpoint security tests: compatibility default principal, explicit anonymous no-result, and explicit user/role claims.

## Planned tests

- no test headers authenticates the historical `test-user` compatibility principal;
- `X-Test-Anonymous: true` returns no authentication result;
- `X-Test-UserId` and comma-separated `X-Test-Roles` produce the expected user and role claims.

## Validation

In progress. No executable .NET environment is available in this connector session.

## Completion

10%
