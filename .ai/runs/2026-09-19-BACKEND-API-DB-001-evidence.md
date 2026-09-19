# BACKEND-API-DB-001 Evidence

Evidence format: v2
Prompt ID: BACKEND-API-DB-001
Queue: formal
Agent/tool: unknown-not-exposed
Model provider: unknown-not-exposed
Model name/id: unknown-not-exposed
Client/IDE: unknown-not-exposed
Run mode: known-fix
Token budget: high
Started at UTC: 2026-09-19T11:50:48Z
Completed at UTC: 2026-09-19T11:56:31Z
Elapsed time: 6m
Relevant prior mistakes read: BACKEND-MISTAKE-EVIDENCE-001, BACKEND-MISTAKE-VALIDATION-001, BACKEND-MISTAKE-XREPO-001, BACKEND-MISTAKE-AUDIT-001
How this run avoids prior mistakes: apply BACKEND-MISTAKE-EVIDENCE-001; apply BACKEND-MISTAKE-VALIDATION-001; apply BACKEND-MISTAKE-XREPO-001; apply BACKEND-MISTAKE-AUDIT-001
Owner/hypothesis: Online pre-answer payloads must use a DTO that cannot serialize answer keys or solution material; falsifier would be a listed route or advertised schema containing `correctAnswerId`, option correctness, `hintFull`, explanation or steps.
Files inspected: 12
Files changed: 7
Searches: 6
Validation runs: 5
Failed retries: 0

## Outcome
- Hardened the stale `QuestionDto`/`QuestionEndpoints` surface to the same pre-answer-safe shape as canonical quiz/SRS DTOs.
- Added literal-solution regression coverage: hidden explanation/full hint stays absent before answer and returns only as post-settlement incorrect-answer feedback.
- Synchronized OpenAPI and mobile contract documentation; option correctness metadata is absent.

## Changed paths
- src/MathLearning.Application/DTOs/Quiz/QuestionDto.cs
- src/MathLearning.Api/Endpoints/QuestionEndpoints.cs
- tests/MathLearning.Tests/Contracts/QuizStartContractIntegrationTests.cs
- openapi.yaml
- docs/mobile_api_contract.md
- docs/prompt_queues/backend_test_coverage.md
- .ai/runs/2026-09-19-BACKEND-API-DB-001-evidence.md

## Validation
Validation run: pre-change broad contract filter 98/100 with two unrelated durable-ingest seed failures; post-change `QuizStartContractIntegrationTests` 13/13; focused quiz/SRS/translation contract filter 27/27; `dotnet build MathLearning.slnx -c Release --no-restore` succeeded with 9 existing warnings; OpenAPI QuestionDto static forbidden-field check passed; `git diff --check` pending commit.
Validation not run: Full suite and PostgreSQL provider lane; not required for this DTO/OpenAPI-only security contract change.

## Exceptions and learning
Mistakes observed: BACKEND-MISTAKE-EVIDENCE-001, BACKEND-MISTAKE-VALIDATION-001, BACKEND-MISTAKE-XREPO-001, BACKEND-MISTAKE-AUDIT-001.
Waste: Broad required filter included two unrelated durable-ingest tests failing on empty seeded email; narrowed proof to the affected quiz/SRS contract families.
Missed: No generated OpenAPI artifact is available; the checked-in schema was inspected directly.
Follow-up: Keep mobile client parsing aligned with the documented safe shape; no Flutter repository change was available in this run.
Residual risk: Existing package/compiler warnings and unrelated durable-ingest seed failures remain outside this owner.
Documentation impact: Updated `docs/mobile_api_contract.md`, OpenAPI schema, and canonical queue status.
Cross-repo impact: Backend contract tightened; mobile-side implementation is unchanged and should continue using the safe fields.

## Delivery
State: Needs validation
Branch/PR: `agent/BACKEND-API-DB-001`; delivery to `origin/main` in this run
Commit SHA: self
Completion %: 79
