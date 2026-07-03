# BACKEND-TEST-036 Evidence

Prompt ID: BACKEND-TEST-036
Queue: `docs/prompt_queues/backend_test_coverage.md`
Agent/tool: ChatGPT via GitHub connector
Model provider: OpenAI
Model name/id: GPT-5.5 Thinking
Run mode: direct unit/contract coverage for previously unprompted gaps
Started from queue status: new implementation package requested by user

## Goal

Increase backend coverage in areas not explicitly owned by BACKEND-TEST-022…035 and not requiring schema/contract architecture changes:

- stable user-id to GUID mapping used by analytics/sync/practice;
- idempotency observability counters, normalization, routing, concurrency and log privacy;
- database startup-mode/schema-status decision logic;
- complete mathematical boundary coverage for weakness scoring.

## Relevant prior mistakes read

- BACKEND-MISTAKE-EVIDENCE-001
- BACKEND-MISTAKE-VALIDATION-001
- BACKEND-MISTAKE-IDEM-001
- BACKEND-MISTAKE-VALIDATION-002

## Guardrails

- Test behavior and invariants, not implementation line count alone.
- Avoid sleeps and nondeterministic timing.
- Use exact assertions for deterministic pure logic and ranges only for floating-point decay.
- Do not change production contracts merely to make tests easy.
- Do not claim execution success without `dotnet test` or checked CI evidence.

## Files inspected

- `src/MathLearning.Application/Helpers/UserIdGuidMapper.cs`
- `src/MathLearning.Infrastructure/Services/Idempotency/IdempotencyObservabilityService.cs`
- `src/MathLearning.Api/Services/DatabaseSchemaVersionGuard.cs`
- `src/MathLearning.Api/Services/WeaknessScoring.cs`
- `tests/MathLearning.Tests/Services/WeaknessScoringTests.cs`
- `tests/MathLearning.Tests/Endpoints/IdempotencyObservabilityEndpointsTests.cs`

## Validation

Implementation in progress. No executable .NET checkout is available in this connector session.

## Completion

10%
