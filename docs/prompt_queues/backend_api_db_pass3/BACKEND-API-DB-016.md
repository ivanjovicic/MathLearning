# BACKEND-API-DB-016 — Make bug-report screenshots private, durable and ownership-safe

Repository: `ivanjovicic/MathLearning`  
Queue: `docs/prompt_queues/backend_api_db_residuals_pass3_2026_07_16.md`  
Priority: P0/P1 child/user privacy and storage integrity  
Run mode: endpoint/storage redesign + authorization/compensation/provider tests  
Scope exclusion: do not edit `src/MathLearning.Admin/**`

## Problem evidence

- `Program.cs` serves the physical `uploads` directory at `/uploads` and blocks only `/uploads/avatars`.
- `LocalScreenshotStorageService` writes bug screenshots under `AppContext.BaseDirectory/uploads/screenshots` and returns a directly fetchable `/uploads/screenshots/{fileName}` URL.
- The filename includes the authenticated `userId` and a GUID.
- `BugReportService` persists/returns this URL, while `BugEndpoints` protects metadata with user/admin authorization that the static-file request bypasses.
- Container-local files are not durable or shared across replicas/redeploys.

Expected invariant: a bug screenshot is a private attachment. Only its reporter and exact admin policy may read it; the database never advertises an anonymously fetchable path, and production storage has explicit durability, ownership, retention and compensation semantics.

## Deduplication / owner boundary

- Extend, do not duplicate, `BACKEND-TEST-025`: it owns free-text/image limits and upload/DB compensation fixtures.
- Do not absorb `BACKEND-API-DB-014`: profile photo avatars have a separate public/deprecation contract.
- Preserve `BACKEND-TEST-020` bug metadata authorization and `DesignTokenSecurity.AdminPolicy`.
- This prompt is the canonical runtime owner for screenshot read authorization, private storage keying, durable production storage and attachment lifecycle.

## Inspect first

- `src/MathLearning.Api/Program.cs`
- `src/MathLearning.Api/Endpoints/BugEndpoints.cs`
- `src/MathLearning.Infrastructure/Services/BugReportService.cs`
- `src/MathLearning.Infrastructure/Services/LocalScreenshotStorageService.cs`
- `IScreenshotStorageService`, bug DTO/entity/EF mapping/migrations
- BACKEND-TEST-020/025 tests and avatar static-file guard tests
- deployment configuration and any approved object-storage client already present

Maximum initial reads: 12 files. Search budget: 4 exact searches for screenshot routes/storage/DTOs/tests.

## Required design

1. Replace public URL ownership with an opaque attachment identifier/storage key. Do not expose physical paths, bucket names, user IDs or predictable filenames.
2. Remove `/uploads/screenshots/*` from anonymous static-file serving. A static middleware 404 shim is acceptable only as a compatibility deny rule, not as the final authorized read path.
3. Add one canonical authorized download endpoint, for example `GET /api/bugs/{bugId}/screenshot`:
   - reporter can read only their own attachment;
   - exact admin policy can read any attachment;
   - anonymous and another learner receive 401/403 or privacy-safe 404 according to one documented policy;
   - missing storage object returns a stable safe contract and correlation ID, not a filesystem error.
4. Store screenshots outside the public web root/static-file tree.
5. Production storage must be durable and shared (approved object/blob storage or another reviewed durable provider). Local storage may remain only for Development/Test behind an explicit environment guard.
6. If durable production configuration is missing, fail startup/readiness or disable screenshot capability truthfully; never silently fall back to ephemeral local disk.
7. Define upload state: pending -> persisted bug row -> finalized attachment, or deterministic delete compensation after DB failure.
8. Define deletion/retention for rejected reports, deleted reports, orphan pending uploads and expired screenshots. Use bounded batches and audit-safe metadata.
9. Return content using the stored server-detected MIME type, `Content-Disposition` and no-sniff/cache headers suitable for private child/user data.
10. Coordinate BACKEND-TEST-025 validation: enforce encoded/decoded size before excessive allocation where possible, parse image structure safely and reject polyglot/truncated content according to an approved image library or strict parser.

## Failure-mode matrix

- anonymous request knows a valid attachment route/key;
- authenticated user requests another user's screenshot;
- reporter and admin each request an existing screenshot;
- bug row exists but object is missing;
- object exists but DB insert fails;
- DB row commits but finalization fails;
- duplicate client retry uploads the same report operation;
- two replicas handle upload/read;
- redeploy/restart occurs between upload and read;
- malicious filename/path traversal input reaches storage adapter;
- oversized, invalid, truncated or MIME-mismatched data;
- cleanup runs concurrently with a download or finalization.

## Required tests

### Authorization/HTTP

- true anonymous request cannot read screenshot;
- authenticated non-owner cannot read screenshot;
- reporter succeeds only for own report;
- exact admin role/policy succeeds;
- static `/uploads/screenshots/*` path cannot serve bytes;
- response headers and safe missing-object behavior are exact.

### Storage/transaction

- storage keys are opaque and contain no raw user identifier;
- DB failure after upload deletes or leaves a recoverable bounded pending object;
- upload/finalize failure creates no completed bug attachment reference;
- retry does not create duplicate finalized objects;
- two service instances can read the same durable object;
- local provider is rejected outside Development/Test when durable storage is required;
- retention deletes only eligible orphan/expired objects and is cancellation-safe.

### Existing linked coverage

- all BACKEND-TEST-025 field/image boundary and compensation tests;
- all bug metadata authorization tests;
- provider-specific integration test when production object storage is selected.

Use `X-Test-Anonymous: true` for real anonymous tests.

## Validation

```text
dotnet test tests/MathLearning.Tests/MathLearning.Tests.csproj -c Release --filter "FullyQualifiedName~BugScreenshot|FullyQualifiedName~BugReport|FullyQualifiedName~BugEndpointAuthorization"
dotnet build MathLearning.slnx -c Release
dotnet ef migrations has-pending-model-changes --project src/MathLearning.Infrastructure/MathLearning.Infrastructure.csproj --startup-project src/MathLearning.Api/MathLearning.Api.csproj --context ApiDbContext
```

Also run one real durable-provider upload/read/delete integration or record `Needs provider validation` with exact missing credentials/environment owner. No provider success claim from an in-memory fake.

## Owned paths

- bug screenshot attachment DTO/entity/storage key and private read endpoint;
- screenshot static-serving denial/removal;
- screenshot provider selection/configuration/readiness;
- linked storage lifecycle and focused tests;
- API inventory/mobile contract update for attachment access.

## Avoid paths / non-goals

- Blazor Admin UI/pages;
- profile-photo avatar migration/deprecation;
- general bug-report workflow/status redesign;
- public signed URLs with long lifetime or bearer URLs persisted in DB;
- logging screenshot contents, raw base64, storage credentials or raw keys;
- broad storage framework refactor unrelated to screenshots.

## Stop / handoff conditions

Stop and create an exact provider/operations handoff if no approved durable provider/configuration exists. Keep production screenshot capability disabled/fail-closed rather than shipping local-disk fallback. Stop and split if more than 10 runtime/test files are required beyond migration/evidence/docs.

## Completion gate

Do not mark Done when anonymous/static access remains possible, another user can read the attachment, production durability is unproven, compensation/retention tests are missing, provider proof is queued/red, or mobile/API docs still advertise a public URL. Done requires executable proof, verified main delivery and synchronized queue/evidence.

Evidence: `.ai/runs/<date>-BACKEND-API-DB-016-evidence.md`