# BE-PERF-016 Evidence

Evidence format: v2
Prompt ID: BE-PERF-016
Queue: docs/prompt_queues/backend_performance_followups_2026_07_03.md
Agent/tool: unknown-not-exposed
Model provider: unknown-not-exposed
Model name/id: unknown-not-exposed
Client/IDE: unknown-not-exposed
Run mode: known-fix
Token budget: medium
Started at UTC: 2026-08-02T21:01:16Z
Completed at UTC: 2026-08-02T21:09:45.6355169Z
Elapsed time: 8m 29s
Relevant prior mistakes read: BACKEND-MISTAKE-EVIDENCE-001, BACKEND-MISTAKE-VALIDATION-001, BACKEND-MISTAKE-PERF-001, BACKEND-MISTAKE-PERF-002, BACKEND-MISTAKE-PERF-003, BACKEND-MISTAKE-SCOPE-001
How this run avoids prior mistakes: apply BACKEND-MISTAKE-EVIDENCE-001; apply BACKEND-MISTAKE-VALIDATION-001; apply BACKEND-MISTAKE-PERF-001; apply BACKEND-MISTAKE-PERF-002; apply BACKEND-MISTAKE-PERF-003; apply BACKEND-MISTAKE-SCOPE-001
Owner/hypothesis: validate the canonical outbox claim/lease/backoff owner, prove local PostgreSQL execution, and keep the linked test row as the regression gate without duplicating runtime ownership
Files inspected: 7
Files changed: 4
Searches: 4
Validation runs: 7
Failed retries: 1

## Outcome
- BE-PERF-016 is now locally validated with executable PostgreSQL proof on `localhost:5432`
- the shared outbox claim/lease/backoff behavior remains owned by the canonical runtime row, and the linked test row now serves as the regression gate
- queue status rows were synchronized to `Done 100%` with the same local proof

## Changed paths
- .ai/runs/2026-08-02-BE-PERF-016-evidence.md
- docs/prompt_queues/backend_performance_followups_2026_07_03.md
- docs/prompt_queues/backend_test_coverage.md
- docs/prompt_queues/backend_test_followups_2026_07_03.md

## Validation
Validation run: `python scripts/run_guarded.py --timeout-seconds 240 -- dotnet test tests\MathLearning.Tests\MathLearning.Tests.csproj --filter "FullyQualifiedName~OutboxBatchProcessorTests" --no-restore` | Passed 5/5
Validation run: `python scripts/check_documentation_health.py --full-links --context docs/prompt_queues/backend_performance_followups_2026_07_03.md` | failures=0
Validation run: `python scripts/check_documentation_health.py --full-links --context docs/prompt_queues/backend_test_coverage.md` | failures=0
Validation run: `python scripts/check_documentation_health.py --full-links --context docs/prompt_queues/backend_test_followups_2026_07_03.md` | failures=0
Validation run: `git diff --check` | warning only: CRLF normalization notices in changed queue docs
Validation run: `python scripts/validate_agent_evidence.py --changed-from HEAD --verify-git` | failures=0
Validation run: `python scripts/analyze_agent_runs.py --changed-from HEAD --fail-on-regression` | runs=0, regressions=0

## Exceptions and learning
Mistakes observed: none
Waste: one failed patch attempt before narrowing to exact lines
Missed: no runtime code changes were needed because the implementation was already present; only validation and queue synchronization were required
Follow-up: keep `BACKEND-TEST-023` as the regression gate for future outbox changes
Residual risk: none observed in the focused claim/lease/backoff coverage
Documentation impact: updated the performance queue, coverage queue, follow-up queue and this run log to reflect validated local PostgreSQL proof
Cross-repo impact: none

## Delivery
State: Done
Branch/PR: agent/BACKEND-SEASON-DAILY-RUN-PROVENANCE-001 / not created
Commit SHA: self
Completion %: 100
