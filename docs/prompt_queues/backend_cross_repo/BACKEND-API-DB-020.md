# BACKEND-API-DB-020 — Private authorized bug screenshot contract

Prompt contract: v2
Prompt admission: v3
Repository: ivanjovicic/MathLearning
Prompt ID: BACKEND-API-DB-020
Queue: docs/prompt_queues/backend_cross_repo_current_main_2026_07_17.md
Run lane: known-fix
Token budget: medium
Timebox: 30 minutes

Problem evidence:
- `BACKEND-API-DB-016` only blocks anonymous static-file access; its evidence remains 70% and explicitly leaves local storage plus persisted screenshot URLs unresolved.
- `LocalScreenshotStorageService` writes under the API process directory and returns `/uploads/screenshots/<file>`, while `BugReportService` persists/projects that value as `ScreenshotUrl`.
- Current bug endpoints authorize reports and admin reads, but there is no reporter/admin-authorized byte-stream endpoint bound to bug ownership.

Deduplication check:
- `BACKEND-API-DB-016` is partial historical work and is superseded by this narrower residual rather than reopened.
- `BACKEND-CRIT-004` owns avatar upload/static serving, not bug attachment authorization.
- `BACKEND-TEST-025` owns general bug-report validation/compensation tests and remains a supporting test owner, not a second storage implementation.

Priority rationale: P0/P1 privacy because persisted public-path semantics can be accidentally re-exposed and DTOs disclose an implementation URL rather than an authorized resource contract.

Dependencies/collisions:
- Preserve the existing anonymous deny rule from `016`; do not use it as the only privacy control.
- Keep durable provider selection/migration outside this prompt in `BACKEND-API-DB-021`.
- Coordinate DTO/route compatibility with Flutter only if the mobile app consumes screenshot URLs; do not invent a client dependency.

Owner boundary:
- Bug-report attachment contract owns opaque storage key creation, reporter/admin authorization, byte streaming and delete/compensation lifecycle.
- Storage provider durability/topology is consumed here but implemented by `021` after the contract is stable.
- Avatar/photo storage, general bug fields and admin UI rendering are excluded.

Queue placement: immediate privacy residual after partial `BACKEND-API-DB-016`, before provider/deployment durability work.

Task: Stop treating bug screenshots as public URLs. Persist an opaque private storage reference and expose bytes only through an authenticated route that proves reporter ownership or admin policy.

Source of truth:
- `src/MathLearning.Api/Endpoints/BugEndpoints.cs`
- `src/MathLearning.Infrastructure/Services/BugReportService.cs`
- `src/MathLearning.Infrastructure/Services/LocalScreenshotStorageService.cs`
- `src/MathLearning.Application/Services/IBugReportService.cs`
- `src/MathLearning.Application/Services/IScreenshotStorageService.cs`
- bug DTO/entity mapping and `BugEndpointAuthorizationTests`
- `.ai/runs/2026-07-16-BACKEND-API-DB-016-evidence.md`

Interpretation before work: Trace `upload -> validated bytes -> opaque key -> bug persistence -> DTO projection -> reporter/admin stream -> delete/report failure compensation` and list every place that currently assumes a URL.

Ambiguity rule: Do not sign or expose a raw filesystem path and do not claim durable provider behavior. If DTO compatibility requires keeping a field named `ScreenshotUrl`, its value must be an authorized API route, never the storage key or static path.

Risk/ownership model:
- Authenticated bearer derives reporter identity; admin policy may read any bug attachment.
- Storage keys are unguessable opaque identifiers and never accepted as sufficient authorization.
- Route loads the bug first, proves reporter/admin access, then opens the referenced object.
- Content type and length come from validated stored metadata, not user-controlled file names.
- Create/report failure deletes the uploaded object; report deletion/retention defines attachment cleanup.

Failure-mode matrix:
- Anonymous, another learner and non-admin request a known screenshot route/key.
- Reporter and admin request an existing valid attachment.
- Bug exists without attachment, storage object is missing or storage read fails.
- Upload succeeds but database save fails; object is compensated.
- Duplicate/retried report creates no orphan or cross-linked object under the owning request policy.
- Malicious key/path traversal, invalid content type and oversized payload are rejected.

Execution packet:
- Initial reads: endpoint, service, storage interface/provider, entity/DTO and focused tests; maximum 10 files.
- Search budget: maximum 3 searches for `ScreenshotUrl`, storage interface calls and bug authorization tests.
- First hypothesis/falsifier: privacy relies on static middleware while DTO/service retain public URL semantics; falsify with authorized stream and forbidden-access fixtures.
- Expected changed files: endpoint, storage/service contract, DTO/entity mapping and focused tests/docs; maximum 6 paths plus evidence.
- Focused proof: reporter/admin allow, anonymous/cross-user deny, missing object and DB-failure compensation.
- Stop trigger: schema/provider migration, object-store SDK/deployment configuration or mobile UI work moves to `021`/named handoff.

Owned paths:
- Bug screenshot opaque-reference contract.
- Authorized bug attachment streaming endpoint.
- Bug service/storage compensation and focused authorization tests.
- Endpoint/mobile contract docs when response shape changes.

Avoid paths:
- Avatar upload/static serving.
- General bug-report field validation unrelated to attachments.
- Object-storage vendor/deployment migration (`BACKEND-API-DB-021`).
- Returning storage keys or filesystem paths to clients.
- Broad admin UI changes.

Documentation impact: update `docs/API_ENDPOINT_INVENTORY.md` and the bug-report contract/readme; record mobile compatibility or a specific no-client-impact reason.

Acceptance criteria:
1. Bug DTOs no longer expose a raw static/storage URL; any retained URL-shaped field points only to the authorized API route.
2. Reporter and admin can stream valid bytes; anonymous, cross-user and wrong-role callers cannot infer existence or read content.
3. Storage key/path traversal and content metadata are validated and never become authorization.
4. Upload followed by failed bug persistence leaves no orphan object.
5. Missing/corrupt storage returns a safe bounded error without leaking paths/provider details.
6. Existing anonymous static deny remains and focused tests prove both middleware and route authorization.

Proof required:
- Real stored-byte endpoint tests for reporter/admin/cross-user/anonymous cases.
- DB save failure injection with storage delete assertion.
- Key/path traversal and missing-object fixtures.
- DTO serialization assertion excluding raw storage key/static path.
- Exact route/auth inventory update.

Validation:
```powershell
python scripts/run_guarded.py --timeout-seconds 180 -- dotnet test tests/MathLearning.Tests/MathLearning.Tests.csproj --filter FullyQualifiedName~BugScreenshot
python scripts/run_guarded.py --timeout-seconds 180 -- dotnet build src/MathLearning.Api/MathLearning.Api.csproj -c Release --no-restore
python scripts/check_documentation_health.py --context src/MathLearning.Api/Endpoints/BugEndpoints.cs
python scripts/validate_agent_evidence.py --changed-from <base-sha> --verify-git
```

Completion gate: No Done from returning 404 under `/uploads` alone. Done requires opaque references, authorized streaming, compensation tests, safe errors, contract docs and verified main delivery.

Stop conditions:
- Stop before selecting/configuring a cloud provider or rewriting unrelated bug-report storage.
- Stop when a migration/provider owner is required; hand it to `BACKEND-API-DB-021`.
- Stop at six changed paths, a second subsystem or the 30-minute limit.

Evidence: .ai/runs/<yyyy-mm-dd>-BACKEND-API-DB-020-evidence.md
