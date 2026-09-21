# direct-user-request Evidence

Evidence format: v2
Prompt ID: direct-user-request
Queue: direct-user-request
Agent/tool: Codex
Model provider: unknown-not-exposed
Model name/id: unknown-not-exposed
Client/IDE: API
Run mode: known-fix
Token budget: medium
Started at UTC: 2026-09-09T13:20:00Z
Completed at UTC: 2026-09-09T14:07:00Z
Elapsed time: 47 minutes
Relevant prior mistakes read: none
How this run avoids prior mistakes: Kept the confirmed cosmetics race in the backend owner and treated missing routes as contract mismatches.
Owner/hypothesis: CosmeticPlatformService default initialization races between parallel avatar/inventory loads; PostgreSQL unique violation is the expected losing outcome.
Files inspected: 12
Files changed: 2
Searches: 7
Validation runs: 3
Failed retries: 1

## Outcome
- Confirmed Fly correlation `ml-hm4tsxul3w-9` failed on `UX_user_cosmetics_user_item` during default ownership initialization.
- Made duplicate default ownership and concurrent avatar-row creation idempotent for PostgreSQL.
- Confirmed `/api/progress/week-activity` and `/api/seasons/active` are not backend routes; no production mutation was performed.

## Changed paths
- `src/MathLearning.Infrastructure/Services/Cosmetics/CosmeticPlatformService.Helpers.cs`
- `.ai/runs/2026-09-09-cosmetics-api-contract-evidence.md`

## Validation
Validation run: `dotnet build src/MathLearning.Infrastructure/MathLearning.Infrastructure.csproj --no-restore -v minimal` -> pass; `dotnet test tests/MathLearning.Tests --no-restore --filter CosmeticPlatformServiceTests -v minimal` -> fail on two pre-existing catalog setup failures (`CosmeticCatalogRevisionMissing`), with 5/7 tests passing.
Validation not run: no live Fly deploy/restart; production mutation requires explicit authorization.

## Exceptions and learning
Mistakes observed: none
Waste: broad-search - one initial search included generated migrations and was immediately narrowed.
Missed: none
Follow-up: deploy branch and retest avatar load with the recorded correlation ID.
Residual risk: 256 MB Fly machine previously hit OOM and Npgsql connection timeouts; infrastructure sizing/DB health still needs operational action.
Documentation impact: none - this is an implementation/evidence change with no durable contract owner update.
Cross-repo impact: yes - mobile client separately suppresses the known unsupported week-activity request.

## Delivery
State: Needs merge
Branch/PR: `codex/fix-cosmetics-default-ownership-race`
Commit SHA: self
Completion %: 85
