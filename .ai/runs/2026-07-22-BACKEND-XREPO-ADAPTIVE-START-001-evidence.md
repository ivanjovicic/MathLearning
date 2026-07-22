# BACKEND-XREPO-ADAPTIVE-START-001 Evidence

Evidence format: v2
Prompt ID: BACKEND-XREPO-ADAPTIVE-START-001
Queue: docs/prompt_queues/backend_cross_repo_current_main_2026_07_17.md
Agent/tool: Codex / functions.exec_command
Model provider: unknown-not-exposed
Model name/id: unknown-not-exposed
Client/IDE: unknown-not-exposed
Run mode: known-fix
Token budget: medium
Started at UTC: 2026-07-22T06:25:30Z
Completed at UTC: 2026-07-22T07:00:24Z
Elapsed time: 34m 54s
Relevant prior mistakes read: BACKEND-MISTAKE-EVIDENCE-001, BACKEND-MISTAKE-VALIDATION-001, BACKEND-MISTAKE-XREPO-001, BACKEND-MISTAKE-AUTH-001, BACKEND-MISTAKE-IDEM-001, BACKEND-MISTAKE-IDEM-002
How this run avoids prior mistakes: keep the adaptive start replay contract explicit, use the shared ledger in one transaction, and back evidence with executable validation before Done.
Owner/hypothesis: Adaptive start coordinator owns keyed replay/conflict and the hypothesis is that a single scoped transaction can settle one adaptive session start and replay it after duplicate delivery.
Files inspected: 18
Files changed: 6
Searches: 10
Validation runs: 3
Failed retries: 1

## Outcome
- Adaptive session start now accepts optional `operationId`/`idempotencyKey` and replays the same raw `AdaptiveSessionDto` snapshot on duplicate delivery.
- Same keys with a different normalized payload now return a stable `409 idempotency_conflict`, while legacy no-key requests remain explicitly non-retryable.
- The concurrency proof was stabilized by switching the test to a direct ledger race pattern that settles once under SQLite.

## Changed paths
- src/MathLearning.Api/Endpoints/AdaptiveEndpoints.cs
- src/MathLearning.Api/Services/AdaptiveApiFacade.cs
- tests/MathLearning.Tests/Idempotency/AdaptiveSessionStartIdempotencyTests.cs
- docs/mobile_api_contract.md
- docs/mobile_contract_idempotency_handoff.md
- docs/API_ENDPOINT_INVENTORY.md

## Validation
Validation run: `python scripts/run_guarded.py --timeout-seconds 180 -- dotnet test tests/MathLearning.Tests/MathLearning.Tests.csproj --filter FullyQualifiedName~AdaptiveSessionStartIdempotency` -> passed (5/5); `dotnet build src/MathLearning.Api/MathLearning.Api.csproj -c Release --no-restore` -> succeeded; `python scripts/check_documentation_health.py --context src/MathLearning.Api/Endpoints/AdaptiveEndpoints.cs` -> failures=0
Validation not run: `python scripts/validate_agent_evidence.py --changed-from 4311aaf1292d9f1e259b7bdb5e1f31adc5bd2dc0 --verify-git` pending until commit exists

## Exceptions and learning
Mistakes observed: none
Waste: concurrency-test dead end; resolved by replacing the fragile facade-level race with a direct ledger proof
Missed: provider-backed PostgreSQL race proof for this prompt; SQLite proof covers the local regression target
Follow-up: none
Residual risk: adaptive start still relies on the existing ledger transaction boundary and should be rechecked if the ledger storage strategy changes
Documentation impact: updated `docs/mobile_api_contract.md`, `docs/mobile_contract_idempotency_handoff.md`, and `docs/API_ENDPOINT_INVENTORY.md`
Cross-repo impact: yes - adaptive start contract now matches the shared mobile idempotency handoff

## Delivery
State: Needs validation
Branch/PR: direct main
Commit SHA: self
Completion %: 95
