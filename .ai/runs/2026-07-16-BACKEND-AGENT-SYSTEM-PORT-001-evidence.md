# BACKEND-AGENT-SYSTEM-PORT-001 Evidence

Prompt ID: BACKEND-AGENT-SYSTEM-PORT-001
Queue: docs/prompt_queues/README.md
Agent/tool: ChatGPT with GitHub connector and local Python/container validation fixture
Model provider: OpenAI
Model name/id: GPT-5.6 Thinking
Model mode/settings: reasoning, connector-backed repository writes
Client/IDE: ChatGPT Android/web service environment
Run mode: docs-evidence
Token budget: high
Actual context: Flutter agent docs/validators, current backend agent docs/queues/tooling, target branch contents
Started from queue status: user-assigned unqueued cross-repo tooling port
Local collision check: backend already owned evidence/mistake/run-log rules; new work was limited to missing entrypoint, source map, prompt/command/system gates and queue router/lifecycle
Relevant prior mistakes read: none - no exact BACKEND-MISTAKE card changed the ownership decision
How this run avoids prior mistakes: preserved existing backend domain/evidence owners, excluded Flutter runtime paths/commands and made prompt validation forward-only
Elapsed time: unknown-not-recorded
Phase time breakdown: unknown-not-recorded
Commit SHA: e418fb933fd46beb8f687f8f0be040d92a11ff87

## Files inspected

- Flutter `AGENTS.md`, `.ai/README.md`, `.ai/SOURCE_OF_TRUTH.md`, `.ai/TOKEN_BUDGETS.md`, `.ai/VALIDATION_SELECTOR.md`, `.ai/PROMPT_LINT_CHECKLIST.md`
- Flutter `docs/AGENT_COMMAND_PLAYBOOK.md`, prompt validators, quality registry/runner and guarded command runner
- Backend `AGENTS.md`, `docs/DOCS_INDEX.md`, `docs/AGENT_SHARED_OPERATING_STANDARD.md`, `.ai/RUN_LOG_TEMPLATE.md`
- Backend `scripts/validate_agent_evidence.py`, evidence workflow and current prompt queues

## Files changed

- `AGENTS.md`
- `docs/DOCS_INDEX.md`
- `.ai/README.md`
- `.ai/SOURCE_OF_TRUTH.md`
- `.ai/TOKEN_BUDGETS.md`
- `.ai/VALIDATION_SELECTOR.md`
- `.ai/PROMPT_LINT_CHECKLIST.md`
- `docs/AGENT_COMMAND_PLAYBOOK.md`
- `docs/ai/TASK_TEMPLATE.md`
- `docs/prompt_queues/README.md`
- `docs/prompt_queues/PROMPT_LIFECYCLE.md`
- `scripts/run_guarded.py`
- `scripts/test_run_guarded.py`
- `scripts/validate_agent_prompt.py`
- `scripts/test_validate_agent_prompt.py`
- `scripts/validate_agent_system.py`
- `scripts/test_validate_agent_system.py`
- `.github/workflows/agent-system-validation.yml`
- this run log

## Commands run

- `python -m py_compile scripts/*.py` -> pass in local assembled fixture
- `python -m unittest -v scripts/test_run_guarded.py` -> pass, 6 tests
- `python -m unittest -v scripts/test_validate_agent_prompt.py` -> pass, 6 tests
- `python -m unittest -v scripts/test_validate_agent_system.py` -> pass, 4 tests
- `python scripts/validate_agent_prompt.py docs/ai/TASK_TEMPLATE.md` -> pass
- `python scripts/validate_agent_prompt.py` -> pass against assembled new docs/queue fixture
- `python scripts/validate_agent_system.py` -> pass against a synthetic complete backend tree containing the target's known existing linked files

## What was done

- Ported the useful low-context workflow concepts without copying Flutter runtime commands or mobile-specific ownership.
- Added a backend repository-root/bootstrap entrypoint and explicit source-of-truth owner map.
- Added bounded time/context/search/edit budgets and a backend validation selector.
- Added forward-only prompt contract/admission validation so historical queue prose is not mass-migrated.
- Added a guarded command runner with wall/idle timeout and process-tree termination.
- Added mechanical wiring/link/Flutter-command-leak validation and an automatic GitHub Actions workflow.
- Added canonical backend queue routing/lifecycle and updated `AGENTS.md`/`DOCS_INDEX.md`.
- Preserved the existing backend evidence validator, run-log template, mistake ledger and domain-specific rules as canonical owners.

## What was missed

- No .NET runtime code was changed or validated because this is a docs/agent-tooling-only port.
- A full local checkout of the backend repository was unavailable; full-tree link validation must be confirmed by the PR workflow.
- Existing historical queue files were deliberately not migrated to prompt contract v2/v3.
- Flutter's worktree/claim/supervisor stack was not copied because the backend has no matching established coordination mechanism and GitHub branch/PR ownership is sufficient for this slice.

## Validation run

- Local Python unit and synthetic wiring validation passed as listed above.
- Target-branch GitHub Actions result: pending until PR creation/run inspection.

## Validation not run

- `dotnet build` / `dotnet test`: not run - no backend runtime source changed and connector environment has no full checkout.
- Full backend `python scripts/validate_agent_evidence.py`: not run - connector-only target tree; existing evidence workflow remains authoritative.
- Main verification: not run - branch/PR delivery only, no merge requested.

## Waste categories

- connector write overhead from publishing several text files through repository APIs;
- source/target documentation overlap review needed to avoid duplicate owners.

## Mistakes observed

- none

## Where time/context was wasted

- Initial candidate scope included porting more of the Flutter validator registry than the backend needed.

## Why waste happened

- The Flutter repository has a mature multi-tool supervisor system, while the backend already had evidence/mistake/domain owners but lacked only selected workflow layers.

## What the next agent should avoid

- Do not copy Flutter runtime validators, self-hosted workflow assumptions or worktree claim mechanics into the backend without a proven backend need.
- Do not mass-reformat historical queues merely to satisfy the forward-only v2/v3 contract.

## Docs/rules updated to prevent repeat

- `.ai/SOURCE_OF_TRUTH.md`
- `.ai/PROMPT_LINT_CHECKLIST.md`
- `docs/AGENT_COMMAND_PLAYBOOK.md`
- `docs/prompt_queues/PROMPT_LIFECYCLE.md`

## Queue updated

- Added `docs/prompt_queues/README.md` as canonical router; existing prompt bodies/statuses were not changed.

## New optimized prompt added

- `docs/ai/TASK_TEMPLATE.md`

## Follow-up prompt

- none unless PR CI exposes broken target-tree links or workflow portability issues

## Completion %

- 88%

## Residual risk

- PR workflow and exact head-SHA checks still need inspection.
- The branch is not merged to `main`; documentation/tooling delivery is not main-verified.
- Full backend runtime validation is intentionally outside scope.
