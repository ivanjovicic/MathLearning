# BACKEND-TEST-029 Evidence

Prompt ID: BACKEND-TEST-029
Queue: `docs/prompt_queues/backend_test_followups_2026_07_03.md`
Agent/tool: ChatGPT via GitHub connector
Model provider: OpenAI
Model name/id: GPT-5.5 Thinking
Run mode: analytics/recommendation endpoint contract tests
Started from queue status: Prompt-ready

## Goal

Add HTTP-level coverage for analytics and practice recommendation routes: explicit anonymous denial, server-derived user scope, paging normalization, response shape, cancellation-token forwarding and safe error handling.

## Files changed

- `tests/MathLearning.Tests/Endpoints/AnalyticsEndpointContractTests.cs`
- `src/MathLearning.Api/Endpoints/AnalyticsEndpoints.cs` through BACKEND-TEST-028 pagination hardening
- coverage audit/queue/inventory documents.

## New executable test cases

Seven cases:

1–3. Explicit anonymous requests are denied for weakness, weakness details and practice recommendations before service invocation.
4. Weakness uses authenticated claim identity, ignores forged query user id, normalizes paging and forwards cancellation.
5. Weakness details returns stable topic/subtopic page shape.
6. Practice recommendations use authenticated identity and return the requested page contract.
7. Unexpected service failure returns generic 500 with trace id and no raw secret.

## Test design

- `IWeaknessAnalysisService` is replaced with a recording fake.
- User id, take count and cancellation token are asserted at the endpoint boundary.
- Service/scoring integration remains separate.
- Extreme arithmetic is covered by BACKEND-TEST-028.

## Validation not run

No executable .NET environment is available. No passing-test claim is made.

Required validation:

```text
dotnet test tests/MathLearning.Tests/MathLearning.Tests.csproj --filter "AnalyticsEndpointContractTests|AnalyticsExtremePaginationEndpointTests"
dotnet build MathLearning.slnx -c Release
```

## Residual risk

- Bounded fetch windows are still sliced in memory.
- Database-level stable ordering/query budget is not proven.
- PostgreSQL query plan and cancellation remain unmeasured.

Follow-up: BACKEND-TEST-045.

## Completion

88%

## Key commits

- `6d21be93e133b355113b3f2ae94bc5e5f665f31f` — start evidence
- `e4cb469888023e39d354643d0946f85142ebc461` — analytics HTTP contracts
- `8e37460482ec530e65094586c55c82e70a4a18c3` / `26c37f59ea2257645768d110696f37e3ca41582a` — bounded pagination

## Cross-repo sync

Not required. Existing response fields and authenticated-user ownership semantics were locked down, not changed.
