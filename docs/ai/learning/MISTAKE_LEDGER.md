# Backend Agent Mistake Ledger

Status: active agent-learning memory  
Last aligned: 2026-07-03  
Scope: `ivanjovicic/MathLearning`

## Purpose and use

This ledger records repeated backend-agent mistakes so later work does not rediscover them from scattered commits and queues.

Before a non-trivial prompt:

1. Read this ledger.
2. Name relevant mistake IDs in the run log.
3. Explain the prevention used.

Before marking a prompt Done:

1. Classify each discovered problem as new, repeated or false alarm.
2. Add/update a card, rule, prompt, test or queue row.
3. Record executable validation or why it did not run.

Severity: P0 = data loss/idempotency/contract; P1 = security/evidence/material regression; P2 = local inefficiency or stale process.  
Status values: `Open | Mitigated | Watching | Retired | False alarm`.

---

## Known mistakes

### BACKEND-MISTAKE-EVIDENCE-001 — Runtime commit without `.ai/runs` evidence

Severity: P1  
Status: Open

Problem: runtime/tests were committed and queues advanced without per-prompt evidence.  
Impact: later agents cannot distinguish implemented, tested and merely documented work.  
Prevention: mandatory `.ai/runs/<prompt>-evidence.md`, evidence lint and explicit validation status.  
Next check: no Done row without run log and executable result or explicit validation failure.

---

### BACKEND-MISTAKE-AUDIT-001 — Docs-only audit treated like runtime fix

Severity: P1  
Status: Mitigated

Problem: audits and prompt creation were described as if runtime behavior changed.  
Prevention: use `docs/audit`, `prompt-ready`, `runtime-fixed` and `validated` as distinct statuses.  
Next check: static findings must never be cited as fix proof.

---

### BACKEND-MISTAKE-VALIDATION-001 — Queue advances without executable validation

Severity: P1  
Status: Open

Problem: tests/build were unavailable or omitted, but status language implied success.  
Prevention: every run lists validation executed or `Validation not run: <reason>`; no pass claim from static review.  
Next check: focused command and release build before moving implemented rows to Validated.

---

### BACKEND-MISTAKE-XREPO-001 — Backend/mobile contract risk not synced

Severity: P0  
Status: Open

Problem: backend routes/idempotency changed without updating or explicitly deferring Flutter contract docs.  
Prevention: every contract-touching log records `Cross-repo sync` and mobile paths.  
Next check: P0 mutation changes cannot close without a mobile sync decision.

---

### BACKEND-MISTAKE-AUTH-001 — Refresh-token generator/model/schema length drift

Severity: P0/P1  
Status: Open

Problem: generated token is 88 chars, migration widened DB column to 128, current EF model/snapshot still declare 64.  
Impact: InMemory tests may pass while PostgreSQL rejects tokens or future migration regresses schema.  
Prevention: BACKEND-TEST-012 aligns model/snapshot and adds metadata/relational persistence tests.  
Next check: generator, EF metadata, snapshot, migration history and PostgreSQL schema must agree.

---

### BACKEND-MISTAKE-AUTH-002 — Privileged routes protected by generic authentication

Severity: P1  
Status: Mitigated / Watching

Problem: bug-management and maintenance routes used generic `.RequireAuthorization()` despite being described as admin.  
Prevention: exact `UiTokensAdminPolicy`, learner-denial tests and route-metadata assertions.  
Next check: automated privileged-route inventory under BACKEND-TEST-047.

---

### BACKEND-MISTAKE-IDEM-001 — Authoritative commit before non-durable analytics ingest

Severity: P0  
Status: Open

Problem: answer/XP commits before quiz-attempt analytics ingest; post-commit failure can leave permanent analytics gaps while replay is deduplicated.  
Prevention: BACKEND-TEST-022 requires same-transaction durable handoff and idempotent consumer/recovery tests.  
Next check: fail-after-commit, restart, duplicate delivery and two-consumer race must pass.

---

### BACKEND-MISTAKE-VALIDATION-002 — Test auth silently authenticates no-header requests

Severity: P1  
Status: Mitigated / Watching

Problem: tests named anonymous could actually run as authenticated `test-user`.  
Prevention: `X-Test-Anonymous: true`, separate 401 and 403 tests, direct `TestAuthHandlerTests`.  
Next check: old authorization suites and future metadata audit.

---

### BACKEND-MISTAKE-CONTENT-001 — Regex normalization removed existing inline LaTeX

Severity: P1  
Status: Mitigated / Needs validation

Problem: `Regex.Split` discarded matched `$...$` expressions.  
Impact: authored math could disappear from API JSON.  
Prevention: copy existing inline math byte-for-byte; normalize only plain segments; helper and HTTP preservation tests.  
Next check: focused formatter/quiz/SRS/next-question tests.

---

### BACKEND-MISTAKE-CONTENT-002 — HTML sanitizer covered only quoted event attributes

Severity: P1  
Status: Mitigated / Needs validation

Problem: unquoted event attributes and dangerous `javascript:`/`data:` URLs survived sanitization.  
Prevention: quoted/unquoted event handling, unsafe URL rules and direct sanitizer tests.  
Next check: sanitizer plus authoring pipeline tests before accepting new HTML.

---

### BACKEND-MISTAKE-PERF-001 — GET maintenance route invoked mutating rebuild work

Severity: P1  
Status: Mitigated / Needs validation  
First seen: BACKEND-TEST-024

Problem: `GET /api/maintenance/index-stats` called `RebuildCorruptedIndexesAsync`, allowing a nominal read request to execute `REINDEX` and `ANALYZE`.

Impact:

- GET retries, crawlers or admin refreshes could trigger expensive database mutations;
- route semantics were misleading and hard to test safely;
- scheduled and manual maintenance used separately constructed services.

Root cause: query and mutation responsibilities were combined in one method and endpoints created concrete services directly.

Prevention:

- `IIndexMaintenanceService` separates read-only statistics from rebuild;
- GET contract test asserts zero rebuild calls;
- shared DI service and cancellation tokens;
- endpoint inventory explicitly marks mutating vs read-only paths.

Next check: PostgreSQL distributed lock/operator audit under BACKEND-TEST-042 and focused maintenance tests.

---

### BACKEND-MISTAKE-QUEUE-001 — Parallel agents assigned the same prompt ID to different work

Severity: P1  
Status: Mitigated / Watching  
First seen: BACKEND-TEST-AUDIT-003

Problem: second-pass follow-ups initially used BACKEND-TEST-036 while parallel work had already committed a different BACKEND-TEST-036 package.

Impact:

- evidence, commits and queue statuses become ambiguous;
- a future agent may run or close the wrong prompt;
- audit links can point to unrelated implementation.

Root cause: prompt IDs were allocated from a stale queue snapshot during parallel work.

Prevention:

- re-read the central queue immediately before publishing new prompt IDs;
- never reuse an existing ID even if the package is unrelated;
- second-pass residual prompts were moved to BACKEND-TEST-042…047;
- central queue preserves the parallel BACKEND-TEST-036 package.

Next check: automated queue/evidence lint should detect duplicate IDs across queue and `.ai/runs` files.

---

## Add new mistake card

Use `docs/ai/learning/MISTAKE_CARD_TEMPLATE.md` and IDs:

```text
BACKEND-MISTAKE-<AREA>-<NNN>
```

Areas: `EVIDENCE`, `AUDIT`, `VALIDATION`, `XREPO`, `IDEM`, `MIGRATION`, `AUTH`, `CONTENT`, `PERF`, `QUEUE`.
