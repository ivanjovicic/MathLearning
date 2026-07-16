# MathLearning Backend AI Workflow Entrypoint

Last aligned: 2026-07-16  
Owner: `backend-agent-system`

This is the low-context entrypoint for repository-aware agents working in `ivanjovicic/MathLearning`. It routes the next decision to one canonical owner; it is not a second copy of every rule. Resolve disagreements through [`SOURCE_OF_TRUTH.md`](SOURCE_OF_TRUTH.md).

## Fast path

For a normal file-changing task:

1. prove the repository root;
2. identify one bounded outcome, one authoritative owner, selected failure modes and focused proof;
3. select the queue owner through [`../docs/prompt_queues/README.md`](../docs/prompt_queues/README.md) and use the status semantics in [`../docs/prompt_queues/PROMPT_LIFECYCLE.md`](../docs/prompt_queues/PROMPT_LIFECYCLE.md);
4. choose the time/context limit from [`TOKEN_BUDGETS.md`](TOKEN_BUDGETS.md);
5. read only the prompt, relevant backend invariant, target source, nearest focused test and mapped owning document;
6. make the smallest owned change;
7. choose proof through [`VALIDATION_SELECTOR.md`](VALIDATION_SELECTOR.md) and command form through [`../docs/AGENT_COMMAND_PLAYBOOK.md`](../docs/AGENT_COMMAND_PLAYBOOK.md);
8. record evidence with [`RUN_LOG_TEMPLATE.md`](RUN_LOG_TEMPLATE.md);
9. deliver using the commit/push policy in [`../AGENTS.md`](../AGENTS.md) and keep queue/evidence status honest.

Do not edit before the repository, outcome, owner, expected changed files and focused proof are known.

## Canonical owners

| Need | Read |
|---|---|
| Backend engineering invariants | [`../AGENTS.md`](../AGENTS.md) |
| Documentation routing and current architecture/status docs | [`../docs/DOCS_INDEX.md`](../docs/DOCS_INDEX.md) |
| Resolve conflicting agent rules | [`SOURCE_OF_TRUTH.md`](SOURCE_OF_TRUTH.md) |
| Time, reads, searches and changed-file limits | [`TOKEN_BUDGETS.md`](TOKEN_BUDGETS.md) |
| Validation selection | [`VALIDATION_SELECTOR.md`](VALIDATION_SELECTOR.md) |
| New prompt contract and active admission | [`PROMPT_LINT_CHECKLIST.md`](PROMPT_LINT_CHECKLIST.md) |
| Command timeouts and safe `dotnet`/Git execution | [`../docs/AGENT_COMMAND_PLAYBOOK.md`](../docs/AGENT_COMMAND_PLAYBOOK.md) |
| Queue discovery | [`../docs/prompt_queues/README.md`](../docs/prompt_queues/README.md) |
| Queue state, ownership and closure | [`../docs/prompt_queues/PROMPT_LIFECYCLE.md`](../docs/prompt_queues/PROMPT_LIFECYCLE.md) |
| Run evidence | [`RUN_LOG_TEMPLATE.md`](RUN_LOG_TEMPLATE.md) and [`../docs/AGENT_RUN_LOG_ENFORCEMENT.md`](../docs/AGENT_RUN_LOG_ENFORCEMENT.md) |
| Bug-fix regression expectations | [`../docs/BUGFIX_PATTERN_GUARDRAILS.md`](../docs/BUGFIX_PATTERN_GUARDRAILS.md) |
| Runtime architecture | [`../docs/ARCHITECTURE_OVERVIEW.md`](../docs/ARCHITECTURE_OVERVIEW.md) |
| HTTP contract and route ownership | [`../docs/API_ENDPOINT_INVENTORY.md`](../docs/API_ENDPOINT_INVENTORY.md) |

Open only the owner needed for the next decision. Do not chain-read the table.

## Repository root bootstrap

Before prompt discovery or source search, prove that the current directory is the backend checkout. Required markers:

```text
.git
AGENTS.md
MathLearning.slnx
src/MathLearning.Api/Program.cs
tests/MathLearning.Tests/MathLearning.Tests.csproj
```

A generic task folder, output directory or document session is not the repository. Do not recursively search the user profile or unrelated drives for a checkout. Use the configured repository path, verify the markers and record:

```text
Repository root:
Working branch:
Delivery target: main | branch/PR | none
```

When the exact checkout is unavailable, stop local implementation and use an explicit connector-only evidence state. Do not claim local `dotnet`, Python or Git validation.

## Interpretation gate

Before broad reading or editing, record:

```text
Interpreted outcome:
Source of truth used:
Assumptions:
Material ambiguity: none | <description>
Run lane and budget:
Risk/ownership model:
Failure modes selected:
Initial reads and search budget:
Expected changed files and limit:
Focused completion proof:
Stop/handoff trigger:
Documentation impact expectation:
```

Material ambiguity involving authentication authority, target-user scope, persistence, privacy/security, idempotency/economy settlement, destructive migration, public API/schema or acceptance criteria stops implementation.

## Minimal reads

Default assigned-task read set:

1. exact prompt or queue row;
2. relevant section of `AGENTS.md`;
3. target source segment;
4. nearest focused test;
5. mapped owning documentation;
6. one relevant `BACKEND-MISTAKE-*` card when it changes the decision.

Do not read every audit, full queue history, all migrations or the whole solution by default. Every search answers one written question.

## Execution lanes

| Lane | Use when | Output |
|---|---|---|
| `known-fix` | owner, root cause and proof are clear | smallest implementation plus focused regression proof |
| `investigation` | owner or cause is uncertain | finite diagnosis and one implementation handoff; no speculative runtime edit |
| `validation-only` | committed work needs executable proof | exact build/test/workflow evidence and only proven minimal repair |
| `tests` | production behavior exists but proof is missing | bounded tests without opportunistic runtime redesign |
| `docs-evidence` | docs, queue, evidence or agent tooling only | internally consistent artifacts and mechanical checks |
| `audit` | finite multi-area analysis | findings plus bounded owners; implementation is split |
| `review` | an existing change needs a verdict | diff/CI verdict; no repository edit unless separately authorized |

One run has one primary lane. A second unexpected subsystem or a second falsified hypothesis triggers handoff.

## Backend ownership boundaries

- Endpoints bind/validate/authenticate and call the owning service; they do not become a second business-logic owner.
- Authenticated server identity, not request-body `userId`, owns mobile-facing writes.
- Existing idempotency ledgers and domain tables remain authoritative; do not invent another settlement pattern without an explicit architecture decision.
- EF Core mappings, migrations and model snapshots must remain aligned; migration work requires clean-database and upgrade-path reasoning.
- Mobile-facing contract changes must check the Flutter contract documents and record cross-repo impact.
- Background jobs require bounded work, cancellation, retry/backoff and provider-appropriate concurrency proof.

## Prompt and queue changes

- New or materially rewritten non-trivial prompts use `Prompt contract: v2`.
- Newly admitted active prompts also use `Prompt admission: v3`.
- Historical unchanged queue prose remains historical and is not mass-migrated.
- A prompt that lacks current evidence, deduplication, owner boundaries, dependency/collision rules or executable proof is not `Ready`.

Use [`PROMPT_LINT_CHECKLIST.md`](PROMPT_LINT_CHECKLIST.md) and the task template at [`../docs/ai/TASK_TEMPLATE.md`](../docs/ai/TASK_TEMPLATE.md).

## Validation and command honesty

Follow [`VALIDATION_SELECTOR.md`](VALIDATION_SELECTOR.md). Long-running commands use [`../scripts/run_guarded.py`](../scripts/run_guarded.py) as defined by [`../docs/AGENT_COMMAND_PLAYBOOK.md`](../docs/AGENT_COMMAND_PLAYBOOK.md).

Evidence order:

```text
current contract or reproducer
→ smallest static/build check
→ nearest focused behavior and counterexample test
→ docs/prompt/evidence checks
→ wider suite only for a named wider risk
```

A test file existing, code compiling, planned CI or a queued workflow is supporting information, not completed runtime proof. Never report CI green without inspecting the exact run for the delivered SHA.

## Delivery and evidence

```text
local edit / commit / pushed branch / open PR != complete
honest focused proof + delivered commit + synchronized queue/evidence = completion candidate
```

Every non-trivial task records files read/changed, commands run or skipped, documentation impact, cross-repo impact, delivery state and residual risk. Connector-only work states which local commands were not run.

## Stop rules

Stop implementation and hand off when:

- repository root cannot be proven;
- material authority remains ambiguous;
- selected time/context/search/edit budget is reached;
- a second subsystem appears;
- two hypotheses are falsified;
- the same command fails or times out after one classified retry;
- required proof cannot execute;
- another worker owns the prompt or overlapping paths;
- branch/direct-main ownership is unclear.
