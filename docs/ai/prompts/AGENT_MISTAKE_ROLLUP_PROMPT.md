# Backend Agent Mistake Rollup Prompt

Use when there are at least 5 meaningful `.ai/runs` logs since the last rollup, or the same mistake/waste category appears 3 times.

```text
Use only this repository:
ivanjovicic/MathLearning

Prompt ID:
BACKEND-MISTAKE-ROLLUP-001

Run mode:
docs/evidence learning rollup

Token budget:
low

Goal:
Turn recent backend agent mistakes into durable learning so future agents do not repeat them.

Read first:
- docs/ai/learning/MISTAKE_LEDGER.md
- docs/ai/learning/MISTAKE_CARD_TEMPLATE.md
- docs/AGENT_RUN_LOG_ENFORCEMENT.md
- .ai/runs/README.md
- .ai/RUN_LOG_TEMPLATE.md

Relevant prior mistakes read:
- (list BACKEND-MISTAKE-* IDs likely to appear in recent logs)

How this run avoids prior mistakes:
- Rollup only; one small prevention change; no broad refactors

Inspect only:
- last 5–8 meaningful `.ai/runs/*.md` since previous rollup;
- latest `docs/ai/learning/*rollup*.md`, if any;
- queue rows referenced by those logs when incomplete;
- docs/rules named by repeated mistakes only.

Do not inspect:
- src/** refactors
- full solution builds unless a log names a specific test gap

Required checks:
1. Which mistakes repeated?
2. Which BACKEND-MISTAKE-* IDs were involved?
3. Which waste category cost the most context?
4. Which rule should have prevented the repeat?
5. Did the prior run read relevant prior mistakes?
6. Did the prior run update rule/prompt/test/queue?
7. Which one change most reduces next-run waste?

Required work:
1. Create `docs/ai/learning/<yyyy-mm-dd>-backend-agent-mistake-rollup.md`.
2. Update `docs/ai/learning/MISTAKE_LEDGER.md` (status, Repeated in, Next check).
3. Make exactly one small prevention change (rule, queue row, or `docs/ai/prompts/*`).
4. Create `.ai/runs/<date>-BACKEND-MISTAKE-ROLLUP-001-evidence.md`.

Output rollup shape:

```text
Reviewed logs:
Relevant mistake IDs:
Repeated mistakes:
Most expensive mistake:
Root cause:
Rule/prompt/test/queue updated:
What the next agent should avoid:
Next check:
Residual risk:
```

Validation:
- git diff --check
- verify referenced mistake IDs exist in MISTAKE_LEDGER.md
- verify new prompt paths exist

Mistakes observed: (fill at end)

Final response:
- logs reviewed
- mistake IDs updated
- rule/prompt/queue changed
- validation
- residual risk
- commit SHA
```
