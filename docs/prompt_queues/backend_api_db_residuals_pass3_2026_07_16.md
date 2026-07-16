# Backend API / Database Residual Queue — 2026-07-16 Pass 3

Source audit: [`../BACKEND_API_DB_RESIDUAL_AUDIT_2026_07_16_PASS3.md`](../BACKEND_API_DB_RESIDUAL_AUDIT_2026_07_16_PASS3.md)  
Target repo: `ivanjovicic/MathLearning`  
Reviewed runtime baseline: `0d0a1965b88f20855987c865fcd4038c856cdfa8`  
Scope: MathLearning backend/API only; exclude `src/MathLearning.Admin/**`

> These are implementation prompts, not proof of runtime fixes. Re-read current `main`, active queues, remote claims and open PRs before claiming an ID.

## Shared rules

- Read `AGENTS.md`, `docs/DOCS_INDEX.md`, `docs/BUGFIX_PATTERN_GUARDRAILS.md`, the source audit and the selected detail prompt.
- Do not edit or use the Blazor Admin project as implementation evidence.
- Current code and executable tests override prompt prose.
- Keep authenticated actor identity server-derived; do not trust request-body user IDs.
- Preserve existing canonical owners. Extend linked test/performance rows instead of creating duplicate infrastructure.
- Add the smallest regression test that fails before the fix.
- PostgreSQL, distributed cache/object storage and deployment claims require provider-specific evidence.
- Contract-touching changes require an explicit Flutter/mobile sync decision.
- Every non-trivial run creates `.ai/runs/<date>-<prompt-id>-evidence.md`.
- No Done from source inspection, queued CI or a branch-only commit.

## Active prompts

| ID | Priority | Status | Prompt | Purpose |
|---|---:|---|---|---|
| `BACKEND-API-DB-016` | P0/P1 privacy/storage | Prompt-ready | [Open](backend_api_db_pass3/BACKEND-API-DB-016.md) | Make bug-report screenshots private, reporter/admin-authorized, durable and lifecycle-safe. |
| `BACKEND-API-DB-017` | P0/P1 account security | Prompt-ready | [Open](backend_api_db_pass3/BACKEND-API-DB-017.md) | Add lockout-aware credential validation, auth-specific abuse controls, enumeration-safe responses and explicit account verification. |
| `BACKEND-API-DB-018` | P1 session security | Ready after `BACKEND-API-DB-017` | [Open](backend_api_db_pass3/BACKEND-API-DB-018.md) | Make logout-all, account-state and privilege changes invalidate existing access tokens within a documented bound. |
| `BACKEND-API-DB-019` | P1 catalog/readiness | Prompt-ready | [Open](backend_api_db_pass3/BACKEND-API-DB-019.md) | Replace silent startup catalog mutation with versioned, auditable and readiness-enforced cosmetics catalog ownership. |

## Canonical ownership and deduplication

| New prompt | Existing owner retained | Boundary |
|---|---|---|
| `016` | `BACKEND-TEST-025`, `BACKEND-API-DB-014`, `BACKEND-CRIT-004` | `016` owns private screenshot read/storage/runtime lifecycle; 025 keeps input/compensation tests; avatar policy remains separate. |
| `017` | `BE-PERF-011`, `BACKEND2-CRIT-001`, `BACKEND-API-DB-013`, `BACKEND-API-DB-007` | `017` owns credential/account verification policy; generic limiter storage, proxy trust, provisioning atomicity and refresh-token storage remain separate. |
| `018` | `BACKEND-API-DB-007`, `BACKEND-TEST-015` | `018` owns access-token/session invalidation; refresh-token hashing/rotation remains with existing owners. |
| `019` | `BACKEND-MIGRATION-001`, `BACKEND-API-DB-009`, `BACKEND-API-DB-015` | `019` owns catalog data deployment/version/readiness only; schema FK, entitlement and pending-operation recovery stay separate. |

## Execution order

1. `BACKEND-API-DB-016` and `BACKEND-API-DB-017` are the highest-risk new rows and may run in parallel only if their paths/tests are disjoint.
2. Run `BACKEND-API-DB-018` after `017` is main-verified because both change auth token issuance/validation tests and service registration.
3. `BACKEND-API-DB-019` may run in parallel with auth/screenshot work after migration and cosmetics-owner collision checks.
4. Existing P0 mutation/data-loss owners still outrank these rows when current main shows they remain unimplemented.

## Collision rules

- `016` must not absorb general bug-report field validation already specified by BACKEND-TEST-025 or photo-avatar behavior owned by 014.
- `017` must not create a second generic rate-limit store; integrate with the BE-PERF-011 contract.
- `018` must not store every JWT in a new unbounded table or add a database query to every request without a measured design.
- `019` must not overwrite operator/product catalog changes at startup or hide missing required catalog data behind a warning.
- Re-read migrations and model snapshots before adding schema.
- Update `docs/API_ENDPOINT_INVENTORY.md` and mobile contract docs when HTTP behavior changes.

## Completion gate

Each row remains Prompt-ready/Needs validation until:

- focused endpoint/service tests pass;
- required PostgreSQL/cache/storage/deployment proof runs;
- release build passes;
- queue, audit, endpoint inventory, run evidence and mobile sync decision agree;
- exact delivered main SHA is recorded.

Audit evidence: `.ai/runs/2026-07-16-BACKEND-API-DB-AUDIT-003-evidence.md`