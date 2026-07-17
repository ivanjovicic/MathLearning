# BACKEND-API-DB-021 — Durable private screenshot provider and migration decision

Prompt contract: v2
Prompt admission: v3
Repository: ivanjovicic/MathLearning
Prompt ID: BACKEND-API-DB-021
Queue: docs/prompt_queues/backend_cross_repo_current_main_2026_07_17.md
Run lane: investigation
Token budget: medium
Timebox: 30 minutes

Problem evidence:
- Current `LocalScreenshotStorageService` stores files under the API process directory, which is not a proven durable/shared store across deploys or replicas.
- Repository search found no existing S3/blob/object-storage abstraction or deployment contract to reuse.
- `BACKEND-API-DB-020` intentionally stabilizes private authorization/opaque-key semantics but does not own vendor/deployment durability.

Deduplication check:
- `020` owns privacy/read contract and must complete first.
- Avatar and cosmetics object/data ownership are separate domains and provide no verified reusable screenshot provider.
- No active backend prompt currently owns bug screenshot provider selection, migration, retention and multi-replica durability.

Priority rationale: P1 durability because process-local attachments may disappear on redeploy, diverge across replicas or block horizontal scaling even after privacy is fixed.

Dependencies/collisions:
- Ready only after `BACKEND-API-DB-020` is main-verified.
- Do not change the authorized read route or DTO contract except for provider-neutral metadata required by the selected design.
- Deployment secrets, bucket/container lifecycle and migration execution require an explicit operator/environment owner.

Owner boundary:
- This investigation owns provider requirements, deployment topology, migration/rollback plan and one bounded implementation handoff.
- It does not implement mobile UI, avatar storage, generic media platform or public CDN delivery.
- Runtime implementation begins only after the provider and operator path are proven.

Queue placement: dependent durability phase immediately after the private attachment contract.

Task: Produce an evidence-backed decision and implementation packet for durable private screenshot storage that survives redeploy, supports multiple API replicas and preserves the authorized route from `020`.

Source of truth:
- `src/MathLearning.Infrastructure/Services/LocalScreenshotStorageService.cs`
- screenshot service interface and registration
- deployment manifests/configuration currently used by the API
- bug attachment retention/lifecycle from `BACKEND-API-DB-020`
- existing secret/configuration conventions and health/readiness endpoints
- current provider pricing/limits only when verified from authoritative provider documentation

Interpretation before work: Build `provider option -> durability -> private access -> atomic upload/delete -> multi-replica -> local migration -> retention -> backup -> cost -> operator steps -> rollback` before recommending implementation.

Ambiguity rule: Do not add an SDK, bucket name or secret convention by guess. Missing deployment/provider authority yields one named operator handoff, not a speculative cloud implementation.

Risk/ownership model:
- Objects remain private; the API authorized route mediates reads.
- Database stores provider-neutral opaque key plus only required validated metadata.
- Upload/delete are idempotent and compensatable; retention and orphan cleanup are bounded.
- Readiness reports configuration/access failures without leaking secrets or object names.
- Migration never deletes local originals before remote verification and rollback checkpoint.

Failure-mode matrix:
- API deploys with missing/invalid credentials or inaccessible bucket/container.
- Two replicas upload/read/delete the same logical attachment.
- Remote upload succeeds but database commit fails, or database points to a missing object.
- Local-to-remote migration is interrupted and restarted.
- Provider timeout, throttling or partial outage affects authorized reads.
- Retention cleanup races with an active report/read.

Execution packet:
- Initial reads: storage/service registration, deploy config, health/readiness, `020` evidence and one provider-authoritative source per candidate; maximum 12 sources.
- Search budget: maximum 4 searches for existing provider config, deployment platform persistence and authoritative SDK/lifecycle limits.
- First hypothesis/falsifier: process-local storage is not durable/shared; falsify only with deployed persistent-volume topology and multi-replica proof.
- Expected changed files: investigation/ADR, one exact implementation prompt, queue/evidence and optional provider-neutral interface adjustment; maximum 5 paths.
- Focused proof: configuration/readiness prototype or provider emulator contract, migration dry-run design and cost/retention comparison.
- Stop trigger: no operator/provider authority, unavailable credentials or a second media domain.

Owned paths:
- Durable screenshot provider ADR/decision.
- Deployment/configuration/readiness requirements.
- Local-object migration, verification and rollback plan.
- One bounded follow-up implementation prompt.

Avoid paths:
- Public CDN or signed public URLs.
- Mobile/admin UI.
- Avatar/media platform redesign.
- Secret values in repository or evidence.
- Deleting local objects during investigation.

Documentation impact: update architecture/operations and bug attachment docs only after a provider decision; otherwise record the exact operator handoff and unverified assumptions.

Acceptance criteria:
1. One provider/topology is selected from current deployment facts, or the decision is explicitly blocked by a named missing authority.
2. Private access, multi-replica behavior, upload/delete idempotency, readiness and retention contracts are specified.
3. Restartable local-to-remote migration includes verification, metrics, rollback and no early deletion.
4. Secret/configuration ownership is explicit and no credentials are committed/logged.
5. Cost/limits are cited from authoritative current sources when they affect the choice.
6. Output is one bounded implementation prompt, not a generic “support cloud storage” backlog item.

Proof required:
- Deployment persistence/topology evidence.
- Provider-private-object and emulator/contract test plan.
- Failure matrix with retry/timeout and compensation decisions.
- Migration sample inventory/checksum/restart algorithm.
- Operator checklist and rollback checkpoint.

Validation:
```powershell
python scripts/check_documentation_health.py --context src/MathLearning.Infrastructure/Services/LocalScreenshotStorageService.cs
python scripts/validate_agent_prompt.py docs/prompt_queues/backend_cross_repo/BACKEND-API-DB-021.md
python scripts/validate_agent_evidence.py --changed-from <base-sha> --verify-git
```

Completion gate: Investigation is complete only with an authoritative provider/operator decision and one executable handoff. It cannot claim durable runtime behavior or Done implementation.

Stop conditions:
- Stop without deployment/provider authority and name the exact owner/action.
- Stop before broad media storage or secret-management redesign.
- Stop at five changed paths, four searches or the 30-minute limit.

Evidence: .ai/runs/<yyyy-mm-dd>-BACKEND-API-DB-021-evidence.md
