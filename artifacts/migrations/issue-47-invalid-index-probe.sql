-- Issue #47: can a same-name but INVALID / wrongly-defined critical index satisfy a name-only check?
-- Runs inside a transaction that is ROLLED BACK.
\set ON_ERROR_STOP 1
BEGIN;
DROP INDEX "UX_UserAnswerAudits_FirstCorrect_PerQuestion";
-- wrong definition: non-unique, no predicate, same name
CREATE INDEX "UX_UserAnswerAudits_FirstCorrect_PerQuestion" ON public."UserAnswerAudits" ("UserId");
SELECT 'name-only (pg_indexes) check sees it' AS probe, count(*) FROM pg_indexes
 WHERE schemaname = current_schema() AND indexname = 'UX_UserAnswerAudits_FirstCorrect_PerQuestion';
SELECT 'semantic check' AS probe, i.indisunique, i.indisvalid, pg_get_expr(i.indpred, i.indrelid) AS predicate
FROM pg_index i JOIN pg_class c ON c.oid = i.indexrelid WHERE c.relname = 'UX_UserAnswerAudits_FirstCorrect_PerQuestion';
-- (an INVALID index cannot be forced without superuser; covered by the DB-gated test that
--  produces one via a failed CREATE UNIQUE INDEX CONCURRENTLY on duplicate data.)
ROLLBACK;
