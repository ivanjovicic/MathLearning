# direct-user-request-quiz-backend-availability-2026-09-14 Evidence

Evidence format: v2
Prompt ID: direct-user-request-quiz-backend-availability-2026-09-14
Queue: user-assigned
Run mode: investigation
Owner/hypothesis: quiz/content contract mismatch may cause mobile to reject backend questions; production readiness may not prove playable content.
Files changed: 1 (evidence only; no product code changed)

## Outcome
- Confirmed mobile classic quiz calls `GET /api/quiz/questions?topic=topic_<topicId>&count=10`.
- Confirmed backend returns pre-answer questions without `correctAnswerId`; current mobile `Question.isPlayable` requires it and filters the response to zero playable questions.
- Confirmed current mobile fabricates a local quiz id after legacy loading; backend answer requires a server-issued quiz session, so answer submission can end with `Quiz session not found`.
- Confirmed `Question` defaults to `draft`; `DbSeeder` does not publish seeded questions. Offline bundle filters to published, non-deleted questions.
- Live API probe: `/api/health/`, `/api/health/db`, `/api/health/ready` returned HTTP 200; PostgreSQL connected, schema ready, no pending migrations, 25 total questions. Authenticated content/playability probe was not run because no token was available.

## Validation
- `dotnet test ... --filter FullyQualifiedName~QuizStartContractIntegrationTests`: PASS 8/8.
- `dotnet test ... --filter FullyQualifiedName~QuizAnswerIdempotencyTests`: PASS 5/5.
- Flutter focused model/provider/API tests: PASS; they reproduce the fail-closed filtering behavior.
- Combined sync test run: one unrelated expectation failure in `SyncServiceTests.SyncAsync_UserMismatch_IsRejectedWithoutPersistingAnswer`.
- Combined durable ingest run: two test setup failures caused by invalid empty test email.

## Documentation impact
- Evidence log added only; no runtime, contract, schema or durable architecture document updated.

## Delivery
State: Needs backend/mobile contract handoff
Branch/PR: none
Commit SHA: self
Completion %: 100 (analysis delivered; implementation intentionally not requested)
