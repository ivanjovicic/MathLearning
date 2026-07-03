# BACKEND-TEST-021 Evidence

Prompt ID: BACKEND-TEST-021
Queue: `docs/prompt_queues/backend_test_coverage.md`
Agent/tool: ChatGPT via GitHub connector
Model provider: OpenAI
Model name/id: GPT-5.5 Thinking
Run mode: operational security bugfix + endpoint policy tests
Started from queue status: new P1/P0 operational authorization bug found by BACKEND-TEST-AUDIT-002

## Goal

Restrict all `/api/maintenance/*` routes to the explicit admin policy and prove ordinary authenticated users cannot trigger index rebuilds or read database index details.

## Relevant prior mistakes read

- BACKEND-MISTAKE-EVIDENCE-001
- BACKEND-MISTAKE-AUDIT-001
- BACKEND-MISTAKE-VALIDATION-001
- BACKEND-MISTAKE-AUTH-002
- BACKEND-MISTAKE-VALIDATION-002

## Confirmed problem

`MaintenanceEndpoints` used generic `.RequireAuthorization()` and contained `TODO: Add admin role check`. This protected only against anonymous callers, not ordinary learners.

## Files inspected

- `src/MathLearning.Api/Endpoints/MaintenanceEndpoints.cs`
- `src/MathLearning.Infrastructure/Maintenance/IndexMaintenanceService.cs`
- `src/MathLearning.Application/Services/DesignTokenServices.cs`
- `tests/MathLearning.Tests/Helpers/CustomWebApplicationFactory.cs`
- `tests/MathLearning.Tests/Helpers/TestAuthHandler.cs`

## Files changed

- `src/MathLearning.Api/Endpoints/MaintenanceEndpoints.cs`
- `tests/MathLearning.Tests/Endpoints/MaintenanceEndpointAuthorizationTests.cs`
- `tests/MathLearning.Tests/Helpers/TestAuthHandler.cs`
- `tests/MathLearning.Tests/GlobalUsings.TestInfrastructure.cs`
- `docs/API_ENDPOINT_INVENTORY.md`
- `docs/BACKEND_TEST_COVERAGE_AUDIT_2026_07_03.md`
- `docs/prompt_queues/backend_test_coverage.md`
- `docs/prompt_queues/backend_test_followups_2026_07_03.md`
- `docs/ai/learning/MISTAKE_LEDGER.md`
- `.ai/runs/2026-07-03-BACKEND-TEST-021-evidence.md`

## Runtime fix

- The maintenance route group now requires `DesignTokenSecurity.AdminPolicy`.
- All rebuild, health and statistics routes inherit the explicit admin policy.
- Interpolated logging was replaced with structured logging for the rebuild count.

## Implemented tests

1. Explicit anonymous caller is denied on rebuild, index health and index statistics routes.
2. Authenticated learner receives 403 on all three routes.
3. Endpoint metadata inspection proves every `/api/maintenance/*` route carries the exact `UiTokensAdminPolicy` policy.

The tests intentionally stop before invoking the real maintenance implementation for denied requests.

## Validation run

Static code/test inspection only:

- route metadata assertion protects against future downgrade to generic authentication;
- explicit anonymous and authenticated learner branches are separate;
- endpoint inventory documents exact maintenance policy.

## Validation not run

No executable .NET repository checkout is available in this connector environment. No passing test/build claim is made.

Required command:

```text
dotnet test tests/MathLearning.Tests/MathLearning.Tests.csproj --filter "FullyQualifiedName~MaintenanceEndpointAuthorizationTests"
dotnet build MathLearning.slnx -c Release
```

## Confirmed follow-up problem

`GET /api/maintenance/index-stats` currently invokes `RebuildCorruptedIndexesAsync`, which can make a nominal read route mutate database indexes. Endpoints instantiate the concrete service directly, so positive admin behavior cannot be isolated with a fake service.

Tracked by BACKEND-TEST-024:

- injectable maintenance interface;
- side-effect-free GET routes;
- positive admin endpoint tests;
- rebuild non-overlap/cancellation/audit coverage.

## Residual risk

- Runtime/test code is not executable-proof validated.
- Positive admin execution still calls the real database maintenance implementation and is not covered here.
- GET index-statistics side effects remain open until BACKEND-TEST-024.

## Completion

88%

## Commit SHAs

- `4d7c97252eab2f615e563862c7702eede31a0aa3` — start evidence
- `1cc603125758bb22c506ea3d77799033a33ebd04` — admin policy runtime fix
- `4ba01b6d33694311beaceeb127508353f979bc07` — initial endpoint tests
- `d62ae7d747d2dd4144ec0ca24bd73d8ac285c1fc` — explicit anonymous test-auth support
- `5eefd93e261fe146216bd846456aee1297bdf67e` — shared MVC test global using
- `ec3dbc31a49c8555a6f330f46bdd814c1c3c27ac` — explicit-anonymous test update
- `f455ee127ac62b3b5c08aad16ee54860088d8cfb` — endpoint inventory
- `9a9f4b696b479d530ed50e0fb433e03dade6af0d` — mistake ledger
- `c431804ef5bbc2c86d6f8b4da9ec2ab41eec75c7` — central queue

## Cross-repo sync

Not applicable. Maintenance routes are backend/admin operational surfaces; no mobile contract change.
