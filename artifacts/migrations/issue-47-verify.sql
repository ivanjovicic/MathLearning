-- Issue #47 verification queries (read-only).
-- Usage: psql "<url>" -X -v ON_ERROR_STOP=1 -f artifacts/migrations/issue-47-verify.sql
\pset footer off
SELECT current_setting('server_version') AS server_version;
SELECT count(*) AS history_rows, max("MigrationId") AS last_migration,
       md5(string_agg("MigrationId"||':'||"ProductVersion", ',' ORDER BY "MigrationId")) AS history_md5
FROM "__EFMigrationsHistory";
SELECT md5(string_agg("MigrationId"||':'||"ProductVersion", ',' ORDER BY "MigrationId")) AS history_md5_excl_repair
FROM "__EFMigrationsHistory" WHERE "MigrationId" <> '20261101185317_RepairUserAnswerAuditXpSchema';
SELECT "MigrationId", "ProductVersion" FROM "__EFMigrationsHistory" ORDER BY "MigrationId" DESC LIMIT 2;
SELECT pg_relation_filenode('public."UserAnswerAudits"') AS relfilenode,
       (SELECT count(*) FROM public."UserAnswerAudits") AS row_count;
SELECT a.attnum, a.attname, format_type(a.atttypid, a.atttypmod) AS type, a.attnotnull AS not_null,
       pg_get_expr(d.adbin, d.adrelid) AS default_expr, a.atthasmissing
FROM pg_attribute a LEFT JOIN pg_attrdef d ON d.adrelid = a.attrelid AND d.adnum = a.attnum
WHERE a.attrelid = 'public."UserAnswerAudits"'::regclass AND a.attnum > 0 AND NOT a.attisdropped
ORDER BY a.attnum;
SELECT c.relname AS index_name, i.indisunique, i.indisvalid, i.indisready,
       pg_get_indexdef(i.indexrelid) AS definition
FROM pg_index i JOIN pg_class c ON c.oid = i.indexrelid
WHERE i.indrelid = 'public."UserAnswerAudits"'::regclass ORDER BY c.relname;
