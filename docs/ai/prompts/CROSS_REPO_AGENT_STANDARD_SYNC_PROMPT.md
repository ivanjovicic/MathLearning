# Cross Repo Agent Standard Sync Prompt

Prompt ID: XREPO-AGENT-STANDARD-SYNC-001

Run mode: docs/evidence

Goal: keep MathLearning backend, MathLearning Flutter, and AgentsWatch agent rules aligned.

Read in each repo: AGENTS.md, docs/DOCS_INDEX.md, docs/AGENT_SHARED_OPERATING_STANDARD.md, run-log gate if present, run-log template if present, mistake ledger if present, and the main prompt queue index.

Check: source-of-truth order, one prompt / one mode, token budget, run-log fields, score caps, mistake learning, docs-only vs runtime wording, validation honesty, cross-repo sync fields, and final response fields.

Allowed edits: shared standard docs, AGENTS.md references, DOCS_INDEX references, missing run-log templates, missing mistake-learning templates, compact evidence logs.

Avoid: runtime edits, broad roadmap rewrites, replacing repo-specific guardrails, or claiming validation that was not run.

Validation: git diff --check if available, verify referenced paths exist.

Final response: differences found, files changed, validation, commit SHAs, residual risk, next prompt.
