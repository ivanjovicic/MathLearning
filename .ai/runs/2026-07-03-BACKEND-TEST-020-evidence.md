# BACKEND-TEST-020 Evidence

Prompt ID: BACKEND-TEST-020
Queue: `docs/prompt_queues/backend_test_coverage.md`
Agent/tool: ChatGPT via GitHub connector
Model provider: OpenAI
Model name/id: GPT-5.5 Thinking
Run mode: security bugfix + endpoint authorization tests
Started from queue status: new P1 authorization bug found by BACKEND-TEST-AUDIT-002

## Goal

Prevent ordinary authenticated learners from listing, reading, or updating all bug reports through `/api/bugs` admin routes, and make the submission route's authentication contract explicit.

## Relevant prior mistakes read

- BACKEND-MISTAKE-EVIDENCE-001
- BACKEND-MISTAKE-AUDIT-001
- BACKEND-MISTAKE-VALIDATION-001
- BACKEND-MISTAKE-XREPO-001
- BACKEND-MISTAKE-AUTH-002
- BACKEND-MISTAKE-VALIDATION-002

## Confirmed problem

`BugEndpoints` created its admin group with generic `.RequireAuthorization()` only. The list, detail and update routes lacked an admin policy. The report group was marked `.AllowAnonymous()` even though its handler rejected requests without a `userId` claim.

## Files inspected

- `src/MathLearning.Api/Endpoints/BugEndpoints.cs`
- `src/MathLearning.Application/Services/IBugReportService.cs`
- `src/MathLearning.Infrastructure/Services/BugReportService.cs`
- `src/MathLearning.Application/DTOs/Bugs/BugDtos.cs`
- `src/MathLearning.Application/Services/DesignTokenServices.cs`
- `tests/MathLearning.Tests/Helpers/CustomWebApplicationFactory.cs`
- `tests/MathLearning.Tests/Helpers/TestAuthHandler.cs`

## Files changed

- `src/MathLearning.Api/Endpoints/BugEndpoints.cs`
- `tests/MathLearning.Tests/Endpoints/BugEndpointAuthorizationTests.cs`
- `tests/MathLearning.Tests/Helpers/TestAuthHandler.cs`
- `tests/MathLearning.Tests/GlobalUsings.TestInfrastructure.cs`
- `docs/API_ENDPOINT_INVENTORY.md`
- `docs/BACKEND_TEST_COVERAGE_AUDIT_2026_07_03.md`
- `docs/prompt_queues/backend_test_coverage.md`
- `docs/prompt_queues/backend_test_followups_2026_07_03.md`
- `docs/ai/learning/MISTAKE_LEDGER.md`
- `.ai/runs/2026-07-03-BACKEND-TEST-020-evidence.md`

## Runtime fix

- Created an authenticated user bug group for `/report` and `/mine`.
- Created an admin bug group protected by `DesignTokenSecurity.AdminPolicy` for list/detail/update.
- Removed the contradictory anonymous group declaration.
- Kept user identity server-derived from the authenticated `userId` claim.

## Implemented tests

1. Explicit anonymous caller cannot report, read own reports, list all, read arbitrary detail or update; fake service receives zero calls.
2. Authenticated learner can report and read only own reports; invalid page/pageSize normalize to 1/50.
3. Authenticated learner receives 403 for global list/detail/update; fake admin service receives zero calls.
4. Admin role can list, read and update; invalid page/pageSize normalize to 1/20 and filters are forwarded.

## Test-infrastructure correction

`TestAuthHandler` previously authenticated no-header requests as `test-user`, so “anonymous” tests could actually be non-admin tests. Added explicit `X-Test-Anonymous: true` support returning `AuthenticateResult.NoResult()` and used it in this suite.

## Validation run

Static code/test inspection only:

- route groups use the registered `UiTokensAdminPolicy` constant;
- denied-call assertions also verify the fake service was not invoked;
- true anonymous and authenticated learner branches are separate;
- endpoint inventory documents exact policies.

## Validation not run

No .NET SDK/repository checkout is available in this connector environment. No passing test/build claim is made.

Required command:

```text
dotnet test tests/MathLearning.Tests/MathLearning.Tests.csproj --filter "FullyQualifiedName~BugEndpointAuthorizationTests"
dotnet build MathLearning.slnx -c Release
```

## Follow-up prompt

BACKEND-TEST-025 covers remaining bug-report safety:

- free-text and screenshot bounds;
- file signature/type checks;
- screenshot compensation when DB persistence fails;
- invalid status/severity and storage error contracts;
- report creation abuse/rate limiting.

## Residual risk

- Runtime/test code is not executable-proof validated.
- Positive tests use a fake bug service; service persistence/storage behavior remains separate coverage.
- Existing authenticated mobile/report consumers should continue to work, but no mobile repository tests ran.

## Completion

88%

## Commit SHAs

- `bef2be6c114b51ccf10219262594d7ff45bb53e4` — start evidence
- `2da42f6e44c89fd83b97b630a0a168504870655c` — admin/auth route fix
- `9ef4dbc7774e49c2d561db4ea6938b71ee0a0af3` — initial endpoint tests
- `d62ae7d747d2dd4144ec0ca24bd73d8ac285c1fc` — explicit anonymous test-auth support
- `5eefd93e261fe146216bd846456aee1297bdf67e` — shared MVC test global using
- `6d48b8d607ce9882d21c5a99f09190444bed04e5` — explicit-anonymous test update
- `f455ee127ac62b3b5c08aad16ee54860088d8cfb` — endpoint inventory
- `9a9f4b696b479d530ed50e0fb433e03dade6af0d` — mistake ledger
- `c431804ef5bbc2c86d6f8b4da9ec2ab41eec75c7` — central queue

## Cross-repo sync

Not applicable to payload shape. The handler already required a user claim in practice; this change makes middleware policy explicit and locks admin routes. Mobile docs touched: none.
