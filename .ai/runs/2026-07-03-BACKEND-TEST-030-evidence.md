# BACKEND-TEST-030 Evidence

Prompt ID: BACKEND-TEST-030
Queue: `docs/prompt_queues/backend_test_followups_2026_07_03.md`
Agent/tool: ChatGPT via GitHub connector
Model provider: OpenAI
Model name/id: GPT-5.5 Thinking
Run mode: explanation endpoint safe-error and validation tests
Started from queue status: Prompt-ready

## Goal

Cover explanation endpoints at the HTTP boundary: explicit anonymous denial, validator short-circuiting, valid service delegation, default language, cancellation-token forwarding, stable not-found responses and generic unexpected-error handling.

## Confirmed problem

Generate and mistake-analysis routes returned `KeyNotFoundException.Message` directly, making service exception text a public contract and possible information leak.

## Files changed

- `src/MathLearning.Api/Endpoints/ExplanationEndpoints.cs`
- `tests/MathLearning.Tests/Endpoints/ExplanationEndpointContractTests.cs`
- coverage audit/queue/inventory documents.

## Runtime fix

- Stored problem misses return `Stored problem was not found.`
- Generate/mistake referenced problem misses return `Referenced problem was not found.`
- Raw `KeyNotFoundException.Message` is never returned.

## New executable test cases

Nine cases:

- anonymous denial for all three routes;
- blank language defaults to `en` and cancellation token is forwarded;
- invalid generate request returns validation problem with zero service calls;
- invalid mistake answer returns validation problem with zero service calls;
- generate and mistake not-found cases return stable safe message without secret text;
- stored problem not-found is stable and safe;
- valid mistake-analysis delegates once and preserves response shape;
- unexpected exception uses generic global 500 with trace id and no secret.

## Validation not run

No executable .NET environment is available. No passing test/build claim is made.

Required validation:

```text
dotnet test tests/MathLearning.Tests/MathLearning.Tests.csproj --filter "ExplanationEndpointContractTests|GenerateExplanationRequestValidator|MistakeAnalysisRequestValidator"
dotnet build MathLearning.slnx -c Release
```

## Residual risk

- Several answer/topic/subtopic fields still need explicit limits.
- Grade and positive problem-id ranges are not fully enforced.
- Expensive generation routes need per-user rate/cost policy.

Follow-up: BACKEND-TEST-043.

## Completion

88%

## Key commits

- `510303804198e789e2c04ba78c62bb4ed73edc2b` — start evidence
- `9b9aaea1cc45acebf64ccaad36ce8c6d8a0f0c58` — stable safe not-found responses
- `3fa5858d14ca98cc3dfb91fa0f38063e1f032687` — endpoint contract tests

## Cross-repo sync

No payload shape changed. Not-found text is now intentionally stable and safer; mobile docs were not modified.
