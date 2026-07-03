# BACKEND-TEST-AUDIT-002 Evidence

Prompt ID: BACKEND-TEST-AUDIT-002
Queue: `docs/prompt_queues/backend_test_coverage.md`
Agent/tool: ChatGPT via GitHub connector
Model provider: OpenAI
Model name/id: GPT-5.5 Thinking
Run mode: coverage audit + targeted test implementation + prompt creation
Started from queue status: user-requested broad backend coverage review

## Goal

Map critical backend flows to existing executable test evidence, identify the highest-risk uncovered branches, implement the strongest safe test packages possible through the connector, and add prompt-ready follow-ups for every material gap found.

## Relevant prior mistakes read

- BACKEND-MISTAKE-EVIDENCE-001
- BACKEND-MISTAKE-AUDIT-001
- BACKEND-MISTAKE-VALIDATION-001
- BACKEND-MISTAKE-XREPO-001
- BACKEND-MISTAKE-AUTH-001

## How this run avoids prior mistakes

- Create run evidence before new test/doc commits.
- Separate static coverage findings from runtime/test proof.
- Do not mark new tests validated without a `dotnet test` or checked CI run.
- Use SQLite/PostgreSQL requirements for relational guarantees instead of EF InMemory claims.
- Record contract/mobile sync explicitly for any contract-changing prompt.

## Planned scope

- existing test inventory and prior validation evidence;
- P0 mutation, auth, offline, ingest, settlement, idempotency, persistence and startup/schema surfaces;
- targeted new tests where current runtime can be exercised safely;
- prompt-ready gaps where a runtime/schema decision is required;
- final coverage matrix and prioritized next steps.

## Validation

In progress. No executable .NET environment is available in this connector session.

## Completion

10%
