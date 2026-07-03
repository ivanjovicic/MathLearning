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

## Relevant prior mistakes read

- BACKEND-MISTAKE-EVIDENCE-001
- BACKEND-MISTAKE-AUDIT-001
- BACKEND-MISTAKE-VALIDATION-001

## Files inspected

- `.github/workflows/database-validation.yml`
- `tests/MathLearning.Tests/coverage.runsettings`
- `tests/MathLearning.Tests/MathLearning.Tests.csproj`
- `docs/BACKEND_TEST_COVERAGE_STRATEGY.md`

## Files changed

- `.github/workflows/database-validation.yml`
- `docs/prompt_queues/backend_test_coverage.md`
- `docs/BACKEND_TEST_COVERAGE_AUDIT_2026_07_03.md`
- `docs/DOCS_INDEX.md`
- `.ai/runs/2026-07-03-BACKEND-TEST-011-evidence.md`

## Implemented

- Existing TRX, Cobertura and JSON collection is retained.
- The workflow installs `dotnet-reportgenerator-globaltool` after the test step with `if: always()`.
- All produced Cobertura files are merged.
- ReportGenerator creates HTML, merged Cobertura and `MarkdownSummaryGithub` output.
- The Markdown summary is appended to `GITHUB_STEP_SUMMARY`.
- Raw results and generated coverage reports are uploaded in the same retained artifact.
- A missing coverage file produces an explicit job-summary note rather than a misleading empty report.
- No line/branch threshold was added because no stable measured baseline was available in this run.

## Validation run

Static workflow inspection only:

- existing `XPlat Code Coverage` collector and settings are preserved;
- generated report paths are included in the artifact upload;
- report generation and upload use `if: always()` so failed tests still retain evidence;
- threshold rollout remains consistent with `BACKEND_TEST_COVERAGE_STRATEGY.md`.

## Validation not run

No GitHub Actions execution evidence was available while this run was completed. The workflow must run on `main` or a pull request before the coverage summary format, ReportGenerator installation and artifact paths are considered validated.

## Required validation

1. Inspect the next `Database Validation` workflow run.
2. Confirm full tests execute and `mathlearning-test-results` contains:
   - TRX;
   - raw Cobertura/JSON;
   - `artifacts/coverage-report/index.html`;
   - merged Cobertura;
   - GitHub Markdown summary.
3. Record overall and assembly/namespace line and branch baseline.
4. Add thresholds only after at least one stable successful baseline, preferably more than one run.

## Residual risk

- Workflow YAML and ReportGenerator report-type/path behavior are not execution-proven yet.
- Overall and critical-namespace coverage percentages remain unknown until the workflow artifact exists.
- There is intentionally no blocking coverage floor yet.

## Completion

88%

## Commit SHAs

- `7058c24b2af229de4d4733af0bca4e048a7ae390` — start evidence
- `3784a38048fd9772770f589c79ad3f0e0bd5205d` — publish non-blocking coverage summary/report
- `d0f6bf3548328f8586b17acb0bb83da50e3483a7` — coverage audit and baseline status
- `3a97eaa2c1ecaeb4274bf0ac36d84403ab8ed589` — documentation index
- `c431804ef5bbc2c86d6f8b4da9ec2ab41eec75c7` — reconciled central test queue

## Cross-repo sync

Not applicable. CI/test evidence only; no mobile contract change.
