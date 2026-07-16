# Backend Agent Mistake Ledger

Last aligned: 2026-07-17  
Status: detailed learning owner

Normal tasks use [`MISTAKE_INDEX.json`](MISTAKE_INDEX.json) to select only relevant IDs. Open this full ledger only when updating a card, investigating a repeated pattern or changing the index.

## Quick inventory

| ID | Status | Guard |
|---|---|---|
| `BACKEND-MISTAKE-EVIDENCE-001` | Open | Compact run log and changed-only lint before Done |
| `BACKEND-MISTAKE-AUDIT-001` | Mitigated | Docs/audit is never runtime proof |
| `BACKEND-MISTAKE-VALIDATION-001` | Open | Exact executed proof or honest non-Done state |
| `BACKEND-MISTAKE-XREPO-001` | Open | Contract work records Flutter sync/defer owner |
| `BACKEND-MISTAKE-AUTH-001` | Open | Token generator/model/snapshot/DB length agree |
| `BACKEND-MISTAKE-AUTH-002` | Watching | Privileged routes use exact policy and denial tests |
| `BACKEND-MISTAKE-IDEM-001` | Open | Durable ingest handoff shares settlement authority |
| `BACKEND-MISTAKE-VALIDATION-002` | Watching | Test auth explicitly proves anonymous/role cases |
| `BACKEND-MISTAKE-CONTENT-001` | Needs validation | Preserve existing inline LaTeX byte-for-byte |
| `BACKEND-MISTAKE-CONTENT-002` | Needs validation | Sanitize quoted/unquoted events and unsafe URLs |
| `BACKEND-MISTAKE-PERF-001` | Watching | GET maintenance paths perform zero rebuild writes |
| `BACKEND-MISTAKE-QUEUE-001` | Watching | One prompt ID/owner; refresh row before publish |
| `BACKEND-MISTAKE-IDEM-002` | Open | No generic mutation retry without replay/atomicity |
| `BACKEND-MISTAKE-PERF-002` | Open | Read endpoints do not settle/refresh/write |
| `BACKEND-MISTAKE-PERF-003` | Open | Process-local keyed state/queues are bounded |
| `BACKEND-MISTAKE-PROCESS-001` | Mitigated | No full-ledger/manual-long-log default |
| `BACKEND-MISTAKE-PROCESS-002` | Mitigated | `Commit SHA: self`; no SHA-backfill commit |
| `BACKEND-MISTAKE-SCOPE-001` | Open | One lane/owner; oversized mixed tasks split |
| `BACKEND-MISTAKE-CI-001` | Mitigated | Docs/agent-only diffs skip full DB suite |

## Detailed cards

### BACKEND-MISTAKE-EVIDENCE-001 — Runtime work without durable evidence

Severity: P1. Runtime/tests advanced without a target log, forcing reconstruction. Prevention: compact v2 evidence, changed-only validator and queue link.

### BACKEND-MISTAKE-AUDIT-001 — Docs-only audit treated as runtime fix

Severity: P1. Static findings were described as changed behavior. Prevention: separate `audit`, `runtime-fixed` and `validated` states.

### BACKEND-MISTAKE-VALIDATION-001 — Queue advances without executable validation

Severity: P1. Static/build/test-existence language implied success. Prevention: exact command/result or honest `Needs validation`/`Blocked` state.

### BACKEND-MISTAKE-XREPO-001 — Backend/mobile contract not synchronized

Severity: P0. API/idempotency/economy/auth changes omitted Flutter impact. Prevention: compact cross-repo fields and named defer owner.

### BACKEND-MISTAKE-AUTH-001 — Refresh-token generator/model/schema length drift

Severity: P0/P1. Generated token, EF metadata/snapshot and PostgreSQL column length diverged. Prevention: metadata, migration history and provider-backed persistence agree.

### BACKEND-MISTAKE-AUTH-002 — Privileged routes use generic authentication

Severity: P1. Admin/maintenance routes used generic authorization. Prevention: exact policy, learner denial and route-metadata assertions.

### BACKEND-MISTAKE-IDEM-001 — Commit before non-durable analytics ingest

Severity: P0. Authoritative answer/XP commit could precede lossy analytics delivery. Prevention: same-transaction durable handoff plus replay/restart/two-consumer proof.

### BACKEND-MISTAKE-VALIDATION-002 — Test auth silently authenticates anonymous tests

Severity: P1. No-header tests ran as `test-user`. Prevention: explicit anonymous marker, separate 401/403 tests and direct handler tests.

### BACKEND-MISTAKE-CONTENT-001 — Regex normalization removes inline LaTeX

Severity: P1. Matched `$...$` content was discarded. Prevention: copy existing math byte-for-byte and normalize plain segments only.

### BACKEND-MISTAKE-CONTENT-002 — Sanitizer misses unquoted events/unsafe URLs

Severity: P1. Unquoted event attributes and dangerous schemes survived. Prevention: adversarial sanitizer and authoring-pipeline tests.

### BACKEND-MISTAKE-PERF-001 — GET maintenance route mutates database

Severity: P1. A nominal read invoked `REINDEX`/`ANALYZE`. Prevention: read/write service separation and zero-rebuild GET contract tests.

### BACKEND-MISTAKE-QUEUE-001 — Same prompt ID assigned to different work

Severity: P1. Parallel stale queue snapshots reused an ID. Prevention: refresh the owning row immediately before publication and never reuse IDs.

### BACKEND-MISTAKE-IDEM-002 — Generic retry wraps non-idempotent multi-save mutation

Severity: P0. Retry could duplicate/diverge history, mastery or SRS effects. Prevention: stable operation identity, payload hash, atomic settlement and PostgreSQL concurrency proof.

### BACKEND-MISTAKE-PERF-002 — Read endpoints perform refresh/settlement/snapshot writes

Severity: P1. Polling caused write amplification and coupled rewards to reads. Prevention: pure read contracts, explicit stale metadata and durable background owners.

### BACKEND-MISTAKE-PERF-003 — Unbounded keyed state/queue

Severity: P1. Process-local dictionaries/channels had no cardinality/capacity/restart contract. Prevention: bounds, TTL/eviction, saturation, deduplication and multi-replica semantics.

### BACKEND-MISTAKE-PROCESS-001 — Full ledger and long manual log on every task

Severity: P2. Agents repeatedly read hundreds of lines and filled many empty sections before/after small tasks. Evidence: old template required 15 narrative sections; many logs record unknown timing. Prevention: `MISTAKE_INDEX.json`, `scripts/agent_run.py`, compact v2 logs and 90-line warning.

### BACKEND-MISTAKE-PROCESS-002 — Self-referential commit SHA creates cleanup commits

Severity: P2. Logs/queue rows required a hash that did not exist until after commit, producing “record/backfill commit SHA” commits and post-rebase fixes. Prevention: `Commit SHA: self`, resolved by Git history with `--verify-git`.

### BACKEND-MISTAKE-SCOPE-001 — Mixed-lane oversized task

Severity: P1. A medium run could combine implementation, migration/bootstrap, readiness, broad docs and 20+ changed files. This increases hypotheses, retries and review/CI time. Prevention: one lane/owner, hard changed-file limits and automatic split before editing.

### BACKEND-MISTAKE-CI-001 — Full database suite runs for docs/agent-only changes

Severity: P2/P1. Agent-system PRs started PostgreSQL, restore/build/schema/full tests and surfaced unrelated legacy failures. Prevention: changed-path classifier, conditional database suite and always-present final `validate-database` gate.

## Add/update a card

Use IDs `BACKEND-MISTAKE-<AREA>-<NNN>`. Update this ledger first, then `MISTAKE_INDEX.json` if normal routing should select the card. Repeated mistakes must name `prevention=<rule/test/tool change>` in the run log.
