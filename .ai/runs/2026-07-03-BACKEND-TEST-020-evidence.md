# BACKEND-TEST-020 Evidence

Prompt ID: BACKEND-TEST-020
Queue: `docs/prompt_queues/backend_test_coverage.md`
Agent/tool: ChatGPT via GitHub connector
Model provider: OpenAI
Model name/id: GPT-5.5 Thinking
Run mode: security bugfix + endpoint authorization tests
Started from queue status: new P1 authorization bug found by BACKEND-TEST-AUDIT-002

## Goal

Prevent ordinary authenticated learners from listing, reading, or updating all bug reports through `/api/bugs` admin routes, and make the submission route's auth contract explicit.

## Relevant prior mistakes read

- BACKEND-MISTAKE-EVIDENCE-001
- BACKEND-MISTAKE-AUDIT-001
- BACKEND-MISTAKE-VALIDATION-001
- BACKEND-MISTAKE-XREPO-001

## Confirmed problem

`BugEndpoints` creates its admin group with generic `.RequireAuthorization()` only. The list, detail, and update routes therefore lack an admin policy. The report group is marked `.AllowAnonymous()` even though the handler rejects requests without a `userId` claim.

## Planned fix and tests

- require `DesignTokenSecurity.AdminPolicy` for admin bug routes;
- require authentication explicitly for report/mine routes;
- prove anonymous and normal authenticated users cannot access admin routes;
- prove admin role can access them;
- prove denied writes never invoke the bug service;
- keep page/pageSize bounds covered.

## Validation

Implementation in progress. No executable .NET environment is available in this connector session.

## Completion

15%
