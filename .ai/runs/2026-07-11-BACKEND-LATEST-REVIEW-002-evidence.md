# BACKEND-LATEST-REVIEW-002 Evidence

Prompt ID: BACKEND-LATEST-REVIEW-002
Queue: `docs/prompt_queues/backend_latest_commit_followups_2026_07_11.md`
Agent/tool: ChatGPT via GitHub connector
Model provider: OpenAI
Model name/id: GPT-5.6 Thinking
Run mode: latest-commit review + prompt queue reconciliation
Started from queue status: ad-hoc user request
Relevant prior mistakes read: BACKEND-MISTAKE-EVIDENCE-001, BACKEND-MISTAKE-VALIDATION-001, BACKEND-MISTAKE-QUEUE-001, BACKEND-MISTAKE-PERF-001, BACKEND-MISTAKE-PERF-002, BACKEND-MISTAKE-PERF-003

## Goal

Review the latest MathLearning backend commits, prompt queues and agent evidence logs; quantify implemented, validated and missing work; identify avoidable duplication; and add precise follow-up prompts to `main` without changing runtime code.

## Guardrails

- Documentation and queue changes only.
- Do not claim build, test, CI or runtime success without executable evidence.
- Do not duplicate existing BACKEND-TEST or BE-PERF implementation scope.
- New prompts must close validation, evidence, CI and queue-ownership gaps.
- Existing P0 implementation IDs remain canonical.

## Sources inspected

- latest 50 commits on `main`, headed by `2ee5ad8260334ca1984bc1c29c2f5bdf7bba486c`;
- `AGENTS.md`;
- `docs/DOCS_INDEX.md`;
- `docs/prompt_queues/backend_test_coverage.md`;
- `docs/prompt_queues/backend_test_followups_2026_07_03.md`;
- `docs/prompt_queues/backend_test_followups_pass2_2026_07_03.md`;
- `docs/prompt_queues/backend_performance_followups_2026_07_03.md`;
- `docs/BACKEND_TEST_COVERAGE_AUDIT_2026_07_03_PASS2.md`;
- `.ai/runs/2026-07-03-BACKEND-TEST-AUDIT-003-evidence.md`;
- `.ai/runs/2026-07-03-BACKEND-PERF-AUDIT-004-evidence.md`;
- `.ai/runs/2026-07-03-BACKEND-TEST-012-evidence.md`;
- `.github/workflows/database-validation.yml`;
- representative latest implementation commits for maintenance, pagination, analytics, explanation and test authentication.

## Preliminary findings

1. The latest implementation pass added 38 executable test cases and three runtime hardening changes, but its own evidence states that build and tests were not run.
2. The active test queue contains a small validated core, a larger implemented/runtime-fixed but unvalidated group, and a similarly large prompt-ready backlog.
3. All nine BE-PERF-009 through BE-PERF-017 packages remain prompt-ready; the latest performance work was a static audit, not runtime implementation.
4. Several queues overlap materially, especially BACKEND-TEST-031 with BE-PERF-009 and BACKEND-TEST-023 with BE-PERF-016.
5. `database-validation.yml` is designed to build, run the full tests, validate PostgreSQL schema, generate coverage and smoke startup, but recent run logs do not link a successful run/artifact to the reviewed head.
6. BACKEND-TEST-012 remains a confirmed P0 refresh-token metadata drift: generated tokens are 88 characters while runtime EF metadata and snapshot still declare 64, despite an existing migration to 128.

## Planned files

- `docs/prompt_queues/backend_latest_commit_followups_2026_07_11.md`
- `docs/prompt_queues/backend_test_coverage.md`
- `docs/DOCS_INDEX.md`
- this evidence file

## Validation

Static repository inspection and queue/evidence reconciliation only. No executable checkout, .NET SDK run or GitHub Actions job log was available through the current connector session.

## Completion

In progress.
