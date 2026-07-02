# BACKEND2-CRIT-007 Evidence

Prompt ID: BACKEND2-CRIT-007
Queue: docs/prompt_queues/backend_second_pass_risk_prevention.md
Agent/tool: Cursor Agent
Model provider: unknown-not-exposed
Run mode: implementation/test
Elapsed time: unknown-not-recorded
Relevant prior mistakes read: BACKEND-MISTAKE-AUDIT-001
How this run avoids prior mistakes: production guard policy unit tests before Done

Commit SHA: pending (see batch commit)
Validation command:

```bash
dotnet test tests/MathLearning.Tests/MathLearning.Tests.csproj --filter "SeedAdmin|Startup|Admin"
```

Result:

```text
Passed!  - Failed: 0, Passed: 55, Skipped: 0, Total: 55
```

Risk prevented:

- Production cannot bootstrap admin with a missing password or the Development default password.
- `SeedAdmin:ResetPasswordOnStart` in non-Development environments is ignored unless `SeedAdmin:AllowEmergencyPasswordReset=true`.
- Startup logs reference admin username only; password values are never logged.

Runtime changes:

- `SeedAdminStartupPolicy` — centralizes production guardrails for admin bootstrap.
- `Program.cs` `SeedAdminUser` — uses policy evaluation and safe audit logs.

Production bootstrap:

1. Set `SeedAdmin__Enabled=true`.
2. Set `SeedAdmin__Password` to a strong secret (not the Development default).
3. Optional one-time emergency reset: set both `SeedAdmin__ResetPasswordOnStart=true` and `SeedAdmin__AllowEmergencyPasswordReset=true`, then remove the emergency flag after recovery.

Tests added:

- `SeedAdminStartupPolicyTests` — Development defaults, production password guards, emergency reset flag behavior.

## Mistakes observed

none

## Completion %

95%

## Residual risk

- Operators must rotate emergency reset flags after one-time recovery.

## Commit SHA

pending (see batch commit)