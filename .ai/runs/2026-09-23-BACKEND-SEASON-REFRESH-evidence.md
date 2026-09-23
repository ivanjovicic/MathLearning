# Season/premium current-main refresh

Evidence format: v2
Prompt ID: PR-25 / season authority package
Queue: user-assigned
Run mode: audit + bounded implementation
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
Commit SHA: self
