# P0-A Evidence

Evidence format: v2
Prompt ID: P0-A
Queue: user-assigned
Agent/tool: unknown-not-exposed
Model provider: unknown-not-exposed
Model name/id: unknown-not-exposed
Client/IDE: unknown-not-exposed
Run mode: investigation
Token budget: high
Started at UTC: 2026-11-25T02:09:35Z
Completed at UTC: 2026-11-25T02:21:27Z
Elapsed time: 11m 52s
Relevant prior mistakes read: BACKEND-MISTAKE-EVIDENCE-001, BACKEND-MISTAKE-VALIDATION-001, BACKEND-MISTAKE-CONTENT-001, BACKEND-MISTAKE-CONTENT-002
How this run avoids prior mistakes: apply BACKEND-MISTAKE-EVIDENCE-001; apply BACKEND-MISTAKE-VALIDATION-001; apply BACKEND-MISTAKE-CONTENT-001; apply BACKEND-MISTAKE-CONTENT-002
Owner/hypothesis: backend API contract owned by issue #64; PR #65/#66 ownership preserved. Hypothesis: exact-hash publish admission plus a signed issued snapshot can keep the optional POST aid tied to reviewed content and the original user/session/version.
Files inspected: 50
Files changed: 28
Searches: 30
Validation runs: 23
Failed retries: 7

## Outcome
- Implemented fail-closed publish checks and typed POST-only MathVisual; 118 focused tests passed; Release build passed; independent read-only review found no remaining new-path defect. Release remains blocked by old-row/translation inventory and teacher approval.

## Changed paths
- docs/API_ENDPOINT_INVENTORY.md
- docs/P0A_MATHEMATICAL_INTEGRITY_AUDIT.md
- docs/mobile_api_contract.md
- openapi.yaml
- src/MathLearning.Admin/Program.cs
- src/MathLearning.Admin/Services/AdminQuestionValidationOrchestrator.cs
- src/MathLearning.Api/Endpoints/QuizEndpoints.cs
- src/MathLearning.Api/Program.cs
- src/MathLearning.Api/Services/IssuedMathVisualService.cs
- src/MathLearning.Api/Startup/ServiceRegistrationExtensions.cs
- src/MathLearning.Application/DTOs/Questions/MathVisualPlanDto.cs
- src/MathLearning.Application/DTOs/Questions/QuestionAuthoringDtos.cs
- src/MathLearning.Application/DTOs/Quiz/SubmitAnswerResponse.cs
- src/MathLearning.Domain/Entities/QuizSession.cs
- src/MathLearning.Infrastructure/DependencyInjection.cs
- src/MathLearning.Infrastructure/Services/DbSeeder.cs
- src/MathLearning.Infrastructure/Services/QuestionAuthoring/MathQuestionAuthoringService.cs
- src/MathLearning.Infrastructure/Services/QuestionAuthoring/MathVisualContentPolicy.cs
- src/MathLearning.Infrastructure/Services/QuestionAuthoring/QuestionAuthoringRequestSanitizer.cs
- src/MathLearning.Infrastructure/Services/Sync/OfflineBundleService.cs
- tests/MathLearning.Tests/Contracts/QuizMathVisualIntegrationTests.cs
- tests/MathLearning.Tests/Services/AdminQuestionValidationOrchestratorTests.cs
- tests/MathLearning.Tests/Services/DbSeederReviewedPackTests.cs
- tests/MathLearning.Tests/Services/IssuedMathVisualServiceTests.cs
- tests/MathLearning.Tests/Services/MathVisualContentPolicyTests.cs
- tests/MathLearning.Tests/Services/OfflineBundleServiceTests.cs
- tests/MathLearning.Tests/Services/QuestionAuthoringPipelineTests.cs
- .ai/runs/2026-11-25-P0-A-evidence.md

## Validation
Validation run at tested SHA `abf2c13fbeb10f32de4de3f353d06f99ac03f924`: `python scripts/run_guarded.py --timeout-seconds 300 -- dotnet test tests/MathLearning.Tests/MathLearning.Tests.csproj --filter 'FullyQualifiedName~StepEngineTests|FullyQualifiedName~LegacyStepExplanationAdapterTests|FullyQualifiedName~QuizIssuedVersionSettlementIntegrationTests|FullyQualifiedName~QuizAnswerIdempotencyTests|FullyQualifiedName~QuizStartContractIntegrationTests|FullyQualifiedName~MathVisualContentPolicyTests|FullyQualifiedName~IssuedMathVisualServiceTests|FullyQualifiedName~QuizMathVisualIntegrationTests|FullyQualifiedName~OfflineBundleServiceTests|FullyQualifiedName~QuestionAuthoringPipelineTests|FullyQualifiedName~DbSeederReviewedPackTests|FullyQualifiedName~AdminQuestionValidationOrchestratorTests' --nologo --no-restore`: 118 passed, 0 failed, 0 skipped; `python scripts/run_guarded.py --timeout-seconds 300 -- dotnet build MathLearning.slnx -c Release --no-restore`: 0 warnings/errors; OpenAPI YAML parse and documentation health passed.
Validation not run: Exact-head CI Database Validation run 45532243425 is in progress; full production-row inventory, translation/cache audit and teacher content approval not available.

## Exceptions and learning
Mistakes observed: BACKEND-MISTAKE-VALIDATION-001; prevention=added overflow and malformed-snapshot negative cases; independent review findings resolved.
Waste: Budget exceeded: 28 changed paths vs selected cap 10; initial command/path errors corrected; no data mutation.
Missed: First-writer concurrent settlement race was not independently stress-tested; only concurrent completed replay and existing idempotency suite were exercised.
Follow-up: Resolve published-row and translation/cache inventory before enabling config; mobile #1015 renders typed POST aid; #987 keeps settlement/replay authoritative; owner #65 retains B0 work.
Residual risk: No teacher-approved hash/config; configured subtopic old published rows remain playable and legacy translations live outside the reviewed hash; no merge.
Documentation impact: updated docs/API_ENDPOINT_INVENTORY.md, docs/mobile_api_contract.md, openapi.yaml and added docs/P0A_MATHEMATICAL_INTEGRITY_AUDIT.md
Cross-repo impact: read-only mobile PR #1014 docs/comments; no mobile files changed

## Delivery
State: Needs merge
Branch/PR: codex/p0a-math-integrity-20261124; draft PR #69
Commit SHA: self
Completion %: 79
