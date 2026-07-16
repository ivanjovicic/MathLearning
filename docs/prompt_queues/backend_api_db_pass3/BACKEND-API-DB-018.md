# BACKEND-API-DB-018 — Make access tokens revocable and account-state aware

Repository: `ivanjovicic/MathLearning`  
Queue: `docs/prompt_queues/backend_api_db_residuals_pass3_2026_07_16.md`  
Priority: P1 session and privilege security  
Status dependency: run after `BACKEND-API-DB-017` is main-verified  
Run mode: JWT/session-version design + authentication middleware + revocation tests  
Scope exclusion: do not edit `src/MathLearning.Admin/**`

## Problem evidence

- Access JWTs live for 30 minutes and contain copied user/role claims plus `jti`.
- JWT bearer validation checks signature, issuer, audience and expiry, but not current user/account/security-stamp/session state.
- `/auth/revoke-all` revokes refresh-token rows only.
- Existing access tokens remain valid after logout-all, password/security-stamp change, account lock/deletion or role removal until expiry.
- Documentation calls `/auth/revoke-all` “Logout from all devices,” which is stronger than current runtime behavior.

Expected invariant: a server-side security action has a precise and tested effect on both refresh and access authority. Logout-all and security-sensitive account/role changes invalidate previously issued JWTs within a documented maximum propagation bound.

## Deduplication / owner boundary

- `BACKEND-API-DB-007` remains the owner for refresh-token at-rest representation, rotation and retention.
- `BACKEND-TEST-015` remains the refresh-rotation race test owner.
- `BACKEND-API-DB-017` owns credential/verification entry policy and must land first.
- This prompt owns access-token/session invalidation, JWT account-state validation and truthful revoke-all semantics.
- Do not create an unbounded per-token blacklist unless a reviewed threat/scale analysis proves it is necessary.

## Inspect first

- `src/MathLearning.Api/Startup/ServiceRegistrationExtensions.cs`
- `src/MathLearning.Api/Endpoints/AuthEndpoints.cs`
- JWT token creation helpers and auth DTOs
- Identity user/security-stamp/role update paths in API/infrastructure code
- refresh-token entity/service and revoke-all tests/docs
- Redis/distributed cache registration and outage behavior
- privileged endpoint policies and test-auth infrastructure

Maximum initial reads: 12 files. Search budget: 5 searches for token issuance, security stamp, role mutation, lockout/deletion and revoke-all semantics.

## Required design decision

Choose and document one bounded invalidation model, such as:

- monotonic per-user `SessionVersion` persisted in the authoritative database and copied into JWT claims; or
- reviewed Identity security-stamp/version semantics with a distributed cache and explicit refresh/invalidation rules.

The design must state:

```text
security event -> version/stamp mutation -> cache invalidation -> maximum stale window -> JWT rejection code -> refresh-token effect
```

Security events must include logout-all, password reset/change, account lock/deactivation/deletion, and privileged role removal/change. Decide whether ordinary profile/settings changes invalidate sessions; default to no unless security-sensitive.

## Required implementation

1. Add the chosen session/security version to newly issued JWTs and refresh-generated JWTs.
2. Validate current account existence, allowed account state and matching version/stamp during JWT authentication.
3. Avoid an unbounded database lookup on every request. Use a bounded distributed cache or another measured design with explicit TTL/invalidation and safe outage behavior.
4. `revoke-all` must atomically or durably:
   - revoke eligible refresh tokens;
   - advance/invalidate the access-session version;
   - invalidate distributed cache state;
   - return a truthful stable response.
5. Security-sensitive password/account/role changes must invoke the same invalidation owner. Do not scatter ad-hoc version increments.
6. Role claims used by admin/content policies must not remain valid after role removal beyond the documented propagation bound.
7. Unknown/deleted/locked/deactivated users fail authentication safely. Do not return internal account-state detail to clients.
8. Refresh must reject a refresh token whose user/session state is no longer valid, even if the token row itself appears active.
9. Bound session metadata/history retention. Add indexes only when the chosen persistence model requires them.
10. Add safe metrics for invalidation reason/category and cache result without user IDs, raw tokens or `jti` cardinality.
11. Update `REFRESH_TOKEN_SYSTEM.md`, API inventory and mobile logout-all contract to state exact access-token behavior.

## Failure-mode matrix

- user invokes revoke-all, then reuses old access token;
- old access token is used on another API replica immediately and after cache TTL;
- password/security-stamp change while old access/refresh tokens exist;
- user is locked, deleted or deactivated;
- Admin/ContentAuthor role is removed while a privileged JWT is active;
- role is added and old non-privileged token is used;
- session-version row/cache entry is missing;
- distributed cache is unavailable;
- two concurrent revoke-all requests;
- revoke-all DB commit succeeds but cache invalidation fails;
- cache invalidates before DB transaction commits;
- refresh races with revoke-all;
- token lacks the new version claim during compatibility rollout.

## Required tests

### HTTP/session tests

- old JWT is accepted before and rejected after revoke-all;
- new login/refresh after permitted reauthentication issues a valid new-version JWT;
- deleted/locked/deactivated user token is rejected;
- role removal blocks privileged endpoint access within the documented bound;
- true anonymous remains 401 and authenticated stale-session behavior has a stable safe code/header.

### Concurrency/provider tests

- two concurrent revoke-all calls advance state safely and do not restore authority;
- refresh vs revoke-all yields one documented safe result and no surviving unauthorized token chain;
- two API instances observe invalidation through the real distributed cache/provider;
- cache outage follows the documented fail-closed/fail-safe policy for ordinary and privileged routes;
- DB commit/cache invalidation failure injection converges without a window exceeding the stated bound.

### Migration/compatibility tests

- existing users receive initial version/stamp safely;
- legacy JWT without the new claim follows an explicit short migration policy, never indefinite acceptance;
- model/snapshot/migration agree and PostgreSQL indexes match lookup/update paths;
- no token, `jti`, username or raw security stamp is logged.

Use fake time and deterministic barriers; do not use sleeps for TTL/concurrency proof.

## Validation

```text
dotnet test tests/MathLearning.Tests/MathLearning.Tests.csproj -c Release --filter "FullyQualifiedName~AccessTokenRevocation|FullyQualifiedName~RevokeAll|FullyQualifiedName~JwtSession|FullyQualifiedName~AuthRefresh|FullyQualifiedName~Authorization"
dotnet build MathLearning.slnx -c Release
dotnet ef migrations has-pending-model-changes --project src/MathLearning.Infrastructure/MathLearning.Infrastructure.csproj --startup-project src/MathLearning.Api/MathLearning.Api.csproj --context ApiDbContext
```

Run PostgreSQL plus real distributed-cache multi-instance proof. If provider credentials are unavailable, keep `Needs provider validation`; do not infer propagation from one in-memory host.

## Owned paths

- session/security version persistence/service/cache owner;
- JWT issuance and validation integration;
- revoke-all access-token semantics;
- hooks for security-sensitive account/password/role changes;
- focused provider/concurrency tests and contract docs.

## Avoid paths / non-goals

- Blazor Admin UI/pages;
- refresh-token hashing/rotation redesign owned by 007/015;
- storing every access JWT in a new table by default;
- a database query per request without measured budget;
- broad authorization-policy rewrite;
- silent fallback that accepts stale privileged tokens during cache failure;
- changing access-token lifetime as the only fix.

## Stop / handoff conditions

Stop for security/operations review if the repository lacks an approved distributed cache/invalidation availability policy. Stop and split if full account deletion/password-reset/role-management surfaces require unrelated product work. Keep current semantics documented as limited rather than claiming immediate logout-all.

## Completion gate

No Done while an old JWT works after revoke-all beyond the documented bound, privilege removal remains stale, a deleted/locked account remains accepted, cache/provider behavior is untested, legacy JWT acceptance is indefinite, or docs still describe stronger semantics than runtime. Done requires verified main delivery, provider evidence, run log and mobile contract synchronization.

Evidence: `.ai/runs/<date>-BACKEND-API-DB-018-evidence.md`