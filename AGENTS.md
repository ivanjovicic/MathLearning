# MathLearning Backend - AI Agent Rulebook

Last aligned: 2026-07-16  
Owner: `backend-agent-system`

Follow this file for every change in `ivanjovicic/MathLearning`. Current code, focused tests and executable tooling override stale prose. Resolve agent-document disagreements through [`.ai/SOURCE_OF_TRUTH.md`](.ai/SOURCE_OF_TRUTH.md).

## 1. Start narrowly

Start with [`.ai/README.md`](.ai/README.md). For most tasks read only:

1. exact assigned prompt/queue row;
2. relevant section of this file;
3. target source and nearest focused test;
4. one mapped owner from [`docs/DOCS_INDEX.md`](docs/DOCS_INDEX.md);
5. one relevant mistake from [`docs/ai/learning/MISTAKE_LEDGER.md`](docs/ai/learning/MISTAKE_LEDGER.md).

High-risk fixes also read [`docs/BUGFIX_PATTERN_GUARDRAILS.md`](docs/BUGFIX_PATTERN_GUARDRAILS.md). Do not reread the whole repository by default.

Before editing, record:

```text
Interpreted outcome:
Source of truth used:
Assumptions:
Material ambiguity:
Risk/ownership model:
Failure modes selected:
Initial reads/search budget:
Expected changed files/limit:
Focused completion proof:
Stop/handoff trigger:
Documentation impact expectation:
Working branch/PR:
Delivery target:
```

Use [`.ai/TOKEN_BUDGETS.md`](.ai/TOKEN_BUDGETS.md). Every search answers one written question.

## 2. Canonical agent owners

| Area | Owner |
|---|---|
| Low-context entry and repository-root proof | [`.ai/README.md`](.ai/README.md) |
| Agent-document authority | [`.ai/SOURCE_OF_TRUTH.md`](.ai/SOURCE_OF_TRUTH.md) |
| Time/context/read/edit limits | [`.ai/TOKEN_BUDGETS.md`](.ai/TOKEN_BUDGETS.md) |
| Validation selection | [`.ai/VALIDATION_SELECTOR.md`](.ai/VALIDATION_SELECTOR.md) |
| Prompt contract/admission | [`.ai/PROMPT_LINT_CHECKLIST.md`](.ai/PROMPT_LINT_CHECKLIST.md) |
| Command limits/guarded execution | [`docs/AGENT_COMMAND_PLAYBOOK.md`](docs/AGENT_COMMAND_PLAYBOOK.md) |
| Queue discovery/order | [`docs/prompt_queues/README.md`](docs/prompt_queues/README.md) |
| Queue states/closure | [`docs/prompt_queues/PROMPT_LIFECYCLE.md`](docs/prompt_queues/PROMPT_LIFECYCLE.md) |
| Shared cross-repo minimum | [`docs/AGENT_SHARED_OPERATING_STANDARD.md`](docs/AGENT_SHARED_OPERATING_STANDARD.md) |
| Evidence fields/score caps | [`docs/AGENT_RUN_LOG_ENFORCEMENT.md`](docs/AGENT_RUN_LOG_ENFORCEMENT.md), [`.ai/RUN_LOG_TEMPLATE.md`](.ai/RUN_LOG_TEMPLATE.md) |

Do not copy full mechanics into prompts. Link to the owner and include task-specific values.

## 3. No-wandering execution

```text
DISCOVER/ASSIGN → INTERPRET → PROVE OWNER → PATCH → VALIDATE → DELIVER → VERIFY → CLOSE/HANDOFF
```

- Do not patch before one owner, one hypothesis, expected changed files and focused proof are known.
- Do not add paths outside the packet without recording scope drift.
- One invalidated hypothesis permits one replacement; a second falsification stops implementation.
- Do not patch product code for environment, harness, docs-tool or CI-infrastructure failure.
- A second unexpected subsystem, unclear authority or unavailable required proof stops the run.
- Explicit user assignment/current branch/open PR owns bounded paths; do not create a duplicate queue owner.

```text
local changes / commit / pushed branch / open PR != Done
required proof + delivered commit + exact target verification + synchronized evidence/status = Done candidate
```

## 4. Repository role and architecture

This is the ASP.NET Core backend/API for the MathLearning Flutter app. It owns auth, profiles, progress, quiz/SRS/practice, Daily Run, economy, cosmetics, leaderboard, sync, health/logging/maintenance, EF Core persistence/migrations, authoritative settlement/idempotency and backend contract tests.

Boundaries:

- `src/MathLearning.Api/Program.cs` owns startup/middleware/endpoint mapping.
- Endpoint files live in `src/MathLearning.Api/Endpoints/`.
- Application/service behavior lives under `src/MathLearning.Application/` and `src/MathLearning.Infrastructure/`.
- Domain entities live under `src/MathLearning.Domain/`.
- EF Core context/migrations live under `src/MathLearning.Infrastructure/Persistance/`.
- Tests live under `tests/MathLearning.Tests/`.

Endpoints authenticate, bind/validate, call the owner and project contract-shaped results. Do not make endpoints a second business-logic owner.

## 5. Auth and user scope

- Mobile-facing writes are scoped by authenticated server user id.
- Never trust request-body `userId` as mutation authority.
- Route `userId` must equal the authenticated user unless explicitly admin-only.
- Admin-targeted routes keep actor and target separate.
- Add/update anonymous, correct-user, wrong-user and wrong-role proof when ownership changes.
- Authorization metadata alone is not enough; execute endpoint/integration tests.

## 6. Idempotency and settlement

Retryable mobile mutations use stable operation identity, normally:

```text
userId + operationType + operationId/idempotencyKey
```

Current strategies remain authoritative: shared `IdempotencyLedger`, `economy_transactions`, `cosmetics_idempotency_ledger`, and Daily Run chest domain-table policy. Do not add another pattern without proving current owners cannot express the invariant and documenting the decision.

Expected behavior:

- first request mutates once and stores settled result;
- duplicate same payload replays it;
- conflicting same key follows owning conflict policy;
- failure/cancellation does not leave completed ledger without domain mutation;
- different users remain isolated;
- ledger/domain writes share a transaction where applicable.

## 7. Endpoint/mobile contract

- Synchronize route changes with [`docs/API_ENDPOINT_INVENTORY.md`](docs/API_ENDPOINT_INVENTORY.md).
- Align mobile payload/behavior with `ivanjovicic/Mathlearning-Mobile-App/docs/mobile_api_contract.md`.
- Do not invent mobile payloads from memory.
- Contract-touching evidence records other repo checked, docs touched/deferred and follow-up owner.
- Prefer canonical `/api/economy/*`, `/api/cosmetics/*`, `/api/quiz/srs/update`, `/api/daily-run/chest/claim` routes.
- Do not expand legacy routes without explicit compatibility ownership.

## 8. Database and migration safety

- Inspect existing migrations and DbContext mappings first.
- Unique indexes match exact auth/idempotency scope.
- Do not assume production auto-migrates.
- Do not weaken/skip schema-from-zero validation to obtain green CI.
- Provider-sensitive constraints, transactions and concurrency require PostgreSQL proof, not only InMemory.
- Durable schema/contract changes update their owning docs.
- Destructive commands require explicit ownership and verified non-production target.

## 9. Jobs, cache and observability

- Give claim/lease, retry/backoff, dead-letter and cancellation behavior one canonical owner.
- Avoid process-local locks for cross-instance invariants unless deployment boundary is explicit.
- Read endpoints must not hide expensive writes/rebuilds without documentation/tests.
- Cache invalidation follows authoritative write/settlement owner.
- Never log bodies, credentials, tokens or sensitive user data.
- Idempotency logs use safe metadata only.
- Admin/observability endpoints remain policy-protected.

## 10. Validation

Choose proof through [`.ai/VALIDATION_SELECTOR.md`](.ai/VALIDATION_SELECTOR.md) and commands through [`docs/AGENT_COMMAND_PLAYBOOK.md`](docs/AGENT_COMMAND_PLAYBOOK.md).

```text
contract/reproducer
→ focused behavior/counterexample test
→ provider/build/static proof
→ docs/prompt/evidence checks
→ wider suite or exact CI only for named wider risk
```

- Bug fixes add the smallest regression test unless evidence explains why executable proof is impractical.
- Test duplicate/conflict/auth/cross-user/error/cancel/rollback/concurrency/provider cases, not only success.
- Test existence/compilation is not execution proof.
- `queued`/`in_progress` CI is not green.
- Never claim Actions success without exact target SHA, jobs/checks and required artifacts.
- Connector-only work records local commands as not run.

## 11. Prompt, docs and evidence

- New/materially changed non-trivial prompts use `Prompt contract: v2`; new/promoted active owners also use `Prompt admission: v3`.
- Admission requires current evidence, deduplication, one owner, dependencies/collisions, impact, bounded scope and executable proof.
- [`docs/DOCS_INDEX.md`](docs/DOCS_INDEX.md) maps canonical vs audit/status docs.
- Update the durable owner when behavior/contract/validation/workflow changes; do not create a second owner.
- Dated audits, queues and run logs are evidence/history, not architecture authority.

Use one documentation impact statement:

```text
Documentation impact: updated <paths>
Documentation impact: none - <specific reason>
Documentation impact: follow-up <ID> - <specific reason>
```

Every non-trivial task updates `.ai/runs/<yyyy-mm-dd>-<prompt-id>-evidence.md` from [`.ai/RUN_LOG_TEMPLATE.md`](.ai/RUN_LOG_TEMPLATE.md). Record interpretation, files, commands run/skipped, validation, mistakes/waste/missed work, docs/cross-repo impact, branch/PR, commit/merge SHA, delivery verification and residual risk.

Do not claim Done/100% while target proof, delivery, evidence/status sync or material residual work is missing. Placeholder commit text remains non-Done.

## 12. Delivery and final response

Use a bounded task branch/PR for broad docs/tooling, workflow, migration or high-risk runtime changes unless direct main is explicitly permitted.

Final response includes:

- run-log path/fallback reason;
- mistake IDs or `none`;
- branch/PR and commit SHA(s);
- files changed;
- validation executed/skipped;
- exact CI/main verification when required;
- completion state, residual risk and next owner.
