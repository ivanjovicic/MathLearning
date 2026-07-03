# BACKEND-TEST-048 — PostgreSQL index-bloat metric validity

Priority: P1  
Status: Prompt-ready  
Run mode: PostgreSQL investigation + query correction + integration tests

## Problem

`IndexMaintenanceService.DetectBloatedIndexesAsync` currently calculates:

```sql
pg_relation_size(indexrelid) - pg_relation_size(indexrelid, 'main')
```

`pg_relation_size(regclass)` reports the main fork by default, so subtracting the explicitly named `main` fork is expected to produce zero in normal cases. The reported `BloatPercentage` may therefore remain zero, meaning the `> 30` rebuild threshold never selects an index.

This is a static finding, not yet PostgreSQL execution proof.

## Risks

- scheduled/manual rebuild can report success while never selecting genuinely bloated indexes;
- admin stats can show misleading `HEALTHY` values;
- tests using fake reports do not validate the SQL metric;
- an incorrect replacement formula could cause unnecessary or dangerous rebuilds.

## Required investigation

1. Execute the current query on PostgreSQL 16 with known table/index fixtures.
2. Confirm the behavior of both `pg_relation_size` overloads.
3. Choose a defensible metric:
   - `pgstattuple`/`pgstatindex` extension where operationally acceptable;
   - a documented catalog/statistics estimate;
   - or remove automatic rebuild decisions and expose only verified usage/size data.
4. Define required privileges and extension availability for Render/Fly/Neon deployment targets.
5. Do not use scan count as a proxy for physical bloat.
6. Make thresholds configurable and conservative.

## Required tests

- fresh compact index does not cross threshold;
- fixture with measurable dead/fragmented pages produces a higher metric;
- current and corrected query results are compared explicitly;
- zero-sized/small indexes do not divide by zero;
- partitioned, expression, partial and schema-qualified indexes are handled safely;
- unavailable extension or insufficient privilege yields a safe diagnostic, not a false healthy result;
- rebuild selection uses the corrected metric exactly once;
- stats endpoint and CLI display the same read-only metric;
- no automatic rebuild occurs when metric confidence is unknown.

## Required validation

```text
dotnet test tests/MathLearning.Tests/MathLearning.Tests.csproj --filter "IndexMaintenance|IndexBloat"
dotnet build MathLearning.slnx -c Release
```

Run against the PostgreSQL 16 CI service with an isolated database and retain SQL/query evidence in the run log.

## Completion rule

Do not mark Done from a rewritten SQL string alone. Completion requires PostgreSQL fixture evidence showing that the metric distinguishes compact and intentionally degraded indexes, plus safe behavior when the required metric source is unavailable.
