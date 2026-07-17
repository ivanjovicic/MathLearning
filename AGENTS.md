# MathLearning Backend - AI Agent Rulebook

Last aligned: 2026-07-17  
Owner: `backend-agent-system`

Current code, focused tests and executable tooling override stale prose. Resolve workflow-document conflicts through [`.ai/SOURCE_OF_TRUTH.md`](.ai/SOURCE_OF_TRUTH.md). Durable documentation ownership is defined by [`docs/DOCUMENTATION_SYSTEM.md`](docs/DOCUMENTATION_SYSTEM.md).

## 1. Start in under one minute

Start with [`.ai/README.md`](.ai/README.md). For a user-assigned bounded task, the request plus the task branch/PR is the owner; do not search queues or manufacture another prompt.

```powershell
python scripts/agent_run.py plan --area <area> --lane <lane> --budget <budget>
python scripts/agent_run.py start --prompt-id <ID> --queue user-assigned --area <area> --lane <lane> --budget <budget>
```

Read only:

1. exact request/owning row;
2. relevant section of this file;
3. target source and nearest focused test;
4. mapped required documentation from `python scripts/check_documentation_health.py --context <path>`;
5. mistake IDs selected by [`docs/ai/learning/MISTAKE_INDEX.json`](docs/ai/learning/MISTAKE_INDEX.json).

Open the full mistake ledger only to add/update a card. Use [`.ai/TOKEN_BUDGETS.md`](.ai/TOKEN_BUDGETS.md); every search answers one written question.

Before editing, know: outcome, authoritative owner, first hypothesis/falsifier, expected changed paths/limit, focused proof, stop trigger, documentation impact and delivery target.

## 2. No-wandering execution

```text
ASSIGN → PLAN → OWNER/HYPOTHESIS → PATCH → FOCUSED PROOF → DELIVER → CLOSE
```

- One run has one lane and one authoritative owner.
- A second subsystem or second falsified hypothesis ends implementation.
- Do not patch product code for a harness/docs/CI-infrastructure failure.
- Do not expand paths silently.
- `micro`/`low` known fixes spend at most half their budget before the first edit.
- Implementation + migration/bootstrap + broad docs/release review is not one run.
- More files than the selected budget requires a split, not a larger completion claim.

```text
local edit / commit / pushed branch / open PR != Done
focused proof + delivered commit + exact target verification + synchronized evidence = Done candidate
```

## 3. Canonical workflow owners

| Area | Owner |
|---|---|
| Fast start/root proof | [`.ai/README.md`](.ai/README.md), `scripts/agent_run.py` |
| Rule ownership | [`.ai/SOURCE_OF_TRUTH.md`](.ai/SOURCE_OF_TRUTH.md) |
| Durable docs/manifest/registry | [`docs/DOCUMENTATION_SYSTEM.md`](docs/DOCUMENTATION_SYSTEM.md) |
| Time/read/search/edit limits | [`.ai/TOKEN_BUDGETS.md`](.ai/TOKEN_BUDGETS.md) |
| Validation | [`.ai/VALIDATION_SELECTOR.md`](.ai/VALIDATION_SELECTOR.md) |
| Formal prompt contract/admission | [`.ai/PROMPT_LINT_CHECKLIST.md`](.ai/PROMPT_LINT_CHECKLIST.md) |
| Command safety | [`docs/AGENT_COMMAND_PLAYBOOK.md`](docs/AGENT_COMMAND_PLAYBOOK.md) |
| Queue selection/states | [`docs/prompt_queues/README.md`](docs/prompt_queues/README.md), [`PROMPT_LIFECYCLE.md`](docs/prompt_queues/PROMPT_LIFECYCLE.md) |
| Compact evidence | [`.ai/RUN_LOG_TEMPLATE.md`](.ai/RUN_LOG_TEMPLATE.md), [`docs/AGENT_RUN_LOG_ENFORCEMENT.md`](docs/AGENT_RUN_LOG_ENFORCEMENT.md) |
| Mistake routing/details | [`MISTAKE_INDEX.json`](docs/ai/learning/MISTAKE_INDEX.json), [`MISTAKE_LEDGER.md`](docs/ai/learning/MISTAKE_LEDGER.md) |

Do not copy full mechanics into prompts. Formal v2/v3 construction applies only when creating or materially changing an active queue owner, not every ad-hoc task.

## 4. Documentation and repository integrity

- [`docs/DOCS_MANIFEST.json`](docs/DOCS_MANIFEST.json) registers durable documents and their source mappings.
- [`docs/DOCS_REGISTRY.md`](docs/DOCS_REGISTRY.md) is generated; never hand-edit it.
- Update the durable owner in the same PR when current behavior, contract, validation or workflow changes.
- Dated audits, prompts, queues and run logs are evidence/history, not architecture authority.
- Unresolved `<<<<<<<`, `=======` or `>>>>>>>` markers in durable docs are blocking corruption; recover intended content from current owners and Git history.
- Record exactly one documentation-impact decision: updated paths, specific no-impact reason, or named follow-up.

## 5. Repository role and architecture

This repo is the ASP.NET Core backend/API for the MathLearning Flutter app. It owns auth, profiles, progress, quiz/SRS/practice, Daily Run, economy, cosmetics, leaderboard, sync, health/logging/maintenance, EF Core persistence/migrations, authoritative settlement/idempotency and backend contract tests.

- `src/MathLearning.Api/Program.cs`: startup/middleware/endpoint mapping.
- `src/MathLearning.Api/Endpoints/`: HTTP ownership.
- `src/MathLearning.Application/` and `src/MathLearning.Infrastructure/`: services/business behavior.
- `src/MathLearning.Domain/`: entities/invariants.
- `src/MathLearning.Infrastructure/Persistance/`: DbContext/migrations.
- `tests/MathLearning.Tests/`: tests.

Endpoints authenticate, bind/validate, call the owner and project contract-shaped results. Do not make endpoints a second business-logic owner.

## 6. Auth and user scope

- Mobile writes use authenticated server user identity.
- Never trust request-body `userId` as authority.
- Route `userId` equals the authenticated user unless explicitly admin-only.
- Admin routes separate actor and target.
- Execute anonymous, correct-user, wrong-user and wrong-role proof when ownership changes.
- Test metadata alone is not runtime authorization proof.

## 7. Idempotency and settlement

Retryable mutations use stable operation identity, normally:

```text
userId + operationType + operationId/idempotencyKey
```

Existing strategies—shared `IdempotencyLedger`, `economy_transactions`, `cosmetics_idempotency_ledger`, Daily Run chest domain-table policy—remain authoritative.

Prove first, duplicate, conflict, failure/cancellation, cross-user isolation and transaction/provider behavior. Never add generic retry before exact replay and atomicity are proven.

## 8. Endpoint/mobile contract and cross-repo ownership

- Synchronize route changes with [`docs/API_ENDPOINT_INVENTORY.md`](docs/API_ENDPOINT_INVENTORY.md).
- Verify current Flutter payloads/prompts; do not invent them from memory.
- Contract evidence records both repository main SHAs, owning prompt/path and touched/deferred docs.
- Prefer an existing backend runtime owner. A Flutter blocker does not justify a duplicate backend prompt.
- When backend support is absent, create one precise backend handoff with explicit dependencies and link the Flutter owner to it.
- Prefer canonical routes; do not expand legacy aliases without compatibility ownership.

## 9. Database and migration safety

- Inspect current mappings/migrations/snapshot first.
- Unique indexes match exact auth/idempotency scope.
- Do not assume production auto-migrates.
- Provider-sensitive behavior requires PostgreSQL proof.
- Never weaken schema-from-zero to obtain green CI.
- Split runtime behavior, migration/bootstrap and operator/release review when they exceed one bounded owner.
- Destructive commands require explicit non-production target proof.

## 10. Jobs, cache and observability

- Claim/lease, retry/backoff, dead-letter and cancellation have one owner.
- Process-local state declares bounds, eviction and multi-replica semantics.
- Read endpoints do not hide expensive writes/rebuilds.
- Cache invalidation follows authoritative writes.
- Never log bodies, credentials, tokens or sensitive user data.

## 11. Validation

Use [`.ai/VALIDATION_SELECTOR.md`](.ai/VALIDATION_SELECTOR.md):

```text
reproducer/current contract
→ focused behavior and counterexample
→ provider/build/static proof required by the risk
→ changed docs/prompt/evidence checks
→ wider suite/CI only for a named wider risk
```

For docs/agent/evidence changes:

```powershell
python scripts/check_documentation_health.py --full-links
python scripts/validate_agent_evidence.py --changed-from <base-sha> --verify-git
python scripts/analyze_agent_runs.py --changed-from <base-sha> --fail-on-regression
python scripts/validate_agent_system.py
```

Do not let historical evidence debt block the current change. `Database Validation` skips docs/agent-tooling-only diffs and runs the full PostgreSQL suite for runtime/test/schema/build changes.

## 12. Compact evidence and learning

Every non-trivial task uses compact `Evidence format: v2`, preferably generated by `scripts/agent_run.py`.

Record numeric counts, exact validation, changed paths, outcome, exceptions/learning and delivery. Target 35–70 lines. Do not enumerate every inspected file or keep empty narrative sections.

`Commit SHA: self` is canonical and resolved by `validate_agent_evidence.py --verify-git`. Never create a second commit only to backfill the SHA.

Queue Done rows stay compact:

```text
Done <n>% — Run log: <path>; Validation: <result>; Residual risk: <one sentence>; Commit: self|<sha>
```

Details belong in the run log, not the queue table.

## 13. Delivery/final response

Use a bounded branch/PR for broad tooling/workflow/migration/high-risk runtime changes unless direct main is explicitly requested/permitted.

Final response includes run log, mistakes, branch/PR/commit/merge SHA, files changed, validation executed/skipped, exact CI/main verification, completion state and residual owner. Do not claim Done/100% with failed proof, missing delivery or material residual work.
