# BACKEND-TEST-021 Evidence

Prompt ID: BACKEND-TEST-021
Queue: `docs/prompt_queues/backend_test_coverage.md`
Agent/tool: ChatGPT via GitHub connector
Model provider: OpenAI
Model name/id: GPT-5.5 Thinking
Run mode: operational security bugfix + endpoint policy tests
Started from queue status: new P1/P0 operational authorization bug found by BACKEND-TEST-AUDIT-002

## Goal

Restrict all `/api/maintenance/*` routes to the explicit admin policy and prove ordinary authenticated users cannot trigger index rebuilds or read database index details.

## Confirmed problem

`MaintenanceEndpoints` currently uses generic `.RequireAuthorization()` and contains `TODO: Add admin role check`. This protects only against anonymous callers, not ordinary learners.

## Planned fix and tests

- require `DesignTokenSecurity.AdminPolicy` on the maintenance route group;
- prove anonymous and ordinary authenticated users receive 401/403 for every maintenance route;
- inspect route metadata and prove all maintenance endpoints carry the admin policy;
- avoid invoking the real index maintenance implementation in authorization tests.

## Relevant prior mistakes read

- BACKEND-MISTAKE-EVIDENCE-001
- BACKEND-MISTAKE-AUDIT-001
- BACKEND-MISTAKE-VALIDATION-001

## Validation

Implementation in progress. No executable .NET environment is available in this connector session.

## Completion

15%
