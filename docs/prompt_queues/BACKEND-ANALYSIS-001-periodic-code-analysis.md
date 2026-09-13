# BACKEND-ANALYSIS-001 - Periodic backend code analysis and prompt triage

Prompt contract: v2
Prompt admission: v3
Repository: ivanjovicic/MathLearning
Prompt ID: BACKEND-ANALYSIS-001
Queue: docs/prompt_queues/backend_code_analysis.md
Run lane: audit
Token budget: medium
Timebox: 30 minutes per cycle; split before exceeding the limits

Task:

Run one bounded backend code-analysis cycle using `docs/BACKEND_CODE_ANALYSIS_PLAYBOOK.md`, select exactly one rotation focus, classify the results, and write at most three new bounded implementation/test prompts for findings that are not already owned. Do not change `src/**` or `tests/**` in this audit prompt.

Source of truth:

- current source and focused tests;
- `docs/BACKEND_CODE_ANALYSIS_PLAYBOOK.md`;
- `AGENTS.md`, `docs/BUGFIX_PATTERN_GUARDRAILS.md` and the relevant contract/performance owner;
- current queue rows and `.ai/runs` evidence.

Interpretation before work:

Static output is a lead, not proof. A finding is `runtime-confirmed` only after a focused reproducer/provider test. Existing owners and current tests override stale audit prose.

Ambiguity rule:

If a hit maps to an existing runtime owner, update/link that owner instead of creating a duplicate prompt. If the result depends on Flutter, production telemetry or unavailable PostgreSQL evidence, record the named dependency and keep it `deferred`.

Risk/ownership model:

One audit owner writes the run log and queue changes. Implementation owners fix runtime behavior later. Authenticated identity, service/ledger settlement, EF mappings/migrations and mobile payload owners remain authoritative.

Test-first contract:

- Exception: this is an audit/docs-only prompt and does not change runtime behavior.
- Validation proof: documentation health, prompt, evidence and agent-system validators prove the process changes.
- Routed runtime prompts must contain their own pre-change failing proof, post-change passing proof and counterexample.

Problem evidence:

- The repository has build, test/coverage and agent-system validation, but no single recurring process for combining free .NET analyzers, format checks and dependency advisory output into unique repair prompts.
- Without a bounded cycle, analyzer warnings and undocumented patterns can remain disconnected from the queue or be overclaimed as fixes.

Deduplication check:

- Search the current router for the finding and affected owner.
- Search active queue files and `.ai/runs` for related `BACKEND-MISTAKE-*` IDs.
- Never reuse an ID and never duplicate `BE-PERF-*`, `BACKEND-API-DB-*` or existing test owners.

Priority rationale:

P1 process gap: missed auth, idempotency, persistence or concurrency risks can become user-visible defects, while unbounded auditing creates false progress and duplicate work.

Dependencies/collisions:

- The periodic workflow may be unavailable or package advisory data may be stale.
- PostgreSQL and Flutter evidence are separate dependencies; existing canonical runtime prompts take precedence over new IDs.

Owner boundary:

This prompt owns analysis, classification, evidence and queue prompt creation only. It does not own runtime fixes, migrations, test implementation, CI policy changes or mobile edits.

Queue placement:

Keep this prompt in the active backend analysis queue and rerun it every two weeks with the next rotation focus. Archive only after a replacement cadence owner exists.

Failure-mode matrix:

- A build or analyzer failure must preserve its exact non-zero command result.
- A vulnerable package result must name the package and advisory output before remediation is proposed.

| Failure mode | Detection | Routing |
|---|---|---|
| Build/analyzer failure | exact non-zero command result | P1/P2 tooling or code-quality prompt with output |
| Vulnerable package | advisory command output and package path | P0/P1 dependency remediation prompt, no automatic upgrade |
| Auth/idempotency/EF/concurrency lead | source hit plus owner/test comparison | static-risk prompt with falsifier and provider requirement |
| Existing coverage | matching owner/test/run log | covered; no duplicate prompt |
| Missing provider/connector/telemetry | exact unavailable proof | deferred with named owner and unblock condition |

Execution packet:

1. Read the playbook, relevant `AGENTS.md` sections, selected focus owner, current queue router and the mistake index IDs selected by `scripts/agent_run.py`.
2. Run Tier 1 and Tier 2 commands from the playbook, capturing exact exit codes and package output. Do not hide failures behind `|| true`.
3. Inspect no more than 12 source/test files and no more than 8 candidate findings for the selected focus.
4. For each candidate, record evidence, affected owner, classification, priority, falsifier and required validation.
5. Add at most three new prompt files, each with one lane and one bounded outcome. Update the owning queue/router row immediately before commit.
6. Create or finish the compact v2 run log. Mark static findings as audit/static-risk, never as runtime-fixed.

Owned paths:

- `docs/BACKEND_CODE_ANALYSIS_PLAYBOOK.md`
- `docs/prompt_queues/BACKEND-ANALYSIS-*.md`
- the owning queue/router row;
- `.ai/runs/<date>-BACKEND-ANALYSIS-001-evidence.md`

Avoid paths:

- `src/**`, `tests/**`, migrations and generated registry output unless a separate implementation prompt owns them;
- Flutter repository files;
- broad refactors, automatic package upgrades and whole-repository unbounded reviews.

Documentation impact:

Update the playbook and queue only when the analysis process or ownership changes. Register durable playbook changes in `docs/DOCS_MANIFEST.json` and regenerate `docs/DOCS_REGISTRY.md`.

Acceptance criteria:

- Tier 1 and Tier 2 commands have exact recorded results or an honest unavailable reason.
- One rotation focus is named and the search/inspection limits are respected.
- Every candidate is classified and either linked to an existing owner, deferred with an owner, or routed to a unique prompt.
- No more than three new prompts are created and each contains a falsifier, required proof and stop condition.
- Queue status and run log agree; no static audit is described as a runtime fix.

Proof required:

- `git diff --check`;
- `python scripts/check_documentation_health.py --full-links` when durable docs change;
- `python scripts/validate_agent_prompt.py --changed-from <base-sha>`;
- `python scripts/validate_agent_evidence.py --changed-from <base-sha> --verify-git`;
- `python scripts/analyze_agent_runs.py --changed-from <base-sha> --fail-on-regression`.

Validation:

```powershell
python scripts/run_guarded.py --timeout-seconds 180 -- dotnet build MathLearning.slnx -c Release --no-restore -p:RunAnalyzers=true -p:EnforceCodeStyleInBuild=true
python scripts/run_guarded.py --timeout-seconds 180 -- dotnet format MathLearning.slnx --verify-no-changes --severity warn --no-restore
python scripts/run_guarded.py --timeout-seconds 180 -- dotnet list MathLearning.slnx package --vulnerable --include-transitive
```

Completion gate:

The audit is complete only when evidence and queue routing are committed and pushed. Runtime fixes remain separate prompts and are not claimed here. Use `No GitHub Actions evidence found via connector` when no connector run/artifact was checked.

Stop conditions:

Stop and split after 30 minutes, 12 inspected source/test files, 8 candidates, 3 new prompts, a second rotation focus or any request to edit runtime code.

Evidence:

Use `.ai/RUN_LOG_TEMPLATE.md` and `scripts/agent_run.py`; record mistakes, waste, missed work, follow-up, residual risk, validation and `Commit SHA: self`.
