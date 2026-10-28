# USER-20261028-NUGET-RESTORE-PATH1 Evidence

Evidence format: v2
Prompt ID: USER-20261028-NUGET-RESTORE-PATH1
Queue: user-assigned
Agent/tool: unknown-not-exposed
Model provider: unknown-not-exposed
Model name/id: unknown-not-exposed
Client/IDE: unknown-not-exposed
Run mode: investigation
Token budget: low
Started at UTC: 2026-10-28T21:23:15Z
Completed at UTC: 2026-10-28T21:34:28Z
Elapsed time: 11m 13s
Relevant prior mistakes read: BACKEND-MISTAKE-EVIDENCE-001, BACKEND-MISTAKE-VALIDATION-001, BACKEND-MISTAKE-CI-001, BACKEND-MISTAKE-PROCESS-001
How this run avoids prior mistakes: apply BACKEND-MISTAKE-EVIDENCE-001; apply BACKEND-MISTAKE-VALIDATION-001; apply BACKEND-MISTAKE-CI-001; apply BACKEND-MISTAKE-PROCESS-001
Owner/hypothesis: A null NuGet/SDK path initialization in the local restore stack causes the repeatable path1 exception; falsifier is line 782 resolving to a repo-owned path property or a command-specific missing environment variable.
Files inspected: 14
Files changed: 2
Searches: 8
Validation runs: 5
Failed retries: 2

## Outcome
- Investigation partially unblocked the local restore path: SDK 8.0.422 restores successfully and focused Hangfire unit tests plus Release API build pass. The original Hangfire/Neon owner remains Needs validation because local PostgreSQL provider tests could not authenticate and no live deployment probe was run.

## Changed paths
- .ai/runs/2026-10-28-USER-20261028-NUGET-RESTORE-PATH1-evidence.md
- docs/prompt_queues/backend_registration_ops_residuals_2026_09_09.md

## Validation
Validation run: SDK 8.0.422 dotnet restore MathLearning.Tests.csproj: pass; run_guarded HangfireOutboxIsolationTests: 3/3 pass; run_guarded Release API build: pass, 0 warnings/errors; docs health: 28 documents, 0 failures; agent-system validation: 0 failures; git diff --check: pass.
Validation not run: Broad Hangfire filter hit run_guarded wall timeout at 187.203s; Outbox provider filter: 4 failed, 1 passed because local PostgreSQL rejected default postgres password (28P01) before temporary DB creation; live Neon/Fly starvation probe not run; CI for this evidence commit not yet started.

## Exceptions and learning
Mistakes observed: BACKEND-MISTAKE-VALIDATION-001: Do not infer provider proof from compilation; keep both owners Needs validation. BACKEND-MISTAKE-PROCESS-001: broad Hangfire filter and search volume exceeded the low budget.
Waste: Broad Hangfire filter consumed 187s before timeout; Docker CLI server check hung because no Docker daemon responded and was interrupted.
Missed: A narrower Hangfire class filter completed quickly and isolated the passing unit tests; the first filter could have been narrower.
Follow-up: Run provider tests against an explicitly isolated PostgreSQL service with valid credentials; deployment operator must run the live Neon/Fly starvation probe before closing BACKEND-OPS-HANGFIRE-NEON-001.
Residual risk: PostgreSQL provider/concurrency behavior and deployed Neon/Fly starvation recovery remain unproven.
Documentation impact: Updated the canonical queue row to link this diagnostic evidence and preserve Needs validation; no runtime contract documentation changed.
Cross-repo impact: None.

## Delivery
State: Needs validation
Branch/PR: codex/nuget-restore-path1-diagnosis
Commit SHA: self
Completion %: 79
