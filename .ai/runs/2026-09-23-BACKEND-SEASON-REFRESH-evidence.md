# Season/premium current-main refresh

Evidence format: v2
Prompt ID: PR-25 / season authority package
Queue: user-assigned
Agent/tool: Codex
Model provider: unknown-not-exposed
Model name/id: unknown-not-exposed
Client/IDE: Codex desktop
Run mode: tests
Token budget: high
Started at UTC: 2026-09-23T15:32:00Z
Completed at UTC: 2026-09-23T15:45:00Z
Elapsed time: 13m 00s
Relevant prior mistakes read: BACKEND-MISTAKE-EVIDENCE-001, BACKEND-MISTAKE-VALIDATION-001, BACKEND-MISTAKE-IDEM-001
How this run avoids prior mistakes: ports only season entitlement and provenance deltas from the stale branch and validates replay/ownership cases on current main
Owner/hypothesis: season rewards must use server-owned entitlement, season XP, claim windows, and chest provenance
Files inspected: 24
Files changed: 6
Searches: 6
Validation runs: 1
Failed retries: 0
Current-main base SHA: `2eefbf5`
Old PR reviewed: #25 / `cursor/season-milestone-premium-gate-e301`
Fresh PR: #39
Merge SHA: `ad8c2a1`
Completion %: 85

## Outcome

- Premium milestone claims are deny-by-default without server-side entitlement.
- Milestone thresholds use season progress, not lifetime profile XP.
- Shared claim-window logic rejects inactive/expired seasons while honoring reward-lock.
- Daily Run chest provenance binds the chest day to one season and replays original XP.
- Milestone XP uses canonical XP settlement; fragment hints use server-side provenance.

## Validation

- Season/reward-track focused suite: 25 passed, 0 failed.
- PostgreSQL-specific season provider test: skipped because no validation database was configured.

Documentation impact: updated queue disposition and this run log.
Cross-repo impact: backend contract behavior is aligned; no Flutter source changed.
Residual risk: PostgreSQL season provenance/concurrency proof.
Mistakes observed: none
Waste: none
Missed: PostgreSQL-specific season provider test was skipped because no validation database was configured
Follow-up: provider season provenance and concurrency validation
State: Needs validation
Branch/PR: main after fresh PR #39; old PR #25 superseded
Commit SHA: self
