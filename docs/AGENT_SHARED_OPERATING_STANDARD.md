# Shared Agent Operating Standard

Last aligned: 2026-07-01  
Scope: `ivanjovicic/MathLearning`  
Role: .NET backend/API for MathLearning

This document is the shared cross-repo standard for agents working across:

- `ivanjovicic/Mathlearning-Mobile-App` — Flutter/mobile app;
- `ivanjovicic/MathLearning` — .NET backend/API;
- `ivanjovicic/AgentsWatch` — local-first agent supervisor and prompt/token optimizer.

Repository-specific docs still apply. This standard defines the common minimum behavior.

---

## 1. Source-of-truth order

When sources disagree, use this order:

1. current code and tests in the current repo;
2. `AGENTS.md`;
3. this shared standard;
4. run-log enforcement, mistake ledger, and evidence templates;
5. prompt router / owning prompt queue;
6. architecture, contract, roadmap, audit, and planning docs.

Never use chat memory or old queue rows as stronger proof than current code/tests or committed run evidence.

---

## 2. One prompt, one mode, one owner

Every non-trivial prompt must declare one primary run mode:

```text
validation-only
investigation-only
implementation
tests
docs/evidence
review-only
diff-only review
```

If the work needs investigation, implementation, tests, docs, and review, split it into separate prompts.

Each prompt must include:

```text
Repository:
Prompt ID:
Queue:
Run mode:
Token budget:
Owned paths:
Avoid paths:
Validation:
Stop rules:
Expected evidence:
Relevant prior mistakes read:
```

---

## 3. Token economy and context budget

Use the smallest useful context.

| Budget | Docs before first action | Files inspected | Files edited | Use for |
|---|---:|---:|---:|---|
| Low | 3 | 8 | 3 | validation, one bug, docs/evidence, diff review |
| Medium | 5 | 15 | 6 | one feature slice, focused implementation, test pass |
| High | 8 | review only unless scoped | scoped only | audits, release evidence, architecture planning |

Stop and split if:

- the prompt becomes whole-repo analysis;
- more than one repo needs runtime edits;
- validation cannot be named;
- owned/avoid paths are unclear;
- evidence would need to be reconstructed from many old commits.

---

## 4. Evidence is mandatory

Every non-trivial prompt must produce or update:

```text
.ai/runs/<yyyy-mm-dd>-<prompt-id>-evidence.md
```

Use `.ai/RUN_LOG_TEMPLATE.md`. Do not invent a custom evidence shape.

Required fields include:

- model provider/name/client or `unknown-not-exposed`;
- elapsed/phase timing or `unknown-not-recorded`;
- files inspected and changed;
- validation run or explicit skipped reason;
- what was done and missed;
- waste categories;
- relevant prior mistakes read;
- mistakes observed;
- follow-up prompt;
- completion percent;
- residual risk;
- commit SHA.

A queue row cannot be high-confidence `Done` without a run-log path or explicit fallback.

---

## 5. Completion score caps

Completion score reflects evidence quality, not effort.

| Situation | Maximum score |
|---|---:|
| Target run log exists, is referenced, and validation is recorded | 95–100% |
| Useful audit completed, but target run logs still missing | 75% |
| Queue rows updated, but no durable `.ai/runs` evidence exists | 70% |
| Model/timing missing and not marked `unknown-*` | 65% |
| Validation/path verification not run for docs/evidence changes | 85% |
| Docs-only audit described as runtime fix | 70% |
| Mistake observed but not classified | 80% |
| Repeated mistake without prevention update | 75% |

Never claim 100% when residual risk says target evidence, validation, runtime fix, or cross-repo sync is missing.

---

## 6. Mistake-learning loop

A run is not learning-complete until every observed mistake is classified as:

```text
new mistake with a mistake card
repeated mistake with a rule/prompt/test/queue/lint update
false alarm with explanation
```

Before starting, read the repo's mistake ledger and record only relevant IDs in the run log.

Before finishing, update one of:

- mistake ledger;
- run-log enforcement rule;
- prompt template;
- queue row;
- validation/lint prompt;
- regression test;
- docs index / router.

If no update is needed, explain why in the run log.

---

## 7. Docs-only vs runtime truth

Docs-only audits, prompt queues, specs, and planning docs are not runtime fixes.

Use exact wording:

```text
docs-only audit
prompt-ready
runtime-fixed
test-validated
Needs evidence sync
Blocked
```

Do not mark a risk fixed until a runtime/test commit and evidence log prove it.

---

## 8. Cross-repo contract rule

If a change touches mobile/backend contracts, API payloads, idempotency, economy, cosmetics, auth, profile, leaderboard, or release evidence, the run log must include:

```text
Cross-repo impact: yes/no
Other repos checked:
Other repo docs touched:
Deferred sync reason:
Follow-up prompt:
```

Flutter-side contract changes must check backend docs. Backend-side contract changes must check Flutter docs. AgentsWatch product/dogfood changes must not leak product queues back into MathLearning repos.

---

## 9. Validation honesty

Do not claim validation passed unless the command was run or CI evidence was checked.

Use exact entries:

```text
Validation run: <command> — passed/failed
Validation not run: not run - <reason>
CI: No GitHub Actions evidence found via connector
```

For backend runtime changes, prefer targeted `dotnet test --filter ...` first. For docs-only work, `git diff --check` plus path verification is enough unless the prompt says otherwise.

---

## 10. Mechanical evidence validation

For any prompt that touches prompt queues, `.ai/runs`, `AGENTS.md`, `docs/DOCS_INDEX.md`, run-log enforcement, run-log templates, mistake ledgers, evidence docs, or cross-repo standard docs, run:

```text
python scripts/validate_agent_evidence.py
```

For a focused repair/backfill pass, the agent may use:

```text
python scripts/validate_agent_evidence.py --referenced-run-logs-only
```

Record the command and result in the run log. If the agent cannot run local commands because it is using only a connector or remote file API, write:

```text
Validation not run: not run - connector-only docs update, no local checkout
```

Do not replace this mechanical check with manual reading unless the skip reason is explicit.

---

## 11. Final response minimum

Final response must include:

```text
Run log:
Mistake IDs:
Files changed:
Validation:
Commit SHA:
Completion %:
Residual risk:
Next prompt:
```
