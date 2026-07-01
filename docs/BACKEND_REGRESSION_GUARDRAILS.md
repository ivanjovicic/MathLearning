# Backend Regression Guardrails

Last aligned: 2026-06-27  
Repo: `ivanjovicic/MathLearning`  
Status: mandatory read for backend code changes and prompt design

This document turns repeated bug-fix patterns from commit history into pre-edit and pre-commit guardrails. It is not a generic checklist. Each rule below maps to regressions that have already happened in this repository.

Read with:

- `AGENTS.md`
- `docs/DOCS_INDEX.md`
- `docs/AGENT_QUICKSTART.md`
- `docs/BACKEND_CHANGE_CHECKLIST.md`
- `docs/COMMON_AGENT_PITFALLS.md`
- `docs/backend_contract_gap_report.md`
- `docs/mobile_contract_idempotency_handoff.md`
- `docs/BACKEND_PERFORMANCE_OPTIMIZATION_REVIEW_2026_06_27.md`

---

## 1. Mandatory prompt block

Every backend implementation prompt must contain this block before the agent edits code:

```text
Backend regression guardrails:
Historical bug class protected:
Why this change can reintroduce it:
Files inspected:
Tests/validation planned:
Contract/schema/docs touched:
Residual risk if validation cannot run:
```

The historical bug class must be one of:

- `schema-migration-drift`
- `auth-user-scope`
- `idempotency-offline-replay`
- `mobile-contract-shape`
- `startup-deploy-config`
- `performance-query-shape`
- `utf8-localization`
- `compiler-warning-cleanliness`
- `admin-blazor-auth-ui`
- `question-authoring-integrity`

If a change touches more than one class, name all of them.

---

## 2. Evidence table from commit history

| Bug class | Repeated evidence | Guardrail |
|---|---|---|
| `schema-migration-drift` | Migration conflicts, no-op shims, schema guard, CI database validation, admin/API schema separation | Migration, snapshot, schema docs, and validation script must move together. |
| `auth-user-scope` | Route/user mismatch fixes, practice-session ownership guard, settings route correction, sync payload mismatch rejection | Mobile-facing mutations must write for authenticated user, not arbitrary request body user id. |
| `idempotency-offline-replay` | Offline batch replay hardening, economy/cosmetics ledgers, fresh transaction id test fix | Retry semantics must be proven with same keys/same payload and same keys/different payload. |
| `mobile-contract-shape` | Cosmetics response shape tests, snake_case body rejection, languageCode/language_code support | DTO shape changes require HTTP contract tests and API inventory updates. |
| `startup-deploy-config` | Hangfire disabled fallback, loopback DB guard, DataProtection persistence, Blazor login fixes | Startup must be bounded, explicit, and safe in containers. |
| `performance-query-shape` | Random selection replacement, leaderboard DB fallback projection, admin N+1 and search fixes | Hot paths need bounded SQL, projections, pagination, and no accidental full-table work. |
| `utf8-localization` | Mojibake fixes in Serbian monitoring/admin text and language-code compatibility | Any user-visible Serbian text must be UTF-8 and searched for mojibake before commit. |
| `compiler-warning-cleanliness` | CS1998 cleanups and fake async fixes | No warning-only cleanup should be deferred after commit. |
| `admin-blazor-auth-ui` | Interactive Blazor login moved to POST endpoint, admin startup migration guard fixes | Cookie auth and identity operations must follow Blazor/server lifecycle constraints. |
| `question-authoring-integrity` | Correct answer identity, MCQ validation, admin/API authoring split, preview/sanitization concerns | Question correctness must be stable by id/validated rules, not fragile UI-only state. |

---

## 3. Guardrails by bug class

### 3.1 `schema-migration-drift`

Applies when touching:

- `ApiDbContext`
- `AdminDbContext`
- migrations
- model snapshots
- schema guard
- deployment workflows
- startup migration behavior

Rules:

- Add entity/model change and migration in the same branch.
- Update `ApiDbContextModelSnapshot` or admin snapshot when EF expects it.
- Do not rely on implicit runtime repair SQL as the primary production migration strategy.
- Keep API and Admin migration streams separate unless the prompt explicitly owns both.
- For production/staging docs, keep database migration before binary deploy.
- If a migration must be idempotent or defensive, explain why in the migration comment or release doc.

Recommended validation:

```bash
dotnet ef migrations script --idempotent --project src/MathLearning.Infrastructure/MathLearning.Infrastructure.csproj --startup-project src/MathLearning.Api/MathLearning.Api.csproj --context ApiDbContext
./scripts/db/validate-schema.ps1
dotnet test tests/MathLearning.Tests/MathLearning.Tests.csproj --filter "Schema|Migration|Database"
```

---

### 3.2 `auth-user-scope`

Applies when touching mobile-facing mutations, user/profile/settings, sync, practice, progress, economy, cosmetics, quiz, SRS, or admin acting-on-user routes.

Rules:

- Authenticated user id is the default write authority.
- Request body `userId` is never trusted for normal mobile mutations.
- Route `{userId}` must match authenticated user unless the route is documented admin-only.
- Practice/session mutations must filter by authenticated user and session id together.
- Sync envelopes that contain a user id must reject mismatches before domain processing.
- Admin routes must clearly separate actor user and target user.

Recommended validation:

```bash
dotnet test tests/MathLearning.Tests/MathLearning.Tests.csproj --filter "MutationUserScope|UserSettings|PracticeSession|Sync"
```

---

### 3.3 `idempotency-offline-replay`

Applies when touching retryable mutations, offline queue endpoints, quiz answer, SRS update, economy, rewards, daily run chest, seasons, cosmetics, or sync.

Rules:

- Stable `operationId` and `idempotencyKey` must be kept across mobile retries.
- Same keys + equivalent payload must replay the settled response.
- Same keys + different payload must return conflict for generic ledger flows.
- Daily Run chest remains a documented domain-table exception; do not force generic conflict semantics there unless policy changes.
- Domain error tests must use fresh transaction/operation ids when the test intends to reach domain validation instead of idempotency short-circuiting.
- Client-supplied XP, coins, cosmetics, fragment progress, and reward state are not settlement authority.

Recommended validation:

```bash
dotnet test tests/MathLearning.Tests/MathLearning.Tests.csproj --filter "Idempotency|MobileMutation|MobileEconomy|MobileCosmetics|OfflineBatch"
```

---

### 3.4 `mobile-contract-shape`

Applies when changing endpoint paths, request DTOs, response DTOs, JSON names, compatibility aliases, or legacy/mobile adapters.

Rules:

- Canonical route, legacy alias, and unsupported old shape must be explicit.
- If a shape is intentionally supported in both camelCase and snake_case, add a contract test for both.
- If a legacy shape is intentionally rejected, add a negative contract test.
- Inventory/avatar/cosmetic responses must reflect server-authoritative persisted state after mutations.
- Update `docs/API_ENDPOINT_INVENTORY.md` and contract/gap docs when mobile-facing shape changes.

Recommended validation:

```bash
dotnet test tests/MathLearning.Tests/MathLearning.Tests.csproj --filter "Contract|MobileApiRoute|MobileMutation"
```

---

### 3.5 `startup-deploy-config`

Applies when touching `Program.cs`, service registration, connection strings, Hangfire, Redis, DataProtection, Identity, deployment workflows, or health checks.

Rules:

- Production must not silently use loopback/local database fallback.
- Slow external dependency probes need bounded timeout and clear fallback state.
- If Hangfire is disabled, enqueue calls must be safe and visible in logs.
- DataProtection keys must survive container restarts.
- Login/logout cookie mutation in interactive Blazor should go through server POST endpoints or another lifecycle-safe pattern.
- Re-read Identity entities after changes when concurrency stamps or password reset operations depend on fresh state.

Recommended validation:

```bash
dotnet test tests/MathLearning.Tests/MathLearning.Tests.csproj --filter "Startup|Health|Admin|Identity"
```

---

### 3.6 `performance-query-shape`

Applies when touching quiz start, SRS, leaderboard, progress, adaptive learning, admin list pages, search, filters, or frequently-called mobile reads.

Rules:

- Avoid `OrderBy(Guid.NewGuid())` on hot paths or large tables.
- Avoid `ToLower().Contains()` for database search; prefer provider-supported indexed search patterns.
- Prefer `AsNoTracking()` for read-only queries.
- Prefer projection DTOs over full entity materialization when only a few fields are needed.
- Cap page sizes and limits.
- Avoid N+1 count/query loops in admin grids and mobile list endpoints.
- Record the expected query budget for quiz start, SRS daily, leaderboard, and progress/adaptive reads (see [`BACKEND_REQUEST_PERFORMANCE_BUDGETS.md`](BACKEND_REQUEST_PERFORMANCE_BUDGETS.md)).

Recommended validation:

```bash
dotnet test tests/MathLearning.Tests/MathLearning.Tests.csproj --filter "Leaderboard|Quiz|Srs|Progress|Performance"
```

---

### 3.7 `utf8-localization`

Applies when changing user-visible Serbian text, admin UI copy, monitoring messages, localization, or language settings.

Rules:

- Save files as UTF-8.
- Search for mojibake markers before commit: `Ã`, `Å`, `Ä`, `â`, `�`.
- Keep JSON/property names separate from translated display copy.
- Treat `languageCode` and `language_code` compatibility as contract behavior, not incidental parsing.

Recommended validation:

```bash
git grep -n "Ã\|Å\|Ä\|â\|�" -- src docs tests
```

---

### 3.8 `compiler-warning-cleanliness`

Applies to any C# or Razor change.

Rules:

- Do not leave CS1998 fake async warnings.
- If a method has no `await`, make it synchronous or return `Task.FromResult` intentionally.
- Remove unused imports after moving code.
- Keep nullable warnings meaningful; do not hide with broad `!` unless proven safe.

Recommended validation:

```bash
dotnet build MathLearning.slnx -c Release
```

---

### 3.9 `admin-blazor-auth-ui`

Applies when touching Admin login/logout, Identity, MudBlazor pages, admin data grids, question editor, bug pages, categories, users, or deployment login flow.

Rules:

- Avoid interactive component lifecycle traps for cookie auth mutations.
- Keep returnUrl local-only and sanitized.
- Prefer MudBlazor components over raw HTML controls in admin forms unless there is a clear reason.
- For list pages, keep search debounce, server-side filtering, pagination, and no N+1 queries.
- For Identity updates, re-fetch user before password reset or operations that depend on concurrency stamp freshness.

Recommended validation:

```bash
dotnet test tests/MathLearning.Tests/MathLearning.Tests.csproj --filter "Admin|Identity|User"
```

---

### 3.10 `question-authoring-integrity`

Applies when touching question authoring, math preview, options, correct answer, steps, translations, validators, publish/draft flow, or admin question pages.

Rules:

- MCQ correctness must be tied to stable option identity where possible, not only option text.
- Server-side validators must enforce the same critical rules as Admin UI validators.
- Prevent duplicate options after trimming/case normalization where the product requires uniqueness.
- Keep step order and option order stable.
- Keep sanitization and preview rules consistent between Admin UI and API authoring paths.
- Add tests for create/edit/publish/save-draft and invalid hidden combinations.

Recommended validation:

```bash
dotnet test tests/MathLearning.Tests/MathLearning.Tests.csproj --filter "QuestionAuthoring|Question|Validation"
```

---

## 4. Pre-edit checklist

Before editing code, answer:

1. Which historical bug class does this change risk reintroducing?
2. Which route/entity/service owns the behavior?
3. Is the mobile API contract affected?
4. Is schema or migration state affected?
5. Is retry/idempotency/offline replay affected?
6. Is auth user scope affected?
7. Is query shape on a hot path affected?
8. Which exact test or validation proves the guardrail?

If the answer to any item is unclear, inspect code and tests before editing.

---

## 5. Final response requirement

Every backend implementation response must include:

```text
Changed:
Historical guardrail applied:
Bug class protected:
Validation actually run:
Validation not run:
Tests added/updated:
Contract/schema/docs updated:
Residual risk:
Commit:
```

Docs-only changes must still state that runtime tests were not run.

---

## 6. Stop-and-report rules

Stop and report instead of guessing when:

- a migration targets a table whose ownership is unclear between API and Admin contexts;
- a mobile contract shape conflicts with existing Flutter docs;
- a route can write for a user different from authenticated user and it is not admin-only;
- idempotency behavior differs from documented policy;
- a performance change changes answer/reward/economy correctness;
- a production startup guard would be weakened;
- validation cannot run and the touched path is P0 mutation, schema, auth, or offline replay.
