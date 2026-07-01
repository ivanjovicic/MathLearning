# BACKEND-CRIT-006 Evidence

Prompt ID: BACKEND-CRIT-006
Queue: docs/prompt_queues/backend_critical_risk_prevention.md
Agent/tool: Codex
Model provider: unknown-not-exposed
Model name/id: unknown-not-exposed
Model mode/settings: unknown-not-exposed
Client/IDE: VS Code terminal
Run mode: investigation/spec first, docs-only
Token budget: medium
Actual context: backend contract idempotency decision pass after BACKEND-CRIT-005 evidence
Started from queue status: Prompt-ready (after CRIT-005 evidence)
Local collision check: no competing BACKEND-CRIT-006 run log found in .ai/runs
Relevant prior mistakes read:
- BACKEND-MISTAKE-AUDIT-001
- BACKEND-MISTAKE-EVIDENCE-001
- BACKEND-MISTAKE-VALIDATION-001
- BACKEND-MISTAKE-XREPO-001
How this run avoids prior mistakes:
- keep the prompt docs-only instead of claiming a runtime hardening that was not implemented;
- separate the decision matrix from any future migration prompt;
- call out legacy compatibility explicitly so no-key fallback is not silently misrepresented;
- record cross-repo sync as deferred because the mobile repo was not edited here.
Elapsed time: unknown-not-recorded
Phase time breakdown: unknown-not-recorded

## Files inspected

- docs/prompt_queues/backend_critical_risk_prevention.md
- docs/mobile_contract_idempotency_handoff.md
- docs/backend_contract_gap_report.md
- src/MathLearning.Api/Endpoints/QuizEndpoints.cs
- src/MathLearning.Api/Endpoints/SrsEndpoints.cs
- src/MathLearning.Api/Endpoints/EconomySettlementEndpoints.cs
- src/MathLearning.Api/Endpoints/CosmeticsEndpoints.cs
- src/MathLearning.Api/Endpoints/DailyRunEndpoints.cs
- tests/MathLearning.Tests/Idempotency/QuizAnswerIdempotencyTests.cs
- tests/MathLearning.Tests/Idempotency/SrsUpdateIdempotencyTests.cs
- tests/MathLearning.Tests/Contracts/MobileMutationContractIntegrationTests.cs
- tests/MathLearning.Tests/Endpoints/OfflineBatchSubmitCompatibilityTests.cs

## Files changed

- docs/MOBILE_MUTATION_IDEMPOTENCY_REQUIREMENTS_2026_07_01.md
- docs/backend_contract_gap_report.md
- docs/prompt_queues/backend_critical_risk_prevention.md

## Commands run

- rg -n on queue/doc/endpoint/test paths for BACKEND-CRIT-006 scope
- Get-Content on queue/doc/endpoint/test snippets
- git diff --check
- Test-Path verification for referenced docs/source/test paths

## What was done

- Added a backend decision matrix for retryable mobile mutation idempotency.
- Recorded that `/api/quiz/answer` and `/api/quiz/srs/update` stay legacy-compatible for now.
- Recorded that `/api/quiz/batch-submit` stays a legacy compatibility adapter in this prompt.
- Added a short decision note to `docs/backend_contract_gap_report.md`.
- Updated the queue row to reflect docs/spec completion.

## What was missed

- No runtime hardening was implemented in this prompt.
- No dotnet test was run because the prompt stayed docs-only.

## Validation run

- `git diff --check` — passed
- docs-only path verification with `Test-Path` — passed

## Validation not run

- `dotnet test` — not run, docs-only per prompt

## Waste categories

- context lookup
- PowerShell separator mismatch
- encoding-visible queue output

## Mistakes observed

- none

## Where time/context was wasted

- One attempt used `&&` in PowerShell, which is not accepted in this shell.
- Some queue output rendered with encoding artifacts (`â€”`) while inspecting markdown.

## Why waste happened

- I initially used a shell separator from a different shell family.
- The queue file contains non-ASCII punctuation that renders noisily in the terminal.

## What the next agent should avoid

- Do not treat the legacy no-key behavior as already hard-rejected.
- Do not convert this spec-only pass into an implicit runtime fix.

## Docs/rules updated to prevent repeat

- docs/MOBILE_MUTATION_IDEMPOTENCY_REQUIREMENTS_2026_07_01.md
- docs/backend_contract_gap_report.md
- docs/prompt_queues/backend_critical_risk_prevention.md

## Queue updated

- yes

## New optimized prompt added

- none

## Follow-up prompt

- implementation prompt required later if the product wants hard rejection of missing operation identity

## Completion %

- 85

## Residual risk

- Legacy no-key compatibility remains until a migration prompt hardens the mobile contract.

## Commit SHA

- 1e53f1c
