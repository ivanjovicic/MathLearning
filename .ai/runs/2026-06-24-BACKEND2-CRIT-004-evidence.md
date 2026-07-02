# BACKEND2-CRIT-004 Evidence

Prompt ID: BACKEND2-CRIT-004
Queue: docs/prompt_queues/backend_second_pass_risk_prevention.md
Agent/tool: Cursor Agent
Model provider: unknown-not-exposed
Model name/id: unknown-not-exposed
Client/IDE: Cursor
Run mode: implementation/test
Elapsed time: unknown-not-recorded
Relevant prior mistakes read: BACKEND-MISTAKE-AUDIT-001, BACKEND-MISTAKE-EVIDENCE-001
How this run avoids prior mistakes: authorization integration tests before Done

Commit SHA: 85a87c6
Validation command:

```bash
dotnet test tests/MathLearning.Tests/MathLearning.Tests.csproj --filter "QuestionAuthoring|Authorization|Admin"
```

Result:

```text
Passed!  - Failed: 0, Passed: 74, Skipped: 0, Total: 74
```

Risk prevented:

- Normal authenticated learners cannot mutate question authoring routes (`save-draft`, `publish`, `revalidate`).
- Authoring routes require `ContentAuthorPolicy` (`UiTokensAdmin` or `ContentAuthor` role).

Policy decision:

- `validate`, `preview`, and all authoring mutation routes are **content-author-only** (admin or `ContentAuthor`), not generic learner auth.

Tests added:

- `QuestionAuthoringAuthorizationTests` — learner 403 on mutations, no draft rows on forbidden save-draft; `ContentAuthor` save-draft; admin validate/preview/save/publish.
- `QuestionAuthoringEndpointsIntegrationTests` — default `X-Test-Roles: UiTokensAdmin` for existing integration coverage.

Docs:

- `docs/API_ENDPOINT_INVENTORY.md` — per-route `ContentAuthorPolicy` table.

## Mistakes observed

none

## Completion %

95%

## Residual risk

- Read-only authoring preview routes remain admin/content-author only; learner read paths unchanged.

## Commit SHA

85a87c6