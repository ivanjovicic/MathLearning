---
name: mathlearning-postgres-integrity
description: Review MathLearning backend PostgreSQL EF Core migrations, transaction/constraint races, Redis projections, Hangfire jobs and idempotent persistence.
---
# Database and worker reliability
1. Read `AGENTS.md`, `DATABASE_CONTEXTS.md`, entity/DbContext, migration snapshot, owning service and nearest PostgreSQL test.
2. Validate unique keys, FK/cascade, indexes, model snapshot, compatibility of running API/admin/worker versions and forward-only migration preconditions.
3. Prove settlement idempotency, transaction/outbox boundary, concurrency, retry/cancellation and isolated-user persistence in PostgreSQL. EF InMemory cannot prove real constraints.
4. Redis/leaderboard entries are projections, not primary account economy authority. Hangfire retry must preserve operation ID, bounded concurrency and restart behavior.
5. Test empty bootstrap and applicable upgrade path on disposable PostgreSQL, not production Neon. Never run auto-apply, truncate, destructive seed or migration against a customer DB.
6. Use guarded commands in `.ai/VALIDATION_SELECTOR.md`; record actual passes/failures/skips and exact CI SHA, update only mapped durable docs.
**Stop:** production DDL, identity/key migration without rollback, unclear ownership or unavailable isolated provider tests.
