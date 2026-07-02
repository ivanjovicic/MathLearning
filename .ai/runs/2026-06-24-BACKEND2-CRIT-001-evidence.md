# BACKEND2-CRIT-001 Evidence

Prompt ID: BACKEND2-CRIT-001
Queue: docs/prompt_queues/backend_second_pass_risk_prevention.md
Agent/tool: Cursor Agent
Model provider: unknown-not-exposed
Model name/id: unknown-not-exposed
Client/IDE: Cursor
Run mode: implementation/test
Token budget: medium
Elapsed time: unknown-not-recorded
Relevant prior mistakes read: BACKEND-MISTAKE-AUDIT-001, BACKEND-MISTAKE-EVIDENCE-001, BACKEND-MISTAKE-VALIDATION-001
How this run avoids prior mistakes: targeted tests + run log + validation command recorded before Done

Commit SHA: pending (see batch commit)
Validation command:

```bash
dotnet test tests/MathLearning.Tests/MathLearning.Tests.csproj --filter "RateLimit|ForwardedHeaders|Proxy"
```

Result:

```text
Passed!  - Failed: 0, Passed: 11, Skipped: 0, Total: 11
```

Risk prevented:

- Spoofed `X-Forwarded-For` cannot create unlimited rate-limit buckets.
- Rate limiting keys authenticated requests by user id; anonymous requests use physical TCP peer IP captured before forwarded-headers middleware.

Runtime changes:

- `ConnectionRemoteIpMiddleware` — preserves physical peer IP before `UseForwardedHeaders`.
- `RateLimitClientIdentity` — resolves `user:{id}` or `ip:{physical}` keys.
- `InMemoryRateLimitCounterStore` + `IRateLimitCounterStore` — injectable counter store for tests.
- `ForwardedHeadersConfiguration` — loads `KnownProxies` / `KnownNetworks` from config (Fly private `fdaa::/48`, RFC1918); production warning when trust boundary is loopback-only.
- Pipeline: `UseAuthentication` moved before rate-limit middleware.
- `appsettings.json` — `ForwardedHeaders` section with explicit proxy trust.

Hosting boundary:

- Fly.io terminates TLS at the edge; only traffic from configured private proxy networks may rewrite forwarded headers. Client-supplied `X-Forwarded-For` on a direct connection does not affect rate-limit identity.

Tests added:

- `RateLimitClientIdentityTests`
- `InMemorySlidingWindowRateLimitMiddlewareTests`
- `ForwardedHeadersConfigurationTests`
- `ForwardedHeadersProxyTrustIntegrationTests`

## Mistakes observed

none

## Completion %

95%

## Residual risk

- Hosting must configure `ForwardedHeaders:KnownNetworks` for production edge topology; loopback-only trust logs a startup warning.

## Commit SHA

pending (see batch commit)