# BE-PERF-011 Evidence

Evidence format: v2
Prompt ID: BE-PERF-011
Queue: backend_performance_followups_2026_07_03
Agent/tool: Codex shell
Model provider: unknown-not-exposed
Model name/id: unknown-not-exposed
Client/IDE: unknown-not-exposed
Run mode: known-fix
Token budget: high
Started at UTC: 2026-07-30T10:47:49Z
Completed at UTC: 2026-07-30T10:52:00Z
Elapsed time: 00:04:11
Relevant prior mistakes read: BACKEND-MISTAKE-EVIDENCE-001, BACKEND-MISTAKE-VALIDATION-001, BACKEND-MISTAKE-PERF-001, BACKEND-MISTAKE-PERF-002, BACKEND-MISTAKE-PERF-003, BACKEND-MISTAKE-SCOPE-001
How this run avoids prior mistakes: apply BACKEND-MISTAKE-EVIDENCE-001; apply BACKEND-MISTAKE-VALIDATION-001; apply BACKEND-MISTAKE-PERF-001; apply BACKEND-MISTAKE-PERF-002; apply BACKEND-MISTAKE-PERF-003; apply BACKEND-MISTAKE-SCOPE-001
Owner/hypothesis: Bounded rate limiting should evict idle partitions, return accurate retry-after semantics, and keep proxy/IP identity consistent without allowing unbounded cardinality or multi-replica ambiguity.
Files inspected: 9
Files changed: 2
Searches: 8
Validation runs: 1
Failed retries: 0

## Outcome
- Existing implementation already provides bounded partitions, idle eviction, accurate `Retry-After`, proxy-aware client identity and per-replica semantics.
- Focused rate-limit middleware, store, identity and metrics tests passed `19/19`.
- No runtime code change was needed for this prompt.

## Changed paths
- `.ai/runs/2026-07-30-BE-PERF-011-evidence.md`
- `docs/prompt_queues/backend_performance_followups_2026_07_03.md`

## Validation
Validation run: `dotnet test tests\MathLearning.Tests\MathLearning.Tests.csproj --filter "FullyQualifiedName~InMemoryRateLimitCounterStoreTests|FullyQualifiedName~InMemorySlidingWindowRateLimitMiddlewareTests|FullyQualifiedName~ForwardedHeadersProxyTrustIntegrationTests|FullyQualifiedName~RateLimitMetricsEndpointTests|FullyQualifiedName~RateLimitClientIdentityTests" -m:1 -p:UseSharedCompilation=false --no-restore --disable-build-servers` -> passed (19 tests)
Validation not run: none

## Exceptions and learning
Mistakes observed: none
Waste: none
Missed: none
Follow-up: none
Residual risk: single-node in-memory semantics remain intentionally per-replica; a distributed shared limiter would be required for global cross-replica enforcement
Documentation impact: updated the owning queue row with validation evidence
Cross-repo impact: no

## Delivery
State: Needs validation
Branch/PR: direct main
Commit SHA: self
Completion %: 79
