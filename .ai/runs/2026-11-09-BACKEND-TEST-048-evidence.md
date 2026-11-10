# BACKEND-TEST-048 Evidence

Evidence format: v2
Prompt ID: BACKEND-TEST-048
Queue: user-assigned
Agent/tool: Codex
Model provider: OpenAI
Model name/id: gpt-6
Client/IDE: ChatGPT Work
Run mode: known-fix
Token budget: high
Started at UTC: 2026-11-09T23:47:32Z
Completed at UTC: 2026-11-09T23:50:34Z
Elapsed time: 3m 2s
Relevant prior mistakes read: BACKEND-MISTAKE-EVIDENCE-001, BACKEND-MISTAKE-VALIDATION-001, BACKEND-MISTAKE-PERF-001, BACKEND-MISTAKE-PERF-002, BACKEND-MISTAKE-PERF-003, BACKEND-MISTAKE-SCOPE-001
How this run avoids prior mistakes: apply BACKEND-MISTAKE-EVIDENCE-001; apply BACKEND-MISTAKE-VALIDATION-001; apply BACKEND-MISTAKE-PERF-001; apply BACKEND-MISTAKE-PERF-002; apply BACKEND-MISTAKE-PERF-003; apply BACKEND-MISTAKE-SCOPE-001
Owner/hypothesis: The maintenance service fabricates zero physical bloat; suppress all REINDEX decisions, expose UNKNOWN physical health, and retain ANALYZE.
Files inspected: 13
Files changed: 7
Searches: 4
Validation runs: 4
Failed retries: 1

## Outcome
- Physical bloat percentage is null and index status UNKNOWN; size and usage remain separate.
- Automatic REINDEX removed; legacy maintenance POST and scheduler retain ANALYZE.
- PostgreSQL 16 regression and endpoint contract tests added; executable proof pending CI.

## Changed paths
- src/MathLearning.Infrastructure/Maintenance/IndexMaintenanceService.cs
- src/MathLearning.Api/Endpoints/MaintenanceEndpoints.cs
- src/MathLearning.Api/Services/IndexMaintenanceBackgroundService.cs
- tests/MathLearning.Tests/Infrastructure/IndexBloatPostgresTests.cs
- tests/MathLearning.Tests/Infrastructure/IndexMaintenanceSqlContractTests.cs
- tests/MathLearning.Tests/Endpoints/MaintenanceEndpointContractTests.cs
- .ai/runs/2026-11-09-BACKEND-TEST-048-evidence.md

## Validation
Validation run: git diff --check: pass | python scripts/check_documentation_health.py --context src/MathLearning.Infrastructure/Maintenance/IndexMaintenanceService.cs: 0 failures | python scripts/analyze_agent_runs.py --changed-from b8acd22 --fail-on-regression: pass | python scripts/validate_agent_evidence.py --changed-from b8acd22 --verify-git: initial log field rejected; fixed and rechecked
Validation not run: dotnet focused tests and Release build: dotnet is absent; PostgreSQL 16 CI Database Validation is required on draft PR | Pre-change failing test: authored first, but could not execute without dotnet/Docker

## Exceptions and learning
Mistakes observed: none
Waste: agent_run area database rejected; retried with performance
Missed: No local red/green execution or Release build; CI outcome pending
Follow-up: CLI index display still computes HEALTHY from nullable bloat and is excluded by approved path scope; update in separate owner
Residual risk: Phase 1 PARTIAL: CI/provider validation pending; legacy method and route names remain; CLI and API inventory documentation need scoped follow-up.
Documentation impact: follow-up: docs/API_ENDPOINT_INVENTORY.md and IndexMaintenanceTool/Program.cs excluded by owner scope; run log records current behavior
Cross-repo impact: no

## Delivery
State: Needs validation
Branch/PR: aw-eval/ai-only-001-backend-test-048; draft PR pending
Commit SHA: self
Completion %: 65
