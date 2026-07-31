# Backend Code Analysis Playbook

Last verified: 2026-07-31
Owner: `backend-quality`

This is the free, repeatable analysis process for the MathLearning ASP.NET Core backend. It finds compiler/analyzer issues, formatting drift, vulnerable dependencies, weak test boundaries and high-risk patterns. It does not claim that a static finding is a runtime bug until a focused regression test or provider-backed proof exists.

## Tool set

Use tools already available in the repository or in the .NET SDK:

| Tool | Cost | What it proves | Main limitation |
|---|---|---|---|
| `dotnet build` with Roslyn analyzers | Free, built in | Compile errors, nullable issues and configured analyzer warnings | It does not execute behavior |
| `dotnet format --verify-no-changes` | Free, built in | Formatting and analyzer/code-style drift | It is not a security or correctness proof |
| `dotnet test` plus XPlat coverage | Free, already in CI | Executed behavior, regressions and coverage evidence | Coverage does not prove important branches are correct |
| `dotnet list package --vulnerable --include-transitive` | Free, SDK command | Known vulnerable NuGet dependencies from advisory data | Requires network/advisory availability and triage |
| `rg` targeted searches | Free, already available | Fast risk-pattern inventory for auth, idempotency, EF, writes and logging | A match is only a review lead |
| PostgreSQL workflow | Free GitHub Actions/local Docker | Provider constraints, transactions, migrations and readiness | It is slower and must not be skipped for runtime/schema changes |

SonarQube/SonarCloud, commercial scanners and paid subscriptions are optional. They are not part of the backend standard and must not replace the repository's focused proof.

## Analysis tiers

Run the smallest tier that answers the current question.

### Tier 1 - every implementation change

```powershell
dotnet restore MathLearning.slnx
dotnet build MathLearning.slnx -c Release --no-restore -p:RunAnalyzers=true -p:EnforceCodeStyleInBuild=true
dotnet format MathLearning.slnx --verify-no-changes --severity warn --no-restore
```

For a clean baseline or a release candidate, repeat the build with `-warnaserror` after existing warnings have been classified:

```powershell
dotnet build MathLearning.slnx -c Release --no-restore -p:RunAnalyzers=true -p:EnforceCodeStyleInBuild=true -warnaserror
```

### Tier 2 - weekly or before a risky merge

```powershell
dotnet list MathLearning.slnx package --vulnerable --include-transitive
rg -n "FromSql|ExecuteSql|SaveChanges|SaveChangesAsync|\.Result|\.Wait\(|Task\.Run|lock \(|SemaphoreSlim|ConcurrentDictionary|Dictionary<|userId|operationId|idempotency|Authorize|AllowAnonymous|catch \(" src tests
```

Review every hit in its owning service/endpoint and nearest test. The search is deliberately a lead generator, not an automatic bug report.

### Tier 3 - release or high-risk persistence/contract work

```powershell
dotnet test tests/MathLearning.Tests/MathLearning.Tests.csproj -c Release --logger "trx;LogFileName=mathlearning-tests.trx" --results-directory artifacts/test-results --collect:"XPlat Code Coverage" --settings tests/MathLearning.Tests/coverage.runsettings
```

Then run the PostgreSQL-backed `Database Validation` workflow or its local equivalent. Inspect the TRX, Cobertura/ReportGenerator summary, schema-from-zero result, idempotent migration artifact and readiness smoke result. Do not mark provider-sensitive work validated from SQLite or InMemory alone.

## Risk-focused review rotation

The periodic audit prompt uses one focus area per cycle. Rotate in this order so a single run remains bounded:

1. Auth and ownership: anonymous/wrong-role access, actor versus target, request-supplied user IDs, token/session invalidation.
2. Idempotency and settlement: first request, exact replay, changed-payload conflict, rollback/cancellation, concurrent users and durable ownership.
3. EF Core and startup: mappings versus migrations/snapshot, unique indexes, delete behavior, cold start, retries and readiness.
4. Performance and concurrency: read paths that write, N+1 queries, unbounded keyed state, cancellation, timeouts, retries and multi-replica behavior.
5. Contract and observability: route/payload drift, safe errors, sensitive logging, pagination/cursor semantics and mobile handoff.

## Finding to prompt workflow

Classify each candidate before creating work:

| Classification | Meaning | Required next step |
|---|---|---|
| `tooling-baseline` | Analyzer/format/dependency output with no confirmed behavior gap | Record the exact output and create a bounded cleanup prompt only if it is actionable |
| `static-risk` | Plausible correctness, security or performance risk found in source | Create a prompt with source evidence and a falsifier/regression test |
| `runtime-confirmed` | Focused test/provider run reproduces the bad behavior | Prioritize a fix prompt and include the failing proof |
| `covered` | Existing code and tests prove the relevant invariant | Do not create a duplicate prompt; record the existing owner/test |
| `deferred` | Requires production telemetry, another repository or unavailable provider | Record the named owner and exact unblock condition |

Priority guidance:

- P0: false authoritative state, cross-user access, duplicate reward/settlement, data loss or release-blocking schema failure.
- P1: likely user-visible correctness/security/performance regression, unsafe startup or missing critical counterexample.
- P2: maintainability, non-critical coverage or cleanup with no immediate correctness impact.

Every new prompt must have one owner, one lane, one bounded proof and a unique ID. Do not turn a list of analyzer hits into one "fix everything" task. Do not create a prompt when an existing canonical runtime owner already covers the behavior; add a test/evidence handoff instead.

Any routed runtime prompt must contain a test-first contract: the smallest pre-change test expected to fail, the post-change command expected to pass and a counterexample. This audit prompt itself is docs/evidence-only and uses the explicit exception in its own contract.

## Cadence and ownership

- Pull request/runtime change: Tier 1 and the normal changed-path CI classification.
- Weekly scheduled or manually dispatched workflow: Tier 1 plus Tier 2 output artifact.
- Every two weeks: run `BACKEND-ANALYSIS-001` for one rotation focus and route at most three new findings into `docs/prompt_queues/`.
- Release candidate or migration/auth/idempotency change: Tier 3 and exact GitHub Actions evidence for the target SHA.
- Quarterly: review the analyzer baseline, recurring mistake cards, prompt duplication and whether a new rule can be made executable.

## Evidence and stop rules

Each analysis cycle creates `.ai/runs/<date>-<prompt-id>-evidence.md` with:

- exact commands and exit codes;
- files/searches inspected and candidate count;
- classification and priority for every routed finding;
- existing owner or new prompt path;
- `Mistakes observed`, residual risk and follow-up owner;
- `No GitHub Actions evidence found via connector` when no connector evidence exists.

Stop and split the run when the focus needs a second subsystem, more than three new prompts, more than six implementation paths or runtime edits. A static audit is never a runtime fix and cannot be marked validated without the required executable proof.
