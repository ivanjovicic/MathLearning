# BACKEND-AUTH-REFRESH-LOGOUT-CONTRACT-001 — Normalize refresh/logout failure semantics without changing token authority

Prompt contract: v2  
Priority: P1 auth/session security  
Repository: `ivanjovicic/MathLearning`  
Status: Ready  
Run mode: known-fix  
Delivery target: main  
Mobile companion: `MOB66-AUTH-LOGOUT-REVOCATION-001` in `ivanjovicic/Mathlearning-Mobile-App`.

## Confirmed current-main gaps

The core token authority work is already delivered: security-stamp-backed access-token invalidation is owned by completed `BACKEND-API-DB-018`, refresh-token at-rest/retention remains `BACKEND-API-DB-007`, refresh rotation race proof remains `BACKEND-TEST-015`, and distributed auth throttling remains `BE-PERF-011`.

This prompt owns only the public refresh/logout HTTP contract:

1. `POST /auth/refresh` still returns legacy anonymous bodies such as `{ error: "Invalid or expired refresh token" }` for invalid/expired/missing-user/stamp-mismatch/concurrent-reuse cases, while login/password-reset/registration now expose stable safe machine codes and correlation-aware auth failure responses.
2. `POST /auth/logout` returns `404 { error: "Token not found" }` for an unknown token. That makes normal logout non-idempotent and reveals whether a supplied bearer token exists in storage.
3. Current backend documentation is inconsistent about logout authorization/public semantics even though the endpoint is under the anonymous `/auth` group and authenticates the revocation operation by possession of the refresh token.

## Required behavior

### Refresh
- Route all expected invalid/expired/reused/stamp-mismatch refresh failures through one generic, enumeration-safe 401 contract with a stable code such as `refresh_invalid` (use the repository's established naming convention; do not expose the internal reason).
- Preserve 429 with `Retry-After` and a stable refresh-specific rate-limit code.
- Preserve unexpected failure through the existing safe error path, with correlation/trace information and no raw provider message.
- Keep single-use rotation semantics exactly as-is; do not weaken reuse detection.

### Logout
- Make logout idempotent from the caller's perspective: valid, already-revoked, missing/unknown refresh token should not disclose token existence.
- Prefer a stable success/no-content outcome for best-effort single-device logout, documented consistently across endpoint inventory/OpenAPI/mobile contract.
- Do not require a valid access token merely to revoke a refresh token; logout must remain usable when the access token has expired.
- Never log or echo the raw refresh token.
- Preserve server revocation when the token is valid.
- Unexpected DB failures remain observable internally but return safe generic output.

### Cross-repository compatibility
- Define the exact response matrix consumed by `MOB66-AUTH-LOGOUT-REVOCATION-001`.
- Mobile must be able to clear local credentials regardless of this endpoint's availability; backend behavior must not imply that network logout is a prerequisite for local denial.

## Owned paths

- `src/MathLearning.Api/Endpoints/AuthEndpoints.cs`
- auth response DTO/helper only if needed
- focused refresh/logout endpoint tests
- `API_CONTRACT.md`
- `docs/mobile_api_contract.md`
- `docs/API_ENDPOINT_INVENTORY.md`
- `openapi.yaml` if contract currently declares these responses

Avoid:
- refresh-token entity/migration/hash representation (`BACKEND-API-DB-007`);
- security-stamp/JWT invalidation (`BACKEND-API-DB-018`);
- limiter storage/distribution (`BE-PERF-011`);
- refresh rotation concurrency algorithm (`BACKEND-TEST-015`);
- registration/password-reset implementation;
- broad auth rewrite.

## Required tests

Prove:
- invalid/expired refresh -> generic 401 stable code + correlation, no internal reason;
- stamp mismatch -> same public contract;
- concurrent/reused refresh -> same public contract and no second active token;
- rate limit -> 429 + Retry-After + stable code;
- valid logout revokes token;
- unknown token logout is indistinguishable from already-revoked/valid-from-client success semantics;
- logout works without access JWT;
- unexpected DB error is safe externally and correlated internally;
- no refresh-token value, password, Authorization header, connection string or raw exception message appears in logs/responses.

Run focused endpoint tests, relevant auth-session regression tests, Release build, and contract/docs validation.

## Definition of done

Refresh/logout expose one stable, privacy-safe contract compatible with the mobile revocation flow, while token rotation, security-stamp authority, at-rest ownership and limiter ownership remain unchanged. Done requires executable proof, main delivery and evidence/status synchronization.
