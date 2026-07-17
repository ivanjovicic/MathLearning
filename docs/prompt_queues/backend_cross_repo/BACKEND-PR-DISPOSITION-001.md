# BACKEND-PR-DISPOSITION-001 — Review stale draft PR #3 against current main

Prompt contract: v2
Prompt admission: v3
Repository: ivanjovicic/MathLearning
Prompt ID: BACKEND-PR-DISPOSITION-001
Queue: docs/prompt_queues/backend_cross_repo_current_main_2026_07_17.md
Run lane: review
Token budget: low
Timebox: 15 minutes

Problem evidence:
- Draft PR #3 (`test: expand critical backend mutation coverage`) was created from an old base, has 15 commits and remains open while current `main` has advanced through later idempotency, provider and agent-system work.
- Its broad quiz/SRS/settlement test scope may contain useful unique fixtures, duplicated tests, stale contract assumptions or changes already superseded on main.
- The current router has no explicit owner for closing, superseding or extracting the remaining value from this PR.

Deduplication check:
- This is review/disposition only and does not replace runtime owners in performance, critical-risk or test queues.
- Current main tests/evidence and existing `BACKEND-TEST-*`/`BE-PERF-*` rows remain authoritative for implementation.
- Flutter stale-PR rules were reviewed as a process pattern, but this prompt owns only backend PR #3.

Priority rationale: P1 queue integrity because an old broad draft can be accidentally merged wholesale, repeatedly re-reviewed or used as false evidence of current coverage.

Dependencies/collisions:
- Read current PR head/base/diff/checks and current main before verdict.
- Do not edit runtime or rebase/merge the PR in this review run.
- Any unique still-needed test package receives a new narrow owner with current-main paths and proof.

Owner boundary:
- Review owns per-file/per-test disposition and the remote PR state/comment/closure decision.
- Runtime fixes, test implementation and CI repair remain separate prompts.
- Do not claim test success from old PR checks or source presence.

Queue placement: active review row after current-main runtime residuals; it may run in parallel because it performs no repository writes beyond review/evidence/PR disposition.

Task: Compare PR #3 against current main and classify every changed test/runtime/doc hunk as already present, obsolete, conflicting, uniquely valuable or unsafe, then close/supersede/retain with one exact next action.

Source of truth:
- PR #3 metadata, head SHA `c506b649...`, changed files, review threads and exact checks
- current backend main `76693a1dc64872993dd02816b161943ed52ecb36`
- current test inventory and owning `BACKEND-TEST-*` / `BE-PERF-*` queue rows
- delivered run evidence for quiz/SRS/idempotency/provider work

Interpretation before work: Build a table `PR file/test -> current-main equivalent -> owner -> unique value -> stale assumption -> disposition -> proof needed` before changing PR state.

Ambiguity rule: Do not merge, cherry-pick or close when a unique high-risk fixture cannot be mapped confidently. Use `Needs handoff` with the exact file/test and owner.

Risk/ownership model:
- Main code/tests and executed current evidence override the PR description.
- Old green checks do not prove compatibility with current main.
- A broad stale branch is never wholesale-merged to recover one useful test.
- Review comments and final state preserve why each unique item was retained or superseded.

Failure-mode matrix:
- A test name is absent on main but its invariant is covered under a different fixture.
- PR test asserts a contract that current main intentionally changed.
- Runtime helper changes are hidden inside a “test” PR.
- PR checks are missing/stale/failed or target an old base.
- One unique provider/concurrency fixture remains useful but collides with an active owner.

Execution packet:
- Initial reads: PR metadata/diff, current main equivalents and owning queue rows; maximum 12 files/entries.
- Search budget: maximum 3 searches for test names, symbols and owning prompt IDs.
- First hypothesis/falsifier: most broad PR content is superseded; falsify with a unique failing invariant absent from current main.
- Expected changed files: review evidence and router/queue status only; maximum 2 paths, plus remote PR comment/state.
- Focused proof: exact current-main test/symbol mapping and check status for each retained claim.
- Stop trigger: any runtime edit, unresolved unique high-risk fixture or unavailable PR diff/check evidence.

Owned paths:
- PR #3 review/disposition evidence.
- One compact router/archive status update when verdict is final.
- Remote PR comment/state when authorized.

Avoid paths:
- Runtime/test edits.
- Rebase, force-push or wholesale merge.
- Reopening completed prompt IDs.
- Claiming current test pass from old workflow runs.

Documentation impact: update the current queue/archive with the final PR disposition; no engineering contract change is allowed in this review lane.

Acceptance criteria:
1. Every changed file/test is mapped to current main and one canonical owner/disposition.
2. No stale runtime helper or contract assumption is silently merged.
3. Unique still-needed work has one new narrow prompt with exact failing/absent proof; duplicate work names its existing owner.
4. PR #3 receives a concrete retain/close/supersede decision and explanatory comment.
5. Final evidence records current main, PR head, exact checks reviewed and unresolved items.

Proof required:
- PR changed-file inventory and selected patches.
- Current-main symbol/test searches for each claimed duplicate or unique item.
- Exact workflow/check conclusion, not queued status.
- Final PR comment/state and queue/archive update.

Validation:
```powershell
python scripts/validate_agent_prompt.py docs/prompt_queues/backend_cross_repo/BACKEND-PR-DISPOSITION-001.md
python scripts/validate_agent_evidence.py --changed-from <base-sha> --verify-git
python scripts/check_documentation_health.py --full-links
```

Completion gate: No Done from “looks stale.” Completion requires per-item disposition, exact current-main/check evidence and a concrete remote PR state or named blocker.

Stop conditions:
- Stop before runtime/test edits.
- Stop when PR diff/check data is unavailable and record the exact connector/permission blocker.
- Stop at two changed paths, three searches or 15 minutes.

Evidence: .ai/runs/<yyyy-mm-dd>-BACKEND-PR-DISPOSITION-001-evidence.md
