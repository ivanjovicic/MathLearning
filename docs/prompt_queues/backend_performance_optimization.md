# Backend Performance / Optimization Prompt Queue

Last aligned: 2026-06-27  
Target repo: `ivanjovicic/MathLearning`  
Default lane: backend performance / contract-safe optimization

Use this queue for backend performance work that must preserve mobile API compatibility, authenticated user scope, idempotency, offline replay, XP/economy authority, and release evidence.

Read first:

- `../../AGENTS.md`
- `../DOCS_INDEX.md`
- `../AGENT_QUICKSTART.md`
- `../ARCHITECTURE_OVERVIEW.md`
- `../API_ENDPOINT_INVENTORY.md`
- `../BACKEND_CHANGE_CHECKLIST.md`
- `../COMMON_AGENT_PITFALLS.md`
- `../backend_contract_gap_report.md`
- `../mobile_contract_idempotency_handoff.md`
- `../BACKEND_PERFORMANCE_OPTIMIZATION_REVIEW_2026_06_27.md`

Hard rules:

- Keep mobile response contracts stable unless the prompt explicitly owns contract docs and tests.
- Keep user identity from auth context, not request body.
- Keep idempotency and duplicate replay behavior stable on mutation paths.
- Keep XP, rewards, cosmetics, and economy settlement backend-authoritative.
- Prefer targeted tests, query-shape proof, or honest expected-effect notes over vague performance claims.
- Do not broadly cache private/user-specific payloads without an explicit privacy/cache policy.

---

## Active prompts

| ID | Status | Can run in parallel with | Purpose |
|---|---|---|---|
| BE-PERF-001 | Ready | docs-only | Quiz start/questions hot-path query budget and test evidence. |
| BE-PERF-002 | Ready | docs-only | Daily SRS query budget, padding behavior, and duplicate-fetch protection. |
| BE-PERF-003 | Ready | docs-only | Answer submit/offline replay transaction audit. |
| BE-PERF-004 | Ready | docs-only | DB-backed leaderboard rank optimization after projection patch. |
| BE-PERF-005 | Ready | docs-only | Redis startup/fallback resilience and bounded connect/retry policy. |
| BE-PERF-006 | Ready | docs-only | Cold-start/background-service budget and health evidence. |
| BE-PERF-007 | Ready | docs-only | Explicit backend performance budgets and observability evidence. |
| BE-PERF-008 | Ready | docs-only | Route compatibility/deprecation and endpoint bloat audit. |

---

## BE-PERF-001 — Quiz start/questions hot-path budget

Task: optimize and prove `/api/quiz/start` and legacy `/api/quiz/questions` hot paths without changing mobile response shape.

Inspect:

- `src/MathLearning.Api/Endpoints/QuizEndpoints.cs`
- `src/MathLearning.Infrastructure/Persistance/ApiDbContext.cs`
- quiz/contract tests under `tests/MathLearning.Tests`

Owned paths:

- `src/MathLearning.Api/Endpoints/QuizEndpoints.cs`
- targeted quiz endpoint/contract tests
- this queue for status only

Required work:

1. Inspect query shape for question id selection and full question detail loading.
2. Keep no-tracking question reads and deterministic selected-id ordering.
3. Confirm random selection stays bounded and avoids full-table materialization.
4. Add or run a targeted contract test proving `quizId`, options, correct answer id, and question text still match the mobile contract.

Validation:

```bash
git diff --check
dotnet format --verify-no-changes
dotnet test tests/MathLearning.Tests/MathLearning.Tests.csproj --filter "Quiz|Contract"
```

---

## BE-PERF-002 — Daily SRS query budget

Task: audit and optimize `/api/quiz/srs/daily` and `/api/quiz/srs/mixed` for mobile Daily Review.

Inspect:

- `src/MathLearning.Api/Endpoints/SrsEndpoints.cs`
- `src/MathLearning.Infrastructure/Persistance/ApiDbContext.cs`
- SRS/idempotency/contract tests

Required work:

1. Verify `QuestionStats` due query uses the right `UserId + NextReview` index.
2. Review due-question and random-padding behavior.
3. Keep response shape stable.
4. Add or run tests for due-only, due+random, no-due, and limit behavior.

Validation:

```bash
git diff --check
dotnet format --verify-no-changes
dotnet test tests/MathLearning.Tests/MathLearning.Tests.csproj --filter "Srs|Daily"
```

---

## BE-PERF-003 — Answer submit/offline replay transaction audit

Task: audit `/api/quiz/answer`, `/api/quiz/offline-submit`, and `/api/quiz/batch-submit` for transaction cost and retry behavior.

Inspect:

- `src/MathLearning.Api/Endpoints/QuizEndpoints.cs`
- `src/MathLearning.Api/Endpoints/QuizEndpointHelpers.cs`
- `src/MathLearning.Api/Endpoints/ApiDbTransactionHelpers.cs`
- idempotency services/tests
- mobile mutation contract tests

Required work:

1. Map the current transaction timeline.
2. Classify each DB call as required in transaction vs candidate for later work.
3. Preserve duplicate answer replay, first-correct XP, audit rows, and ingest rows.
4. Add or run tests for replay, conflict, duplicate offline answer, and first-correct uniqueness.

Validation:

```bash
git diff --check
dotnet format --verify-no-changes
dotnet test tests/MathLearning.Tests/MathLearning.Tests.csproj --filter "Idempotency|MobileMutation|QuizAnswer"
```

---

## BE-PERF-004 — DB-backed leaderboard rank optimization

Task: optimize rank and near-rival lookup so Redis fallback does not load all matching user ids into memory.

Inspect:

- `src/MathLearning.Infrastructure/Services/Leaderboard/DbBackedRedisLeaderboardService.cs`
- `src/MathLearning.Api/Endpoints/LeaderboardEndpoints.cs`
- leaderboard indexes in `ApiDbContext`

Required work:

1. Replace full ordered-id materialization with DB-side rank/count logic where safe.
2. Preserve day/week/month/all-time period behavior and user id tie-breaker.
3. Add or run DB fallback leaderboard tests for rank and near-rivals.

Validation:

```bash
git diff --check
dotnet format --verify-no-changes
dotnet test tests/MathLearning.Tests/MathLearning.Tests.csproj --filter "Leaderboard"
```

---

## BE-PERF-005 — Redis startup/fallback resilience

Task: make Redis optional startup behavior bounded and explicit.

Inspect:

- `src/MathLearning.Api/Startup/ServiceRegistrationExtensions.cs`
- Redis leaderboard service
- DB-backed leaderboard fallback

Required work:

1. Add explicit Redis connect/retry/timeout policy from config defaults.
2. Keep missing Redis fallback to DB-backed leaderboard.
3. Add startup/config test if feasible, or document manual smoke steps.
4. Document expected config keys.

Validation:

```bash
git diff --check
dotnet format --verify-no-changes
dotnet test tests/MathLearning.Tests/MathLearning.Tests.csproj --filter "Startup|Redis|Leaderboard"
```

---

## BE-PERF-006 — Cold-start/background-service budget

Task: audit startup and hosted services so cold-start remains predictable.

Inspect:

- `src/MathLearning.Api/Program.cs`
- `src/MathLearning.Api/Startup/ServiceRegistrationExtensions.cs`
- hosted services
- deployment docs

Required work:

1. Map startup steps and mark blocking vs background.
2. Define budgets for schema check, seeders, design token init, Hangfire probe, hosted-service start.
3. Identify safe candidates to move after readiness.
4. Add docs and small instrumentation only if safe.

Validation:

```bash
git diff --check
dotnet format --verify-no-changes
```

---

## BE-PERF-007 — Explicit performance budgets and observability evidence

Task: turn existing tracing/logging into explicit budgets for mobile-critical flows.

Inspect:

- `src/MathLearning.Infrastructure/Services/Performance/PerformanceDbCommandInterceptor.cs`
- `src/MathLearning.Api/Middleware/RequestPerformanceLoggingMiddleware.cs`
- `SERILOG_LOGGING_GUIDE.md`

Required work:

1. Define budgets for quiz start, SRS daily, answer submit, offline batch, leaderboard, progress overview, adaptive path, auth profile, and startup.
2. Map each budget to current log/trace source.
3. Add local/staging smoke steps for collecting evidence.

Validation:

```bash
git diff --check
```

---

## BE-PERF-008 — Route compatibility and endpoint bloat audit

Task: classify canonical routes, legacy aliases, admin-only routes, and experimental/future routes.

Inspect:

- `src/MathLearning.Api/Program.cs`
- `src/MathLearning.Api/Endpoints/*.cs`
- `docs/API_ENDPOINT_INVENTORY.md`
- `docs/backend_contract_gap_report.md`

Required work:

1. Mark route owner file, auth behavior, test evidence, mobile caller, and deprecation plan.
2. Identify duplicate routes that may trigger duplicate backend work.
3. Add follow-up prompts only; do not remove routes in this audit.

Validation:

```bash
git diff --check
```

---

## Final response format for BE-PERF prompts

```text
Changed:
Hot path addressed:
Expected effect:
Contract/idempotency risk:
Validation actually run:
Validation not run:
Tests added/updated:
Residual risk:
Next prompt:
```
