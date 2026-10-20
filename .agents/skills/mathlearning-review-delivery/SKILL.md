---
name: mathlearning-review-delivery
description: Review MathLearning backend PRs, CI failures, agent docs and delivery status using canonical run budgets, documentation mapping and ownership rules.
---
# Backend PR and evidence discipline
1. Read `AGENTS.md`, `.ai/README.md`, `.ai/SOURCE_OF_TRUTH.md`, `.ai/VALIDATION_SELECTOR.md` and existing task owner. Never create a duplicate queue from a direct bounded assignment.
2. Use `scripts/agent_run.py` and token budget/changed-path limits as defined by the repo. Establish one hypothesis, test, avoid paths and stop criterion; do not broaden into another subsystem.
3. Review diff and nearest test first; classify CI failures as source, fixture, provider/environment or tooling. Prove the defect before changing application code.
4. Run the minimum required guarded focused tests and docs health/agent validators for changed docs; source changes must have an actual regression counterexample.
5. Record validation with exact SHA, command, time and result. Old SHA or queued CI is not a pass on latest main. Never release an ambiguous claim after an arbitrary 24-hour age.
6. Deliver scoped branch/PR according to repo workflow; verify required checks and ancestry, update one run log and affected registered docs only. Generated current-work summaries must not become secondary status/owner authority.
**Stop:** uncertain queue owner, red required check, unresolved model/migration permission, CI/proof missing or attempted force overwrite.
