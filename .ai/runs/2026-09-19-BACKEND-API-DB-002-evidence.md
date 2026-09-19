# BACKEND-API-DB-002 Evidence

Evidence format: v2
Prompt ID: BACKEND-API-DB-002
Queue: formal
Agent/tool: unknown-not-exposed
Model provider: unknown-not-exposed
Model name/id: unknown-not-exposed
Client/IDE: unknown-not-exposed
Run mode: known-fix
Token budget: high
Started at UTC: 2026-09-19T11:58:42Z
Completed at UTC: 2026-09-19T12:12:00Z
Elapsed time: approximately 14 minutes
Relevant prior mistakes read: BACKEND-MISTAKE-EVIDENCE-001, BACKEND-MISTAKE-VALIDATION-001, BACKEND-MISTAKE-AUTH-001, BACKEND-MISTAKE-AUTH-002, BACKEND-MISTAKE-VALIDATION-002, BACKEND-MISTAKE-IDEM-001, BACKEND-MISTAKE-IDEM-002, BACKEND-MISTAKE-XREPO-001
How this run avoids prior mistakes: apply BACKEND-MISTAKE-EVIDENCE-001; apply BACKEND-MISTAKE-VALIDATION-001; apply BACKEND-MISTAKE-AUTH-001; apply BACKEND-MISTAKE-AUTH-002; apply BACKEND-MISTAKE-VALIDATION-002; apply BACKEND-MISTAKE-IDEM-001; apply BACKEND-MISTAKE-IDEM-002; apply BACKEND-MISTAKE-XREPO-001
Owner/hypothesis: `POST /api/quiz/answer` must never synthesize a session; settlement must validate authenticated ownership, persisted issued-question membership, active lifetime and completion inside the authoritative transaction.
Files inspected: 15
Files changed: 8
Searches: 6
Validation runs: 6
Failed retries: 0

## Outcome
- Implemented stable `400 QUIZ_SESSION_ID_REQUIRED` for missing/malformed `quizId`/`sessionId` and removed replacement-session creation.
- Enforced stable `404 QUIZ_SESSION_NOT_FOUND` for unknown, foreign, non-issued, expired and completed sessions.
- Kept the existing persisted `IssuedQuestionIdsJson` manifest as the authoritative membership representation. Active lifetime is 24 hours; a session is completed after every issued question has one settled answer. Existing active sessions still permit repeated attempts before completion.
- Moved no-idempotency session/membership validation into the serializable transaction; keyed replay/conflict remains ledger-first and deterministic.

## Changed paths
- `src/MathLearning.Api/Endpoints/QuizEndpoints.cs`
- `tests/MathLearning.Tests/Idempotency/QuizAnswerIdempotencyTests.cs`
- `openapi.yaml`
- `docs/mobile_api_contract.md`
- `docs/API_ENDPOINT_INVENTORY.md`
- `docs/QUIZ_ANSWER_TRANSACTION_AUDIT.md`
- `docs/prompt_queues/backend_test_coverage.md`
- `.ai/runs/2026-09-19-BACKEND-API-DB-002-evidence.md`

## Validation
Validation run: focused idempotency + contract tests `22/22` pass; `dotnet build MathLearning.slnx -c Release --no-restore` pass, 0 errors; OpenAPI YAML parse pass; `python scripts/check_documentation_health.py --full-links` pass (`documents=25 failures=0`).
Validation run: required broad filter `38/40` pass; failures are pre-existing/unrelated `DurableQuizAttemptIngestEndpointTests` (`Email '' is invalid`) and `RelationalIdempotencyTransactionTests` economy lease ownership assertion.
Validation not run: PostgreSQL schema/concurrency matrix; `localhost:5433` is unreachable. SQLite/in-memory focused success is not treated as provider-sensitive completion.

## Exceptions and learning
Mistakes observed: none; applied BACKEND-MISTAKE-EVIDENCE-001, BACKEND-MISTAKE-VALIDATION-001, BACKEND-MISTAKE-AUTH-001, BACKEND-MISTAKE-AUTH-002, BACKEND-MISTAKE-VALIDATION-002, BACKEND-MISTAKE-IDEM-001, BACKEND-MISTAKE-IDEM-002 and BACKEND-MISTAKE-XREPO-001.
Waste: none material; one broad filter was run and retained as evidence despite unrelated failures.
Missed: PostgreSQL deterministic concurrency proof remains unexecuted because the local server is unavailable.
Follow-up: run the required PostgreSQL schema/concurrency matrix from BACKEND-TEST-032, including concurrent no-key/session settlement and rollback checks.
Residual risk: the derived 24-hour/completed policy requires PostgreSQL validation against deployed schema/provider behavior; offline-submit remains its existing compatibility path and was not redesigned.
Documentation impact: updated mobile contract, endpoint inventory, transaction audit, OpenAPI request schema and queue evidence; no schema migration was needed because issued-question membership already exists in the API model.
Cross-repo impact: none; canonical payload keeps `quizId`, with `sessionId` retained as a documented compatibility alias.

## Delivery
State: Needs validation
Branch/PR: `agent/BACKEND-API-DB-002` → `origin/main` delivery pending commit/push
Commit SHA: self
Completion %: 79
