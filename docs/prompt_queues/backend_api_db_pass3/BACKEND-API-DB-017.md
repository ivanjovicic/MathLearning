# BACKEND-API-DB-017 — Add lockout-aware credential protection and explicit account verification

Repository: `ivanjovicic/MathLearning`  
Queue: `docs/prompt_queues/backend_api_db_residuals_pass3_2026_07_16.md`  
Priority: P0/P1 account security  
Run mode: authentication policy + endpoint contract + distributed abuse tests  
Scope exclusion: do not edit `src/MathLearning.Admin/**`

## Problem evidence

- API Identity configuration accepts six-character passwords and disables digit/lowercase/uppercase/non-alphanumeric requirements.
- Mobile registration duplicates only a six-character password check and validates email with `Contains('@')`.
- Login calls `UserManager.CheckPasswordAsync`, so failed attempts do not use the normal lockout/access-failure state machine.
- Registration gives distinct username/email-exists responses.
- Mobile accounts are created with `EmailConfirmed = false` and immediately receive full access/refresh tokens.
- The only visible request throttle is the general process-local sliding window; it is not an auth-specific account/IP/device policy.

Expected invariant: credential entry is protected by one server-owned, lockout-aware, enumeration-safe and horizontally consistent policy. An unverified account cannot silently receive the same authority as a verified account unless an explicit reviewed no-email/guardian provisioning mode owns that decision.

## Deduplication / owner boundary

- `BE-PERF-011` remains the canonical owner for bounded/distributed rate-limit storage and multi-replica mechanics. This prompt owns auth-specific keys, budgets and responses; reuse the shared limiter abstraction.
- `BACKEND2-CRIT-001` owns trusted proxy/IP resolution.
- `BACKEND2-CRIT-002`, `BACKEND-TEST-015` and `BACKEND-API-DB-007` own refresh rotation and refresh-token storage.
- `BACKEND-API-DB-013` owns atomic Identity/profile/token provisioning and orphan reconciliation.
- `BACKEND-API-DB-018` owns post-issuance access-token/session revocation and runs after this prompt.

## Inspect first

- `src/MathLearning.Api/Startup/ServiceRegistrationExtensions.cs`
- `src/MathLearning.Api/Endpoints/AuthEndpoints.cs`
- auth request/response DTOs and `RefreshTokenService`
- current rate-limit middleware/store/client identity
- Identity schema/model and current auth tests/factories
- mobile registration/login/verification contract docs
- production email/guardian/device-verification capabilities, if any

Maximum initial reads: 12 files. Search budget: 5 exact searches for lockout, password options, email confirmation, auth aliases and limiter owners.

## Required decisions before coding

Create and document a table for:

```text
endpoint -> identifier(s) -> credential/account check -> account/IP/device budget -> public failure contract -> audit event -> retry/reset rule
```

Cover `/auth/mobile/register`, `/auth/register`, `/auth/login`, `/api/auth/login`, `/auth/refresh` and any compatibility aliases. Decide one account-verification mode:

1. verified email/guardian flow before full tokens; or
2. explicit reviewed no-email/managed-child/device-provisioning mode with equivalent ownership proof and restricted account state.

Do not leave `EmailConfirmed=false` as an unused flag while claiming verified identity.

## Required implementation

1. Use the Identity password validator/options as the single password-policy owner. Remove divergent endpoint-only minimum checks.
2. Prefer a passphrase-friendly policy with reviewed minimum and maximum lengths; do not add superficial mandatory character-class rules without threat-model justification. Reject extremely long inputs before expensive hashing.
3. Normalize username/email through Identity and validate email with a real parser/validator plus exact length limits.
4. Use a lockout-aware login path (`SignInManager.CheckPasswordSignInAsync(..., lockoutOnFailure: true)` or an equivalent correctly tested Identity failure/reset sequence).
5. Configure and document lockout threshold/duration/allowed-for-new-users. Correct password during lockout must not bypass the lock.
6. Add auth-specific throttling with combined account and physical-network/device dimensions. Use the shared/distributed store contract from BE-PERF-011; do not depend only on one process or only one IP.
7. Make external registration/login failures enumeration-safe. Username/email duplicate, unknown user, bad password, unverified and locked states must follow a reviewed public contract and timing/work profile, while safe internal audit categories remain distinguishable.
8. Bound and normalize User-Agent/device/IP metadata before persistence/logging. Do not log raw passwords, tokens or high-cardinality credential payloads.
9. Define verification token generation, expiry, single use, resend throttling and replay behavior if verification is enabled. Persist only safe token representation.
10. Issue full access/refresh tokens only when the selected account-state policy allows it. A pending account must receive a limited response/state, not ordinary learner authority.
11. Keep `/auth/login` and `/api/auth/login` contract-equivalent by routing both through one handler/service.
12. Return stable safe 400/401/409/429 semantics and `Retry-After` where appropriate; do not leak whether a specific account exists.

## Failure-mode matrix

- five/specified failed attempts then correct password;
- concurrent failed attempts on two API replicas;
- one account attacked from many IPs;
- many accounts attempted behind one NAT;
- unknown username vs known username with wrong password;
- duplicate username/email registration;
- mixed case/Unicode/whitespace-normalized identifiers;
- extremely long password/email/username;
- unverified account attempts login/refresh;
- expired/replayed verification token;
- resend verification flood;
- lockout expiry and successful login reset;
- unavailable distributed limiter/store;
- forwarded-header spoof attempt under the existing trusted-proxy owner.

## Required tests

### Identity/credential tests

- configured password/passphrase boundaries and maximum input size;
- failed login increments account failure state;
- threshold locks account and lockout expires according to fake time;
- successful login resets failure count only when not locked;
- concurrent failure updates cannot lose counts or permit extra unbounded attempts;
- aliases execute the identical handler/policy.

### Enumeration/contract tests

- unknown user, wrong password, duplicate username/email and unverified state use the reviewed public shape;
- responses contain no raw Identity/provider details;
- safe audit category distinguishes causes without sensitive values;
- response/timing test uses a broad bounded threshold and equivalent expensive path, not a brittle nanosecond assertion.

### Rate-limit/provider tests

- account and network/device budgets both apply;
- two service instances share counters when distributed mode is required;
- store outage follows a documented fail-open/fail-closed policy appropriate to authentication;
- `Retry-After` reflects actual reset time;
- bounded cardinality/retention remains owned and proven with BE-PERF-011.

### Verification tests

- unverified account cannot obtain ordinary tokens;
- valid verification succeeds exactly once;
- expired/replayed token fails safely;
- resend is throttled and does not enumerate accounts;
- selected managed/no-email mode, if used, has explicit authority tests.

Use fake time and deterministic barriers; do not use sleeps for lockout/concurrency proof.

## Validation

```text
dotnet test tests/MathLearning.Tests/MathLearning.Tests.csproj -c Release --filter "FullyQualifiedName~AuthLogin|FullyQualifiedName~AuthRegistration|FullyQualifiedName~Lockout|FullyQualifiedName~AccountVerification|FullyQualifiedName~RateLimit"
dotnet build MathLearning.slnx -c Release
dotnet ef migrations has-pending-model-changes --project src/MathLearning.Infrastructure/MathLearning.Infrastructure.csproj --startup-project src/MathLearning.Api/MathLearning.Api.csproj --context ApiDbContext
```

Run PostgreSQL/provider tests for concurrent failure counters and shared limiter/verification persistence. If the shared distributed limiter owner is not implemented, leave this row `Ready after BE-PERF-011` or `Needs provider validation`; do not claim multi-replica protection from in-memory tests.

## Owned paths

- API Identity password/lockout/account verification configuration;
- one canonical auth credential handler/service for login/register aliases;
- auth-specific limiter policy/integration;
- enumeration-safe auth contracts and audit categories;
- focused tests and mobile/API documentation.

## Avoid paths / non-goals

- Blazor Admin login/UI;
- refresh-token hashing/rotation implementation;
- access-token revocation/session-version implementation owned by 018;
- full password-reset/social-login/MFA product design unless required as a separately approved prompt;
- custom password hashing or home-grown crypto;
- a second generic rate-limit store;
- logging raw identifiers at warning level without redaction policy.

## Stop / handoff conditions

Stop and record a product/security owner decision if email/guardian verification authority is unavailable. Do not invent an email provider or bypass verification silently. Stop and split if MFA/password reset/social login is required or more than 12 runtime/test files are needed beyond migration/evidence/docs.

## Completion gate

No Done while login bypasses Identity failure accounting, six-character weak policy remains, account enumeration remains externally distinguishable, unverified accounts receive ordinary authority without an explicit managed mode, multi-replica throttling is unproven, or focused/provider tests are queued/red. Done requires verified main delivery, run evidence and mobile contract synchronization.

Evidence: `.ai/runs/<date>-BACKEND-API-DB-017-evidence.md`