# BACKEND-PERF-AUDIT-004 Evidence

Prompt ID: BACKEND-PERF-AUDIT-004
Queue: `docs/prompt_queues/backend_performance_optimization.md`
Agent/tool: ChatGPT via GitHub connector
Model provider: OpenAI
Model name/id: GPT-5.5 Thinking
Run mode: docs/audit + prompt creation
Started from queue status: ad-hoc continuation

## Goal

Analyze the current MathLearning backend for performance risks and likely bugs, distinguish confirmed code-level findings from hypotheses, and add precise implementation prompts to the performance queue.

## Guardrails

- Audit only; do not claim runtime fixes.
- Reuse existing prompt IDs and findings where they already exist.
- Create new prompts only for materially new or under-specified risks.
- Every prompt must include exact files, measurements, tests, provider requirements, and completion criteria.
- No performance claim without benchmark, query count, trace, or provider evidence.

## Validation

In progress. No executable .NET checkout is available in this connector session.

## Completion

10%
