# USER-20261115-ISSUE47-QUIZ-ANSWER Evidence

Evidence format: v2
Prompt ID: USER-20261115-ISSUE47-QUIZ-ANSWER
Queue: user-assigned
Agent/tool: unknown-not-exposed
Model provider: unknown-not-exposed
Model name/id: unknown-not-exposed
Client/IDE: unknown-not-exposed
Run mode: investigation
Token budget: high
Started at UTC: 2026-11-15T15:04:42Z
Completed at UTC: 2026-11-15T15:34:48Z
Elapsed time: 30m 6s
Relevant prior mistakes read: BACKEND-MISTAKE-EVIDENCE-001, BACKEND-MISTAKE-VALIDATION-001, BACKEND-MISTAKE-IDEM-001, BACKEND-MISTAKE-IDEM-002, BACKEND-MISTAKE-XREPO-001
How this run avoids prior mistakes: apply BACKEND-MISTAKE-EVIDENCE-001; apply BACKEND-MISTAKE-VALIDATION-001; apply BACKEND-MISTAKE-IDEM-001; apply BACKEND-MISTAKE-IDEM-002; apply BACKEND-MISTAKE-XREPO-001
Owner/hypothesis: issue #47 quiz settlement; historical 42703 arose before commit from missing audit columns; current main has a registered repair and physical schema guard.
Files inspected: 16
Files changed: 1
Searches: 10
Validation runs: 8
Failed retries: 2

## Outcome
- Issue #47 comments confirm the historical 42703 on `UserAnswerAudits.ClientId` before commit. At the time of this 2026-11-15 run, `origin/main` was `59dc54d831270d751d3b7d98c8a8a3fb88a44088` and contained the registered repair and schema guard. No new backend defect was reproduced; correlated production persistence remains unverified.

## Changed paths
- .ai/runs/2026-11-15-USER-20261115-ISSUE47-QUIZ-ANSWER-evidence.md only; temporary anonymous probe removed

## Validation
Validation run: Restore pass; quiz idempotency 9/9; mobile, XP, quiz start, operation identity and ledger 40/40; isolated PostgreSQL settlement 1/1; PostgreSQL schema guard 2/2; anonymous HTTP probe 1/1 (53/53 test executions recorded). First combined build idle timed out; default localhost provider credentials failed, then isolated PG proof passed. These were local/test-environment results, not production recovery evidence.
Validation not run: No production SQL or POST; buffered Fly logs contain neither incident correlation ID. No new CI because no PR or code patch.

## Exceptions and learning
Mistakes observed: none
Waste: Initial agent area invalid; first build idle timeout; guarded timeout options rejected; local default PostgreSQL credential failed.
Missed: Exact Fly runtime database identity and incident scoped persistent rows not available. The task-created local PostgreSQL cluster was stopped; automatic approval review rejected removal of its directory as blocked by policy.
Follow-up: Follow the operator handoff in `docs/migration-drift-issue-47.md`: verify Fly-to-Neon identity, then inspect per-request persisted records using the original operation identity. Preserve that identity for any mobile retry.
Residual risk: Production settled outcome remains unknown until Fly-to-Neon identity and scoped persisted rows are verified. First-success telemetry is emitted before commit, and the endpoint records rollback for any thrown exception, so metrics alone cannot classify a commit-time disconnect. No deployment approved; documentation delivery is separate.
Documentation impact: No durable docs changed; docs/migration-drift-issue-47.md already owns historical repair evidence.
Cross-repo impact: Mobile agent owns stable operationId with idempotencyKey across uncertain response; backend rejects mismatched identity as conflict.

## Delivery
State: Blocked
Branch/PR: codex/issue47-ops-evidence; evidence preservation only, with no backend defect PR
Commit SHA: self
Completion %: 79
