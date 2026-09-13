# REGISTRATION-OBSERVABILITY Evidence

Evidence format: v2
Prompt ID: REGISTRATION-OBSERVABILITY
Queue: direct-user-request
Agent/tool: Cursor Auto
Run mode: known-fix
Token budget: medium
Started at UTC: 2026-09-09T09:14:34Z
Completed at UTC: 2026-09-09T12:30:00Z
Owner/hypothesis: Mobile registration lacked safe reason codes, structured unexpected-failure logs, min password length early reject, rate limit and DbException mapping.
Files changed: AuthEndpoints.cs, TokenDtos.cs, AuthMobileRegistrationAtomicityTests.cs, CorsConfigurationGuardTests.cs, API_ENDPOINT_INVENTORY.md, ops residual queue prompts

## Validation
Validation run:
- `dotnet test --filter FullyQualifiedName~AuthMobileRegistrationAtomicityTests|FullyQualifiedName~CorsConfigurationGuardTests` → Passed 9 / Failed 0
- API project builds with AuthEndpoints changes

Production probe (2026-09-09):
- GET /api/health/, /api/health/db, /api/health/ready and OPTIONS /auth/mobile/register against mathlearning-api.fly.dev timed out (15–20s). No secrets collected.
- Follow-up owners: BACKEND-OPS-HEALTH-LIVENESS-001, BACKEND-OPS-HANGFIRE-NEON-001

## Documentation impact
Documentation impact: updated docs/API_ENDPOINT_INVENTORY.md; added docs/prompt_queues/backend_registration_ops_residuals_2026_09_09.md

## Delivery
State: pending PR/main
Branch: codex/fix-registration-observability
