# BACKEND-TEST-030 Evidence

Prompt ID: BACKEND-TEST-030
Queue: `docs/prompt_queues/backend_test_followups_2026_07_03.md`
Agent/tool: ChatGPT via GitHub connector
Model provider: OpenAI
Model name/id: GPT-5.5 Thinking
Run mode: explanation endpoint safe-error and validation tests
Started from queue status: Prompt-ready

## Goal

Cover explanation endpoints at the HTTP boundary: explicit anonymous denial, validator short-circuiting, valid service delegation, default language, cancellation-token forwarding, stable not-found responses, and generic unexpected-error handling.

## Confirmed problem

`POST /api/explanations/generate` and `/mistake-analysis` return `KeyNotFoundException.Message` directly. Service exception text should not become a public contract or leak internal identifiers/details.

## Planned work

- replace raw not-found exception text with stable safe messages;
- add recording fake `IStepExplanationService`;
- verify invalid requests do not invoke service;
- verify valid response shape and default language;
- verify cancellation token is forwarded;
- verify unexpected failures use global generic error response.

## Validation

In progress. No executable .NET environment is available in this connector session.

## Completion

10%
