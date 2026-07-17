# Backend Documentation Operating System

Last verified: 2026-07-17  
Owner: `backend-docs-system`

This document defines how durable backend documentation stays current, owned, discoverable and mechanically verifiable. Runtime code, focused tests and executable tooling remain the highest authority.

## Durable and transient documents

A **durable** document describes current architecture, contract, workflow, validation, security, operations or release behavior. Every durable document is registered in [`DOCS_MANIFEST.json`](DOCS_MANIFEST.json).

A **transient** document preserves task or point-in-time evidence. Prompt bodies, queue status, `.ai/runs/**`, dated audits and completed archives are transient unless the manifest explicitly registers them. They never override current code or a registered owner.

## Manifest contract

Each registered document declares:

```text
path
class
owner
purpose
review_days
last_verified
source_globs
impact: required | advisory
```

`last_verified` means claims were checked against their owning code/tooling, not merely touched. `required` means matching source changes need an update, an explicit no-impact reason or a named follow-up. `advisory` documents are read when useful but are not churned for unrelated edits.

## Generated registry

[`DOCS_REGISTRY.md`](DOCS_REGISTRY.md) is generated from the manifest:

```powershell
python scripts/check_documentation_health.py --write-registry
```

Never hand-edit the registry. A manifest edit is incomplete until generated output matches.

## Agent context routing

Before reading broad documentation, map the exact changed or investigated paths:

```powershell
python scripts/check_documentation_health.py --context src/MathLearning.Api/Endpoints/AuthEndpoints.cs
```

Read required owners first, then advisory owners only when they change the decision. Do not open the whole registry or every dated audit.

## Documentation-impact decision

A runtime, test, workflow, schema or configuration change records exactly one:

```text
Documentation impact: updated <paths>
Documentation impact: none - <specific reason>
Documentation impact: follow-up <prompt-or-issue> - <specific reason>
```

The declaration is a review decision, not a replacement for reading mapped owners.

## Mechanical health checks

```powershell
python -m unittest -v scripts/test_check_documentation_health.py
python scripts/check_documentation_health.py --full-links
python scripts/validate_agent_system.py
```

The checks reject:

- invalid or duplicate manifest entries;
- missing registered documents;
- registry drift;
- broken links in registered durable documents;
- durable index links that are not registered;
- unresolved Git conflict markers in durable documents;
- invalid source-path routing metadata.

Conflict markers are release-blocking documentation corruption. Do not choose one side from memory; recover intended ownership from Git history, current tooling and the source-of-truth map.

## Update rule

When a durable rule changes:

1. update the canonical owner first;
2. update validators/tests when mechanically enforceable;
3. update manifest ownership or source mappings only when they changed;
4. regenerate the registry;
5. replace duplicated mechanics elsewhere with a short link;
6. keep audits, prompts and run logs as evidence rather than new policy owners.

## Cross-repository contracts

For mobile-facing API, retry, authentication, session, reward or persistence changes, inspect the current Flutter contract/prompts and record:

```text
Flutter baseline checked: <main SHA>
Flutter owner/prompt: <path or ID>
Backend owner/prompt: <path or ID>
Sync result: updated | no change | blocked with named handoff
```

Never create a second backend prompt when an existing owner already covers the runtime mutation. Add a cross-repo dependency/handoff to that owner instead.
