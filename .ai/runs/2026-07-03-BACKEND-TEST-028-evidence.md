# BACKEND-TEST-028 Evidence

Prompt ID: BACKEND-TEST-028
Queue: `docs/prompt_queues/backend_test_followups_2026_07_03.md`
Agent/tool: ChatGPT via GitHub connector
Model provider: OpenAI
Model name/id: GPT-5.5 Thinking
Run mode: shared pagination hardening + boundary tests
Started from queue status: Prompt-ready

## Goal

Prevent integer overflow and extreme-offset abuse in page-based backend reads by introducing one shared pagination normalizer and applying it to analytics/recommendations and bug-report routes/services.

## Confirmed problem

- Analytics endpoints calculate `page * pageSize` and `(page - 1) * pageSize` directly.
- Bug report service calculates `Skip((page - 1) * pageSize)` directly.
- Page size is bounded, but page itself is not capped.
- `int.MaxValue` combinations can overflow or request impractically large offsets/takes.

## Planned work

- add shared Application-layer pagination value/helper using safe bounded arithmetic;
- cap page, normalize page size and expose safe skip/fetch count;
- apply at HTTP boundaries and again in bug service defense-in-depth;
- add direct helper boundary tests and HTTP forwarding tests for extreme values.

## Validation

In progress. No executable .NET environment is available in this connector session.

## Completion

10%
