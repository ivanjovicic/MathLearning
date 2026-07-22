# Backend Cross-Repo / Current-Main Residual Queue — 2026-07-17

Backend baseline: `76693a1dc64872993dd02816b161943ed52ecb36`  
Flutter baseline reviewed: `0d01e940b325fcf5d58e4a1bba058f4bc3d752bb`  
Scope: proven uncovered backend behavior and exact disposition work only.

## Active prompts

| ID | Priority | Status | Prompt | Purpose |
|---|---:|---|---|---|
| `BACKEND-XREPO-ADAPTIVE-START-001` | P0 correctness | Done | [Open](backend_cross_repo/BACKEND-XREPO-ADAPTIVE-START-001.md) | Made adaptive session start replay-safe across timeout/restart using the existing idempotency ledger. Done 79% - Run log: `.ai/runs/2026-07-22-BACKEND-XREPO-ADAPTIVE-START-001-evidence.md`; Validation: `dotnet test`, `python scripts/run_guarded.py --timeout-seconds 180 -- dotnet test tests/MathLearning.Tests/MathLearning.Tests.csproj --filter FullyQualifiedName~AdaptiveSessionStartIdempotency`, `dotnet build src/MathLearning.Api/MathLearning.Api.csproj -c Release --no-restore`, and `python scripts/check_documentation_health.py --context src/MathLearning.Api/Endpoints/AdaptiveEndpoints.cs` passed; Residual risk: SQLite concurrency proof is narrower than the prompt's PostgreSQL ideal; Commit: `self|0ff1b051891ceeaa01606783e60db75e57c0747b`. |
| `BACKEND-API-DB-020` | P0/P1 privacy | Done | [Open](backend_cross_repo/BACKEND-API-DB-020.md) | Replace persisted/public screenshot URL semantics with opaque private storage keys and an authorized streaming route. Done 100% - Run log: `.ai/runs/2026-07-22-BACKEND-API-DB-020-evidence.md`; Validation: `dotnet build`, `dotnet test` and `dotnet ef migrations has-pending-model-changes` passed; Residual risk: `021` still owns durable provider/deployment migration; Commit: `self|f5f6300`. |
| `BACKEND-API-DB-021` | P1 durability | Blocked | [Open](backend_cross_repo/BACKEND-API-DB-021.md) | Durable private screenshot provider/deployment migration is blocked on named platform/infrastructure owner authority to choose and provision the shared provider. Run log: `.ai/runs/2026-07-22-BACKEND-API-DB-021-evidence.md`. |
| `BACKEND-PR-DISPOSITION-001` | P1 queue integrity | Ready | [Open](backend_cross_repo/BACKEND-PR-DISPOSITION-001.md) | Review stale draft PR #3 against current main and retain only unique still-needed tests. |

## Existing owners — do not duplicate

| Current problem/dependency | Canonical backend owner | Cross-repo action |
|---|---|---|
| Adaptive answer timeout, duplicate, conflict, cancellation and PostgreSQL race | `BE-PERF-012` in `backend_performance_followups_2026_07_03.md` | Flutter `CRIT16-FLOW-006` must cite and wait for this owner; refine the row/evidence rather than allocate another implementation ID. |
| Practice answer/completion exactly-once | `BE-PERF-015` | Keep separate from adaptive session start. |
| Auth/rate limiter multi-replica semantics left by `BACKEND-API-DB-017` | `BE-PERF-011` | No second limiter implementation/store. |
| Local latest-wins Adaptive provider publication | Flutter `CRIT26-ADAPTIVE-SESSION-001` | Backend prompts must not absorb mobile provider lifecycle. |

## Selection order

1. `BACKEND-XREPO-ADAPTIVE-START-001` may run in parallel with `BACKEND-API-DB-020` because paths are disjoint.
2. `BACKEND-API-DB-021` starts only after the private storage/read contract from `020` is main-verified.
3. `BACKEND-PR-DISPOSITION-001` is review-only and must not edit runtime in the same run.
4. `BE-PERF-012` and `BE-PERF-015` outrank any attempt to invent duplicate adaptive mutation prompts.

## Completion rule

No row is Done from documentation alone. Runtime prompts require focused behavior and counterexample tests, provider proof where named, synchronized contract docs, compact evidence and exact delivered-main verification. Review disposition requires a concrete PR close/supersede/retain decision with a named next owner.
