# BACKEND-CRIT-001 Evidence

Prompt ID: BACKEND-CRIT-001
Queue: `docs/prompt_queues/backend_critical_risk_prevention.md`
Agent/tool: Cursor Agent
Model provider: unknown-not-exposed
Model name/id: unknown-not-exposed
Client/IDE: Cursor
Run mode: implementation/test
Token budget: medium
Started from queue status: Prompt-ready
Relevant prior mistakes read: BACKEND-MISTAKE-AUDIT-001, BACKEND-MISTAKE-EVIDENCE-001, BACKEND-MISTAKE-VALIDATION-001
How this run avoids prior mistakes: runtime changes + targeted tests + this run log; no Done claim without validation output
Elapsed time: unknown-not-recorded

## Files inspected

- `docs/BACKEND_CRITICAL_APP_FLOW_AUDIT_2026_07_01.md`
- `docs/BACKEND_CRITICAL_RISK_PREVENTION_RULES.md`
- `src/MathLearning.Api/Middleware/GlobalExceptionMiddleware.cs`
- `src/MathLearning.Api/Endpoints/AuthEndpoints.cs`
- `tests/MathLearning.Tests/Helpers/CustomWebApplicationFactory.cs`

## Files changed

- `src/MathLearning.Api/Middleware/SafeClientErrorResponse.cs` (new)
- `src/MathLearning.Api/Middleware/GlobalExceptionMiddleware.cs`
- `src/MathLearning.Api/Endpoints/AuthEndpoints.cs`
- `tests/MathLearning.Tests/Middleware/GlobalExceptionMiddlewareTests.cs` (new)
- `tests/MathLearning.Tests/Endpoints/AuthSafeErrorResponseTests.cs` (new)
- `docs/prompt_queues/backend_critical_risk_prevention.md` (status row)

## Commands run

```bash
dotnet test tests/MathLearning.Tests/MathLearning.Tests.csproj --filter "Auth|GlobalException|ErrorResponse"
dotnet test tests/MathLearning.Tests/MathLearning.Tests.csproj --filter "FullyQualifiedName~GlobalException|FullyQualifiedName~AuthSafeError"
git diff --check
```

## What was done

- Added `SafeClientErrorResponse` helper: generic client messages, trace/correlation ids, `AuthUnexpectedFailure` for auth catch blocks.
- Hardened `GlobalExceptionMiddleware`: removed raw `ex.Message` and `exceptionType` from `errorDetails`; preserved 429 `Retry-After`.
- Wired login, refresh, logout, revoke-all, and register unexpected-failure paths through `SafeClientErrorResponse.AuthUnexpectedFailure`.
- Added unit tests for global 500/429 safe responses (`GlobalExceptionMiddlewareTests`, 2 tests).
- Added integration tests proving login/refresh/logout/revoke-all do not leak raw exception text (`AuthSafeErrorResponseTests`, 4 tests).

## What was missed

- Broader endpoint sweep (avatar, cosmetics, sync, etc.) — out of scope for CRIT-001 owned paths; separate prompts may apply.
- Commit not created (user did not request).

## Validation run

- `dotnet test --filter "Auth|GlobalException|ErrorResponse"` — **Passed: 41, Failed: 0**
- Focused subset `GlobalException|AuthSafeError` — **Passed: 6, Failed: 0**
- `git diff --check` — passed (line-ending warnings only)

## Validation not run

- Full `dotnet test` suite (not required by prompt)

## Risk prevented

- **backend-error-leak**: raw database/provider/exception messages no longer returned to mobile clients on global 500, auth login/refresh/logout/revoke-all unexpected failures, or rate-limit responses.

## Tests added

| Test class | Tests |
|---|---|
| `GlobalExceptionMiddlewareTests` | 500 safe body; 429 Retry-After without leak |
| `AuthSafeErrorResponseTests` | login, refresh, logout, revoke-all safe 500 |

## Mistakes observed

- None new. Prior BACKEND-MISTAKE-VALIDATION-001 avoided by recording filter command + pass count.

## Completion %

90% (runtime + tests validated; commit SHA pending)

## Residual risk

- Other endpoints still return `ex.Message` in some catch paths (avatar, cosmetics, sync) — tracked outside CRIT-001 owned paths.
- `Program.cs` development error detail may still surface message in non-production paths — not in CRIT-001 owned list.

## Commit SHA

uncommitted
