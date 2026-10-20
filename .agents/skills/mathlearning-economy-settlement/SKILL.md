---
name: mathlearning-economy-settlement
description: Change or review authenticated MathLearning coins, XP, hints, rewards, Daily Run, seasons, cosmetics and server idempotency/offline replay.
---
# Authoritative economy and idempotent settlement
1. Read `AGENTS.md` economy rules, `docs/mobile_contract_idempotency_handoff.md`, owning API/application/persistence and focused contract tests.
2. Trace the **authenticated** actor and single authoritative mutation. Never settle currency, XP, chest, season reward or cosmetic from request-body `userId`, client cache or timeout.
3. Preserve stable `userId + operationType + operationId/idempotencyKey` according to the relevant existing ledger or domain table. A retry, app restart or offline replay must keep the original operation identity.
4. Test first execution, identical replay, changed-payload conflict, concurrent duplicate, cancellation/rollback, restart and different user with same key. Verify ledger and domain update occur atomically in real PostgreSQL where required.
5. Successful mutation requires backend acknowledgement; null, parse failure, failed HTTP, timeout and network outage are NOT settlement. Flutter is transport/recovery only.
6. Check matching Flutter route, payload and retry contract at its current main SHA; record both SHAs. Do not duplicate ownership into a separate backend prompt if already covered.
7. Run focused test via guarded `.ai/VALIDATION_SELECTOR.md`, then only the broader provider tests demanded by risk. Log exact CI head, executed/skipped tests and residual uncertainty.
**Stop:** double-writer ledger, unclear business economy policy, destructive schema change or unsupported DB proof.
