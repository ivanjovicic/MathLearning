# BACKEND2-CRIT-005 Evidence

Prompt ID: BACKEND2-CRIT-005
Queue: docs/prompt_queues/backend_second_pass_risk_prevention.md
Agent/tool: Cursor Agent
Model provider: unknown-not-exposed
Model name/id: unknown-not-exposed
Run mode: implementation/test
Elapsed time: unknown-not-recorded
Relevant prior mistakes read: BACKEND-MISTAKE-VALIDATION-001
How this run avoids prior mistakes: endpoint + unit bounds tests with VALIDATION_ERROR contract

Commit SHA: aa83a3a
Validation command:

```bash
dotnet test tests/MathLearning.Tests/MathLearning.Tests.csproj --filter "Adaptive|Validation|AnswerBounds"
```

Result:

```text
Passed!  - Failed: 0, Passed: 50, Skipped: 0, Total: 50
```

Risk prevented:

- Adaptive session answers reject unbounded confidence, response time, timestamp, and answer length before scoring/storage.

Bounds policy:

- `confidence`: finite number in `[0, 1]` (invalid strings rejected)
- `responseTimeSeconds` / `responseTimeMs`: non-negative, max 3600 seconds
- `answeredAt`: UTC-normalized, same 2-minute future / 90-day replay window as offline policy
- `answer`: required, max 2000 characters

Runtime changes:

- `AdaptiveAnswerInputBounds` — shared validation helpers
- `AdaptiveEndpoints.TryBuildAdaptiveAnswerRequest` — rejects invalid payloads with `VALIDATION_ERROR`
- `AdaptiveLearningService.ValidateAnswerRequest` — defense-in-depth using same bounds

Tests added:

- `AdaptiveAnswerInputBoundsTests` — confidence, response time, timestamps, answer length
- `AdaptiveAnswerBoundsEndpointTests` — HTTP 400 + `VALIDATION_ERROR` contract

## Mistakes observed

none

## Completion %

95%

## Residual risk

- Bounds apply to adaptive session answer path only; other free-text endpoints may need separate prompts.

## Commit SHA

aa83a3a