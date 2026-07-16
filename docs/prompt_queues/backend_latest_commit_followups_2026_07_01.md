# Backend Latest Commit Follow-up Queue — 2026-07-01

Target repo: `ivanjovicic/MathLearning`  
Lane: latest-commit evidence and auth hardening follow-ups  
Created from review of latest commits on 2026-07-01.

## Why this queue exists

Recent backend commits made real runtime progress on high-risk auth flows:

- `79ea851` — refresh-token rotation concurrency safety.
- `b073350` — mobile registration atomicity/compensating cleanup.
- `b70b3b1`, `29d7250`, `cb2bed6` — queue/evidence rows for BACKEND2-CRIT-002/003.
- `f2f3fec`, `8914486`, `941c26b` — mechanical evidence validator and manual workflow.

The runtime fixes are valuable, but the latest evidence logs are too short for the new shared standard and the new validator/workflow have not been executed yet.

## Active prompts

| ID | Status | Purpose |
|---|---|---|
| BACKEND-LATEST-EVIDENCE-001 | Done (2026-07-16, docs/evidence) | Run/fix backend evidence validator and backfill latest auth run logs to the new standard. Model: unknown-not-exposed; Run log: `.ai/runs/2026-07-16-BACKEND-LATEST-EVIDENCE-001-evidence.md`; Mistakes: none; Waste: evidence backfill; Missed: none; Follow-up: none; Residual risk: referenced validation still fails on older legacy queue/log rows outside this prompt. |
| BACKEND-LATEST-AUTH-001 | Prompt-ready after BACKEND-LATEST-EVIDENCE-001 | Verify refresh-token and mobile-registration fixes with relational/provider-aware tests or document the remaining provider gap. |
| BACKEND-LATEST-WORKFLOW-001 | Prompt-ready after BACKEND-LATEST-EVIDENCE-001 | Run the manual Agent Evidence Validation workflow in referenced mode and record the result. |

---

## BACKEND-LATEST-EVIDENCE-001 — Validator run and auth evidence backfill

Run mode: docs/evidence repair  
Token budget: low/medium

### Goal

Make the latest backend evidence compatible with `docs/AGENT_SHARED_OPERATING_STANDARD.md` and `scripts/validate_agent_evidence.py` before more runtime risk prompts are marked Done.

### Read first

- `AGENTS.md`
- `docs/AGENT_SHARED_OPERATING_STANDARD.md`
- `docs/AGENT_RUN_LOG_ENFORCEMENT.md`
- `.ai/RUN_LOG_TEMPLATE.md`
- `docs/ai/learning/MISTAKE_LEDGER.md`
- `scripts/validate_agent_evidence.py`
- `docs/prompt_queues/backend_second_pass_risk_prevention.md`

### Inspect only

- `.ai/runs/2026-07-01-BACKEND2-CRIT-002-evidence.md`
- `.ai/runs/2026-07-01-BACKEND2-CRIT-003-evidence.md`
- `docs/prompt_queues/backend_second_pass_risk_prevention.md` rows for BACKEND2-CRIT-002 and BACKEND2-CRIT-003
- `scripts/validate_agent_evidence.py`

### Required work

1. Run:

```bash
python scripts/validate_agent_evidence.py --referenced-run-logs-only
```

2. If the script fails because BACKEND2-CRIT-002/003 logs are missing fields, backfill them using `.ai/RUN_LOG_TEMPLATE.md`.
3. Preserve existing test evidence:
   - `AuthRefreshConcurrencyTests|AuthRefreshEndpointRegressionTests`
   - `AuthMobileRegistrationAtomicityTests`
4. Do not invent model name, elapsed time, phase timing, or CI status.
5. Use placeholders:
   - `unknown-not-exposed`
   - `unknown-not-recorded`
   - `not run - <reason>`
6. Update queue rows only if validator output proves a row is incomplete or stale.
7. Add/update a run log for this prompt.

### Owned paths

- `.ai/runs/2026-07-01-BACKEND2-CRIT-002-evidence.md`
- `.ai/runs/2026-07-01-BACKEND2-CRIT-003-evidence.md`
- `.ai/runs/<yyyy-mm-dd>-BACKEND-LATEST-EVIDENCE-001-evidence.md`
- `docs/prompt_queues/backend_second_pass_risk_prevention.md` rows only
- `docs/prompt_queues/backend_latest_commit_followups_2026_07_01.md` status row only

### Avoid paths

- `src/**`
- `tests/**`
- broad queue rewrites
- changing the validator behavior unless syntax/runtime failure proves it is broken

### Validation

```bash
python scripts/validate_agent_evidence.py --referenced-run-logs-only
python scripts/validate_agent_evidence.py
```

Use the second command only after referenced mode is clean or the findings are intentionally documented as legacy warnings.

### Final response

Include validator result, repaired logs, unchanged runtime scope, commit SHA, residual risk.

---

## BACKEND-LATEST-AUTH-001 — Relational-provider auth fix verification

Run mode: investigation/test  
Token budget: medium

### Goal

Confirm that the latest auth fixes are not only InMemory-test-safe but also safe under the provider behavior used by production or a relational test provider.

### Read first

- `AGENTS.md`
- `docs/BACKEND_SECOND_PASS_RISK_PREVENTION_RULES.md`
- `docs/prompt_queues/backend_second_pass_risk_prevention.md`
- `src/MathLearning.Api/Endpoints/AuthEndpoints.cs`
- `src/MathLearning.Infrastructure/Persistance/ApiDbContext.cs`
- auth tests added by `79ea851` and `b073350`

### Required checks

1. Inspect whether `AuthRefreshConcurrencyTests` prove EF concurrency behavior with a relational provider or only with InMemory.
2. Inspect whether `AuthMobileRegistrationAtomicityTests` exercise transaction rollback and compensating cleanup paths separately.
3. Add one relational/provider-aware test only if the gap is real and feasible in current test infrastructure.
4. If relational testing is not feasible in scope, add a precise follow-up note and do not claim full provider proof.
5. Keep client-facing errors generic and do not log token values.

### Owned paths

- targeted auth tests only
- `docs/prompt_queues/backend_latest_commit_followups_2026_07_01.md` status row
- `.ai/runs/<yyyy-mm-dd>-BACKEND-LATEST-AUTH-001-evidence.md`

### Avoid paths

- broad auth endpoint rewrite
- schema/migration changes unless a failing relational test proves they are required
- changing mobile contract shape

### Validation

```bash
dotnet test tests/MathLearning.Tests/MathLearning.Tests.csproj --filter "AuthRefreshConcurrencyTests|AuthRefreshEndpointRegressionTests|AuthMobileRegistrationAtomicityTests"
```

Run any additional relational/provider-specific test command if added.

---

## BACKEND-LATEST-WORKFLOW-001 — Manual evidence workflow smoke

Run mode: validation-only  
Token budget: low

### Goal

Verify the newly added `.github/workflows/agent-evidence-validation.yml` can run in GitHub Actions without waiting for a future prompt to discover workflow breakage.

### Required work

1. Trigger the manual workflow in `referenced` mode from GitHub UI or CLI.
2. Record run URL/status in `.ai/runs/<yyyy-mm-dd>-BACKEND-LATEST-WORKFLOW-001-evidence.md`.
3. If it fails, classify failure:
   - validator syntax/runtime failure;
   - missing evidence fields;
   - missing run-log file;
   - legacy warning promoted to failure;
   - workflow configuration issue.
4. Add a focused repair prompt only if the failure cannot be fixed safely in this prompt.

### Validation

Manual workflow: `Agent Evidence Validation`, mode `referenced`.

### Stop rule

Do not switch the workflow to automatic push/PR blocking until referenced mode passes and legacy all-mode findings are triaged.
