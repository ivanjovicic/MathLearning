# Backend Agent Mistake Ledger

Status: active agent-learning memory  
Last aligned: 2026-07-02  
Scope: `ivanjovicic/MathLearning`

## Purpose

This ledger tracks repeated agent mistakes on the **backend** repo.

`.ai/runs/` logs explain one run. This ledger captures patterns so future agents do not rediscover them from scattered queue rows, BE-PERF audits, and commits.

A run is not learning-complete until every observed mistake is classified as:

```text
new mistake with a mistake card
repeated mistake with a rule/prompt/test/queue update
false alarm with explanation
```

## How agents must use this file

Before starting a non-trivial prompt:

1. Read this ledger.
2. Pick only mistake IDs relevant to the current prompt.
3. Write them in the run log under `Relevant prior mistakes read`.
4. Explain how the run will avoid repeating them.

Before marking a prompt Done:

1. Add a new mistake card if a new mistake was found.
2. Update an existing card if a known mistake repeated.
3. Add or update a rule, prompt, test, queue row, or validation prompt for repeated mistakes.
4. If no update is needed, write the reason in the run log.

## Severity

| Severity | Meaning | Required action |
|---|---|---|
| P0 | Data loss, false authoritative settlement, broken idempotency, unsafe mobile contract | rule + test update required immediately |
| P1 | Wrong Done status, missing evidence, misleading audit, wasted context | rule/prompt/queue update required |
| P2 | Local inefficiency, stale doc reference, minor template gap | prompt/template update or documented no-op |

## Status values

```text
Open | Mitigated | Watching | Retired | False alarm
```

---

## Known mistakes

### BACKEND-MISTAKE-EVIDENCE-001 — Runtime commit without `.ai/runs` evidence

Severity: P1  
Status: Open  
First seen: BE-PERF-001…008 performance lane (commits without per-prompt run logs)  
Repeated in: commits `12167aa`, `0f6ccd3`, `deb3c28`, `851d961` (backfilled 2026-07-01)

Problem:
Agents committed runtime changes under `src/**` or `tests/**` and marked queue rows `Done` using commit SHA and test notes only, without `.ai/runs/<prompt-id>-evidence.md`.

Impact:
Future agents cannot see validation commands, model/client, mistakes, or phase timing. Queue rows look complete while evidence is incomplete.

Root cause:
Backend repo lacked the Flutter-style run-log gate until `BACKEND-EVIDENCE-BOOTSTRAP-001`.

Prevention:
- `docs/AGENT_RUN_LOG_ENFORCEMENT.md` hard gate.
- `.ai/RUN_LOG_TEMPLATE.md` and `.ai/runs/README.md`.
- `docs/ai/prompts/BACKEND_EVIDENCE_BACKFILL_PROMPT.md` for repair.

Next check:
Run `RUN_LOG_EVIDENCE_LINT_PROMPT` on Done rows for BE-PERF-005…008 and BACKEND-CRIT-* before new runtime work. BE-PERF-001…004 backfilled 2026-07-01.

---

### BACKEND-MISTAKE-AUDIT-001 — Docs-only audit treated like runtime fix

Severity: P1  
Status: Mitigated  
First seen: BE-PERF-006/007/008 docs-only queue prompts  
Repeated in: performance review final responses; BE-PERF-004 queue `docs-only` label for runtime commit `851d961` (fixed 2026-07-01 backfill)

Problem:
Agents completed documentation audits (cold-start budget, request budgets, route compatibility, critical/second-pass app flow audits) and described outcomes as if runtime code was optimized, or left queue `Notes` ambiguous.

Impact:
Stakeholders assume latency/idempotency/route/security behavior changed when only docs or prompts were created. Mobile/backend planning diverges from reality.

Root cause:
Audit prompts and final response templates did not separate `docs/audit` from `implementation`; queue rows used `Ready` without status model.

Prevention:
- `docs/AGENT_RUN_LOG_ENFORCEMENT.md` docs-only vs runtime table.
- `docs/prompt_queues/backend_critical_risk_prevention.md` and `backend_second_pass_risk_prevention.md` status model (audit-created / prompt-ready / runtime-fixed / validated).
- Audit docs banner: static audit only — not fix proof (`BACKEND-AUDIT-STATUS-SYNC-2026-07-01-001`).
- Run logs must set `Run mode: docs-only` or `docs/audit`.

Next check:
No `BACKEND-CRIT-*` or `BACKEND2-CRIT-*` row marked Done without runtime/test commit and `.ai/runs` evidence.

---

### BACKEND-MISTAKE-VALIDATION-001 — Audit says no dotnet test but queue still advances

Severity: P1  
Status: Open  
First seen: BE-PERF-006/007/008 validation blocks (`git diff --check` only)  
Repeated in: queue marked Done without recording validation skip in run log

Problem:
Docs-only prompts correctly omit `dotnet test`, but agents still wrote high-confidence Done rows without explicit `Validation not run: docs-only per prompt` or score cap ≤85%.

Impact:
Later agents assume tests ran. Runtime regressions slip in adjacent prompts.

Root cause:
Queue validation sections and Done-row shape were not tied to evidence score caps.

Prevention:
- Evidence score cap in `docs/AGENT_RUN_LOG_ENFORCEMENT.md`.
- Run log must list `Validation run` **or** `Validation not run: <reason>`.
- Docs-only Done rows capped at 85% unless path verification is recorded.

Next check:
Lint prompt verifies Tests/Validation fields on every Done row.

---

### BACKEND-MISTAKE-XREPO-001 — Backend/mobile contract risk not synced to Flutter repo

Severity: P0  
Status: Open  
First seen: `backend_contract_gap_report.md` updates without mobile matrix sync  
Repeated in: endpoint/idempotency work closed in backend only

Problem:
Backend agents updated contract evidence, routes, or idempotency behavior in `ivanjovicic/MathLearning` without noting whether `ivanjovicic/Mathlearning-Mobile-App` docs (`mobile_api_contract.md`, `mobile_backend_contract_status.md`) were updated or explicitly deferred.

Impact:
Mobile app continues against stale contract assumptions. Release coordination fails across repos.

Root cause:
Cross-repo docs live in a separate private repo; backend prompts did not require a sync decision in run logs.

Prevention:
- Run logs for contract-touching work must include:
  ```text
  Cross-repo sync: updated | deferred <reason> | not applicable
  Mobile docs touched: <paths or none>
  ```
- Follow-up prompt when deferred.
- `docs/backend_contract_gap_report.md` links mobile evidence sources.

Next check:
Any P0 mutation or route audit prompt must record cross-repo sync in its run log before Done.

---

### BACKEND-MISTAKE-AUTH-001 — Refresh token generator and EF model length drift

Severity: P0/P1  
Status: Open  
First seen: `BACKEND-TEST-CORE-001` on 2026-07-02  
Evidence: `RefreshTokenService.GenerateRefreshToken()` creates Base64 from 64 random bytes (88 characters); migration `20260210114958_IncreaseRefreshTokenLength` changed the column to 128; current `ApiDbContext` and model snapshot declare max length 64.

Problem:
The runtime token generator, migration history, EF model, and model snapshot disagree about the maximum refresh-token length.

Impact:
- schema-from-zero or a future migration can regress the column to 64;
- relational persistence can reject generated 88-character tokens;
- InMemory auth tests may pass while PostgreSQL fails;
- refresh/login flows can become unavailable.

Root cause:
An inline EF configuration retained `HasMaxLength(64)` after the migration increased the column to 128, and no model-metadata regression test locked the intended length.

Prevention:
- `BACKEND-TEST-012` in `docs/prompt_queues/backend_test_coverage.md`.
- Align EF configuration and model snapshot to 128.
- Add a model metadata test proving generated token length fits the configured maximum.
- Run schema-from-zero validation and refresh-token tests.
- Treat migration/model snapshot drift as a blocking auth regression.

Next check:
Do not mark `BACKEND-TEST-012` Done until model, snapshot, migration history, generated token length, PostgreSQL schema validation, and a regression test agree.

---

## Add new mistake card

Use `docs/ai/learning/MISTAKE_CARD_TEMPLATE.md` and IDs:

```text
BACKEND-MISTAKE-<AREA>-<NNN>
```

Areas: `EVIDENCE`, `AUDIT`, `VALIDATION`, `XREPO`, `IDEM`, `MIGRATION`, `AUTH`, `PERF`, `QUEUE`
