---
name: mathlearning-api-contract
description: Review or change MathLearning .NET API endpoints, DTOs, OpenAPI and Flutter compatibility; requires backend and mobile repo contract comparison.
---
# Backend/Flutter API contract parity
1. Read `AGENTS.md`, `.ai/README.md`, `.ai/SOURCE_OF_TRUTH.md`, `docs/API_ENDPOINT_INVENTORY.md` and target endpoint/test. A bounded direct task does **not** create an extra queue owner.
2. Identify backend endpoint, service, DTO/serialization, version, error/status contract and matching consumer in `ivanjovicic/Mathlearning-Mobile-App`. Record **both main SHAs**.
3. Compare method/path, auth identity, JSON field names/types/nulls, pagination, date/time, stable operation ID, 401/403/409/429 semantics and compatibility with older mobile builds.
4. Backend owns authenticated settlement and policy; Flutter owns UI/transport. Never invent mobile-only success or quietly change production wire schemas to satisfy one UI codepath.
5. Test the narrowest representative old-client/nullable/malformed/cross-user/duplicate-request counterexample in API contract/integration tests. Cross-repo changes require an explicit handoff under the existing owner, not a speculative second prompt.
6. Update the owning registered documentation if the contract changes; run `.ai/VALIDATION_SELECTOR.md` focused guarded proof and record exact SHA, proof and NOT RUN limitations.
**Stop:** incompatible persisted schema, uncertain user identity, independently claimed Flutter scope or absent owner policy.
