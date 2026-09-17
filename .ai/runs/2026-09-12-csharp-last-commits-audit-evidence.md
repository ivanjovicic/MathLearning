# csharp-last-commits-audit-2026-09-12 Evidence

Evidence format: v2
Prompt ID: csharp-last-commits-audit-2026-09-12
Queue: user-assigned
Run mode: bounded auth/registration review and repair
Owner/hypothesis: latest C# auth observability commit may misclassify EF database failures, duplicate Identity password policy at the endpoint, and log raw auth identifiers.
Relevant prompts read: BACKEND-API-DB-017, BACKEND-API-DB-018, BACKEND-OPS-HEALTH-LIVENESS-001, BACKEND-OPS-HANGFIRE-NEON-001

## Outcome

- Confirmed and repaired EF `DbUpdateException` handling for mobile registration: database write failures now use the existing safe `503 registration_unavailable` contract.
- Removed endpoint-level registration minimum-password duplication; Identity remains the single minimum-policy owner, while the endpoint retains only the bounded pre-hash maximum.
- Removed raw username/user-id values from login logs and bounded username/email validation to documented limits.
- Isolated the registration atomicity test fixture from the shared rate-limit singleton so test order cannot produce false `429` failures.
- No economy, reward, quiz-answer, Daily Run, health/liveness, or Hangfire production behavior was changed in this lane.

## Changed paths

- `src/MathLearning.Api/Endpoints/AuthEndpoints.cs`
- `tests/MathLearning.Tests/Endpoints/AuthMobileRegistrationAtomicityTests.cs`
- `docs/API_ENDPOINT_INVENTORY.md`

## Validation

- `python scripts/run_guarded.py --timeout-seconds 180 -- dotnet test ... --filter "FullyQualifiedName~AuthMobileRegistrationAtomicityTests|FullyQualifiedName~ServiceRegistrationSecurityTests"`: PASS 16/16.
- `python scripts/run_guarded.py --timeout-seconds 240 -- dotnet test ... -c Release --filter "FullyQualifiedName~AuthMobileRegistrationAtomicityTests|FullyQualifiedName~ServiceRegistrationSecurityTests|FullyQualifiedName~HealthEndpointContractTests|FullyQualifiedName~RedisRuntimeStatusTests|FullyQualifiedName~AuthSessionInvalidationTests"`: PASS 28/28.
- `git diff --check`: PASS.
- `python scripts/validate_agent_evidence.py --changed-from HEAD --verify-git`: PASS, failures=0, warnings=0.
- `python scripts/check_documentation_health.py --context .ai/runs/2026-09-12-csharp-last-commits-audit-evidence.md`: PASS, failures=0.

## Residuals

- `BACKEND-API-DB-017` still needs provider/distributed-limiter proof and legacy `/auth/register` consolidation; this lane did not invent a second registration owner.
- `BACKEND-API-DB-018` remains a separate access-token revocation owner.
- Health/liveness and Hangfire/Neon isolation prompts remain handoffs; latest commits did not complete their required bounded-provider proof.
- Existing warnings remain: OpenTelemetry advisory, duplicate Cosmetics using, and EF1002 test SQL warning.

Documentation impact: updated `docs/API_ENDPOINT_INVENTORY.md` for username/email/password limits and diagnostic reason coverage.
Delivery: working-tree patch; no commit or PR created.
Commit SHA: self
State: code repaired; remaining provider/ops/cross-owner proof prevents full Done claim.
