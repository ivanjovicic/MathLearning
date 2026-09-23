# BACKEND-TEST-050 current-main refresh

Evidence format: v2
Prompt ID: BACKEND-TEST-050
Queue: user-assigned
Agent/tool: Codex
Model provider: unknown-not-exposed
Model name/id: unknown-not-exposed
Client/IDE: Codex desktop
Run mode: tests
Token budget: high
Started at UTC: 2026-09-23T15:00:00Z
Completed at UTC: 2026-09-23T15:08:00Z
Elapsed time: 8m 00s
Relevant prior mistakes read: BACKEND-MISTAKE-EVIDENCE-001, BACKEND-MISTAKE-VALIDATION-001, BACKEND-MISTAKE-DB-001
How this run avoids prior mistakes: applies the draft uniqueness rule through the EF model and generated migration while preserving current-main behavior
Owner/hypothesis: design-token draft creation needs a database-enforced single-draft authority under concurrency
Files inspected: 22
Files changed: 7
Searches: 5
Validation runs: 2
Failed retries: 0
Current-main base SHA: `6a8f3ae`
Old PR reviewed: #20 / `cursor/backend-test-050-design-token-version-fa87`
Fresh PR: #36
Merge SHA: `c7e336f`
Completion %: 85

## Outcome

- Draft identities use timestamp plus random suffix and no longer rely on second-resolution timestamps.
- Existing drafts are reused and published sources are cloned without tracking.
- Filtered unique index `UX_DesignTokenVersion_Draft` permits exactly one Draft row and closes the concurrent creator race.

## Validation

- Design-token focused suite: 4 passed, 0 failed.
- EF migration generation/build succeeded.
- Live PostgreSQL migration/provider execution: not run; CI pending.

Documentation impact: updated canonical queue, prompt status, and this run log.
Cross-repo impact: none.
Residual risk: live PostgreSQL upgrade/concurrency proof.
Mistakes observed: none
Waste: none
Missed: live PostgreSQL migration/provider execution was not run
Follow-up: provider migration and concurrent draft-creation validation
State: Needs validation
Branch/PR: main after fresh PR #36; old PR #20 superseded
Commit SHA: self
