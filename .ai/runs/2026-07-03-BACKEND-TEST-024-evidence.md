# BACKEND-TEST-024 Evidence

Prompt ID: BACKEND-TEST-024
Queue: `docs/prompt_queues/backend_test_followups_2026_07_03.md`
Agent/tool: ChatGPT via GitHub connector
Model provider: OpenAI
Model name/id: GPT-5.5 Thinking
Run mode: maintenance refactor + endpoint contract tests
Started from queue status: Prompt-ready

## Goal

Make maintenance operations injectable and testable, ensure GET routes are side-effect free, add cancellation and non-overlap semantics, and cover positive admin behavior without touching a real PostgreSQL database.

## Confirmed problem

- Endpoints instantiate `IndexMaintenanceService` directly.
- `GET /api/maintenance/index-stats` calls `RebuildCorruptedIndexesAsync`, which can execute `REINDEX` and `ANALYZE` from a GET request.
- Service methods do not accept cancellation tokens.
- Scheduled and manual rebuild paths do not share an in-process overlap guard.

## Planned work

- introduce `IIndexMaintenanceService`;
- split read-only statistics from rebuild;
- register one singleton implementation used by endpoint and hosted service;
- pass cancellation tokens through Npgsql calls;
- serialize in-process rebuilds;
- use an injectable fake for positive admin endpoint tests;
- retain existing anonymous/non-admin policy tests.

## Validation

In progress. No executable .NET environment is available in this connector session.

## Completion

10%
