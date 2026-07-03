# BACKEND-TEST-011 Evidence

Prompt ID: BACKEND-TEST-011
Queue: `docs/prompt_queues/backend_test_coverage.md`
Agent/tool: ChatGPT via GitHub connector
Model provider: OpenAI
Model name/id: GPT-5.5 Thinking
Run mode: CI coverage visibility
Started from queue status: Ready after coverage artifact

## Goal

Turn existing raw XPlat coverage collection into a visible non-blocking GitHub job summary and retained report artifact, without inventing thresholds before a real stable baseline is observed.

## Planned work

- keep Cobertura and JSON collection;
- generate Markdown summary and HTML report with ReportGenerator;
- publish the summary to `GITHUB_STEP_SUMMARY`;
- upload the generated report with the existing TRX/raw coverage artifacts;
- leave line/branch gates disabled until the first successful artifact is reviewed.

## Relevant prior mistakes read

- BACKEND-MISTAKE-EVIDENCE-001
- BACKEND-MISTAKE-AUDIT-001
- BACKEND-MISTAKE-VALIDATION-001

## Validation

Implementation in progress. Workflow execution evidence is not available yet.

## Completion

15%
