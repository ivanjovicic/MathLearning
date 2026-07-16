# Backend API / Database Residual Audit — 2026-07-16 Pass 3

> Static code and Git-history audit only. This document identifies currently visible risks and creates implementation prompts. It is not runtime-fix proof and does not claim that an exploit occurred.

Repo: `ivanjovicic/MathLearning`  
Reviewed main: `0d0a1965b88f20855987c865fcd4038c856cdfa8`  
Scope: ASP.NET Core API, application, domain, infrastructure, persistence, migrations and backend tests  
Explicit exclusion: `src/MathLearning.Admin/**` Blazor Admin project

## Audit method

The current API/runtime code was compared against:

- `BACKEND_CRITICAL_APP_FLOW_AUDIT_2026_07_01.md` and `BACKEND_SECOND_PASS_APP_FLOW_AUDIT_2026_07_01.md`;
- `BACKEND_PERFORMANCE_BUG_AUDIT_2026_07_03.md`;
- API/DB residual passes 1 and 2 (`BACKEND-API-DB-001…015`);
- active BACKEND-TEST, BE-PERF, migration and latest-commit queues;
- current open PRs and remote claim branches;
- the current endpoint inventory, agent rules and mistake ledger.

Candidates already owned by an active prompt were rejected before allocating a new ID. No remote `agent/claim/*` branch owns the selected areas. Open PR #3 is a stale test-coverage candidate and does not own these runtime boundaries.

## Executive priority

| Priority | New prompt | Finding | Main impact |
|---|---|---|---|
| P0/P1 | `BACKEND-API-DB-016` | Bug-report screenshots are stored on local disk and returned as `/uploads/screenshots/*`; static-file middleware exposes that path outside the authorization enforced by bug-report endpoints. | Child/user privacy, unauthorized file disclosure, non-durable multi-replica storage |
| P0/P1 | `BACKEND-API-DB-017` | Mobile auth accepts six-character passwords with no composition requirements, login does not record Identity access failures/lockout, duplicate registration responses reveal account existence, and unconfirmed accounts receive full tokens. | Credential stuffing, account enumeration and account takeover |
| P1 | `BACKEND-API-DB-018` | `revoke-all` revokes refresh tokens only; already issued 30-minute JWTs remain valid and JWT validation does not check current account/security-stamp/session state. | False logout-all semantics, stale privileges and continued access after account action |
| P1 | `BACKEND-API-DB-019` | Production startup runs a hard-coded cosmetics catalog seeder, overwrites selected fragment fields on every start, and catches all failures while the API continues serving. | Silent catalog drift, broken reward/economy dependencies and misleading readiness |

---

## Finding 1 — Bug-report screenshots bypass bug-report authorization

### Current evidence

- `Program.cs` serves the physical `uploads` directory under `/uploads` and explicitly blocks only `/uploads/avatars`; screenshots are not blocked.
- `LocalScreenshotStorageService` writes to `AppContext.BaseDirectory/uploads/screenshots` and returns `/uploads/screenshots/{fileName}`.
- Stored names include the authenticated `userId` plus a GUID.
- `BugReportService` persists that URL and returns it in reporter/admin DTOs.
- `BugEndpoints` correctly protects bug-report metadata, but a caller can fetch the static screenshot without passing through reporter/admin authorization.
- Container-local storage is not durable or shared across replicas/redeploys.

### Failure scenarios

1. A screenshot URL leaks through client logs, browser history, support copy/paste or a bug DTO.
2. An anonymous or different authenticated user requests the static path directly and receives the image.
3. A deploy/restart removes the local file while the database still advertises the URL.
4. Two API replicas store/read different local files, making access depend on routing.
5. A DB failure or bug deletion leaves a file with no durable retention/ownership lifecycle.

### Deduplication verdict

- `BACKEND-TEST-025` owns input size/type validation and upload/DB compensation. It does **not** own authenticated screenshot reads, static-file exclusion or production durability. `BACKEND-API-DB-016` is a narrower runtime/privacy extension and must share tests rather than duplicate its validation work.
- `BACKEND-API-DB-014` owns legacy profile photo avatars, whose public/private product contract is different. Do not merge avatar and bug-screenshot storage policies.
- `BACKEND-CRIT-004` blocked public avatar static serving; it does not cover `/uploads/screenshots`.

---

## Finding 2 — Authentication has no account-abuse boundary

### Current evidence

- Identity password settings require only six characters and disable digit, lowercase, uppercase and non-alphanumeric requirements.
- Mobile registration repeats the six-character check and performs only `email.Contains('@')` validation.
- Login uses `UserManager.CheckPasswordAsync`; it does not call a lockout-aware sign-in path or update `AccessFailedCount`.
- No auth-route-specific login/register/refresh budget is visible; all traffic shares the general in-memory 100-request sliding window.
- Username-not-found and bad-password paths are distinguishable in internal warning logs.
- Registration returns distinct `Username already taken` and `Email already registered` responses.
- Mobile registration sets `EmailConfirmed = false` but immediately issues access and refresh tokens.

### Failure scenarios

1. A distributed attacker tries common six-character passwords without triggering Identity lockout.
2. The general per-process limiter is multiplied by replica count and is not an auth-specific account/IP/device defense.
3. Registration responses enumerate valid usernames/emails.
4. Automated accounts obtain full learner tokens with unverifiable email ownership.
5. An attacker targets one account from many IPs, or many accounts behind one NAT, without a reviewed account-plus-network policy.

### Deduplication verdict

- `BE-PERF-011` owns bounded/distributed rate-limit storage and replica semantics. `BACKEND-API-DB-017` owns **auth-specific** credential policy, Identity failure/lockout semantics, enumeration-safe contracts and account verification. It must integrate with or depend on the shared limiter rather than rebuild the generic store.
- `BACKEND2-CRIT-001` owns proxy trust and physical-IP resolution.
- `BACKEND2-CRIT-002`, `BACKEND-TEST-015` and `BACKEND-API-DB-007` own refresh rotation/storage, not password abuse.
- `BACKEND-API-DB-013` owns atomic account provisioning/orphan cleanup, not credential quality or verification.

---

## Finding 3 — Logout-all does not revoke access tokens or stale privileges

### Current evidence

- Access tokens are self-contained JWTs with a 30-minute expiry, `sub`, `userId`, role claims and `jti`.
- JWT bearer setup validates signature, issuer, audience and expiry only; no current user/session/security-stamp/account-state validation is configured.
- `/auth/revoke-all` marks active refresh-token rows revoked but does not invalidate already issued JWTs.
- The documented endpoint is described as “Logout from all devices,” creating stronger semantics than the implementation provides.
- Role claims are copied into the JWT at issuance, so role removal does not affect an existing token until expiry.

### Failure scenarios

1. A stolen access token keeps working after the user invokes logout-all.
2. A locked/deleted/deactivated account continues calling authenticated endpoints until token expiry.
3. Removing an Admin or ContentAuthor role leaves the old privileged token usable.
4. Password reset/security-stamp rotation does not invalidate the current JWT.
5. A cache-based solution accepts stale session state longer than the documented revocation budget.

### Deduplication verdict

- `BACKEND-API-DB-007` protects refresh tokens at rest and their retention; `BACKEND-API-DB-018` owns access-token/session invalidation and truthful revoke-all semantics.
- `BACKEND-API-DB-017` owns credential/verification entry policy; run `018` after `017` because both touch auth registration and tests.
- Do not introduce a per-request unbounded database lookup; use a reviewed session-version/security-stamp design with a measured cache/staleness contract.

---

## Finding 4 — Cosmetics catalog source of truth mutates silently at API startup

### Current evidence

- `Program.cs` invokes `CosmeticStartupSeeder.EnsureCosmeticItemsAsync` during startup after schema checks.
- The seeder contains a large hard-coded product/catalog data set in application source.
- Most rows use `ON CONFLICT DO NOTHING`, so source changes do not reliably update existing catalog rows.
- Selected fragment items use `ON CONFLICT DO UPDATE` and overwrite `FragmentLabel`, `FragmentsRequired`, `IsDefault` and `UnlockType` on every startup.
- Every exception is caught and logged as a warning; readiness/startup continues even when required defaults or fragment definitions are absent.
- Catalog/inventory code assumes default items exist and server-authoritative reward settlement refers to fragment labels from this catalog.

### Failure scenarios

1. An operator-approved catalog change is silently reverted by the next deployment.
2. A source rename or corrected price is ignored because an existing row uses `DO NOTHING`.
3. Seeding fails after a migration/schema mismatch, but health remains green and users later hit missing-item or reward failures.
4. Different application versions start concurrently and race with different embedded catalogs.
5. Clean and upgraded databases produce different catalogs without a version/audit record.

### Deduplication verdict

- `BACKEND-MIGRATION-001` owns historical cosmetics FK migration drift, not catalog data ownership/readiness.
- `BACKEND-API-DB-009` owns cosmetic entitlement and server-priced purchase behavior, not catalog deployment/versioning.
- `BACKEND-API-DB-015` owns pending economy/cosmetics operation recovery.
- `BACKEND-API-DB-019` must preserve all those owners and change only catalog source-of-truth, deployment and readiness semantics.

---

## Rejected or deferred candidates

The following were reviewed but not allocated new IDs:

- user settings accept arbitrary `Theme`, legacy `Language` and notification-time strings; material, but lower priority than the four selected security/privacy/readiness boundaries;
- public profile/search fields remain governed by the already validated identity allowlist owner;
- refresh-token plaintext storage/retention remains `BACKEND-API-DB-007`;
- GET-side writes, rate-limit cardinality, workers, cache stampedes, practice/adaptive concurrency and outbox remain owned by existing BE-PERF/BACKEND-TEST rows;
- photo avatars remain `BACKEND-API-DB-014`;
- Blazor Admin code was deliberately excluded.

## New queue

Detailed implementation packets are in:

- `docs/prompt_queues/backend_api_db_residuals_pass3_2026_07_16.md`
- `docs/prompt_queues/backend_api_db_pass3/BACKEND-API-DB-016.md`
- `docs/prompt_queues/backend_api_db_pass3/BACKEND-API-DB-017.md`
- `docs/prompt_queues/backend_api_db_pass3/BACKEND-API-DB-018.md`
- `docs/prompt_queues/backend_api_db_pass3/BACKEND-API-DB-019.md`

## Validation boundary

This audit used exact current-main file reads, current queue/PR/claim searches and source-level failure analysis. No local checkout, `dotnet build`, `dotnet test`, PostgreSQL, Redis, object storage or deployment environment was available in the connector session. Every queued prompt requires executable regression/provider proof and verified delivery before it can be marked implemented or validated.