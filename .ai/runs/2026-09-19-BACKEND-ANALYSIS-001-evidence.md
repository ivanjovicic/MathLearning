# BACKEND-ANALYSIS-001 Evidence

Evidence format: v2
Prompt ID: BACKEND-ANALYSIS-001
Queue: formal
Agent/tool: unknown-not-exposed
Model provider: unknown-not-exposed
Model name/id: unknown-not-exposed
Client/IDE: unknown-not-exposed
Run mode: audit
Token budget: medium
Started at UTC: 2026-09-19T10:19:35Z
Completed at UTC: 2026-09-19T10:25:11Z
Elapsed time: 5m 36s
Relevant prior mistakes read: BACKEND-MISTAKE-EVIDENCE-001, BACKEND-MISTAKE-VALIDATION-001, BACKEND-MISTAKE-AUDIT-001, BACKEND-MISTAKE-PROCESS-002
How this run avoids prior mistakes: apply BACKEND-MISTAKE-EVIDENCE-001; apply BACKEND-MISTAKE-VALIDATION-001; apply BACKEND-MISTAKE-AUDIT-001; apply BACKEND-MISTAKE-PROCESS-002
Owner/hypothesis: Auth/ownership risks should either be protected by endpoint/group policy and authenticated claim scope, or be explicitly documented public identity surfaces; falsifier would be an in-scope route that accepts caller-supplied identity without matching authorization or an existing counterexample test.
Files inspected: 10
Files changed: 2
Searches: 4
Validation runs: 8
Failed retries: 1

## Outcome
- Completed one bounded auth-and-ownership audit rotation; no unique runtime prompt routed.

## Findings
- `covered` — analytics/recommendation request `userId` values do not override the authenticated claim; `AnalyticsEndpointContractTests` proves forged query identity is ignored.
- `covered` — user settings, profile, sync, bug, and admin monitoring surfaces use required authorization and/or claim-derived scope; targeted authorization/identity tests passed.
- `covered` — public profile/avatar/appearance routes are explicit public identity surfaces and return reduced DTOs; `PublicIdentitySurfaceTests` proves progress, coins, private school data and username are excluded.
- `deferred` — high/moderate NuGet advisories and repository-wide format drift require a separate dependency/format owner; no automatic upgrade or unrelated prompt was created in this auth rotation.
- Routed prompts: 0; duplicate prompts: 0.

## Changed paths
- docs/prompt_queues/backend_code_analysis.md
- .ai/runs/2026-09-19-BACKEND-ANALYSIS-001-evidence.md

## Validation
Validation run: dotnet restore exit 0 with advisory warnings; Release analyzer build exit 0; dotnet format exit 2 with existing whitespace findings; vulnerable-package scan exit 0 with advisory inventory; focused auth/ownership tests 28/28 pass; git diff --check pass; prompt validation pass; documentation health 25 documents/0 issues; evidence validation pass; run analysis pass; agent-system validation pass.
Validation not run: No runtime source/test changes or PostgreSQL proof required by this docs/evidence-only audit; no GitHub Actions evidence found via connector.

## Exceptions and learning
Mistakes observed: BACKEND-MISTAKE-EVIDENCE-001, BACKEND-MISTAKE-VALIDATION-001, BACKEND-MISTAKE-AUDIT-001, BACKEND-MISTAKE-PROCESS-002
Waste: agent_run rejected unsupported area analysis; restarted with supported docs-evidence area; dotnet format produced a large pre-existing baseline only.
Missed: No unique auth/ownership finding beyond covered public identity and claim-scoped paths.
Follow-up: Schedule dependency advisory remediation as a separate owner; next analysis cycle should rotate to idempotency and settlement.
Residual risk: Existing vulnerable package advisories and repository-wide whitespace drift remain unowned by this auth rotation.
Documentation impact: Updated only the analysis queue claim/status; no durable playbook change.
Cross-repo impact: No Flutter changes; existing auth companion remains unchanged.

## Delivery
State: Done
Branch/PR: agent/BACKEND-ANALYSIS-001; direct main delivery after evidence validation
Commit SHA: self
Completion %: 95
