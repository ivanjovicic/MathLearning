# Mobile Mutation Idempotency Requirements - 2026-07-01

## Scope

This note records the backend-side decision matrix for retryable mobile mutations after
`BACKEND-CRIT-005` evidence landed.

It is intentionally spec-first:

- no runtime code changes are made in this prompt;
- no legacy mobile compatibility is removed here;
- follow-up implementation prompts are required if the product later wants hard rejection.

## Decision summary

- `/api/quiz/answer` stays legacy-compatible for now.
- `/api/quiz/srs/update` stays legacy-compatible for now.
- `/api/quiz/batch-submit` stays a legacy compatibility adapter and does not gain per-item diagnostics in this prompt.
- Economy, cosmetics, and Daily Run routes remain idempotent through their existing route-specific identity rules.

Reason:

- current backend docs and tests still model legacy fallback behavior for quiz mutations;
- rejecting missing operation identity now would break documented compatibility before the mobile migration is complete;
- this prompt is a decision/spec pass, not the migration itself.

## P0 retryable mobile mutation matrix

| Route | Current backend behavior | Identity required today? | Notes |
|---|---|---:|---|
| `POST /api/quiz/answer` | Accepts legacy no-key mode; when one of `operationId` / `idempotencyKey` is supplied, both resolve to the same identity. | No | Keep legacy mode until the mobile rollout removes no-key callers. |
| `POST /api/quiz/srs/update` | Accepts legacy no-key mode; when one of `operationId` / `idempotencyKey` is supplied, both resolve to the same identity. | No | Same migration rule as quiz answers. |
| `POST /api/daily-run/chest/claim` | Uses domain-table Policy B with `transactionId` as the settlement anchor. | Yes, via `transactionId` | `idempotencyKey` is accepted but not used for dedupe. |
| `POST /api/economy/*` mobile settlement routes | Uses `operationId`, with existing route helpers falling back to `transactionId` or `idempotencyKey` where documented. | Not strictly enforced yet | Stable identity should still be sent by mobile callers. |
| `POST /api/cosmetics/*` mobile mutation routes | Uses `operationId` / `idempotencyKey` route-specific idempotency. | Not strictly enforced yet | Keep current fallback semantics until the mobile contract is migrated. |
| `POST /api/quiz/batch-submit` | Legacy compatibility adapter for offline replay. | No | Do not change to per-item diagnostics in this prompt. |

## Follow-up implementation prompts

If the product wants a stricter contract later, split it into two prompts:

1. Add a migration prompt that removes no-key callers from the mobile app for `quiz/answer` and `quiz/srs/update`, then hard-rejects missing operation identity in backend mobile-contract mode.
2. Add a separate migration prompt for `quiz/batch-submit` if product wants per-item diagnostics or deprecation of the legacy compatibility adapter.

## Evidence notes

- Cross-repo sync: deferred; mobile repo is not modified in this prompt.
- Mobile docs touched: none.
- CI evidence from connector: no GitHub Actions evidence found via connector.
- Validation for this prompt remains docs-only.

