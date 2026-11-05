-- Issue #47: verify PG fast-default behaviour for ADD COLUMN ... DEFAULT now() on a POPULATED table.
-- Runs entirely inside a transaction that is ROLLED BACK (temp table only; no persistent change).
\set ON_ERROR_STOP 1
BEGIN;
CREATE TEMP TABLE fd_probe(id int NOT NULL);
INSERT INTO fd_probe SELECT g FROM generate_series(1,200000) g;
SELECT 'initial' AS step, pg_relation_filenode('fd_probe') AS relfilenode, pg_size_pretty(pg_relation_size('fd_probe')) AS size;
ALTER TABLE fd_probe ADD COLUMN c_now timestamptz NOT NULL DEFAULT now();
SELECT 'after DEFAULT now() (STABLE)' AS step, pg_relation_filenode('fd_probe') AS relfilenode,
       (SELECT atthasmissing FROM pg_attribute WHERE attrelid='fd_probe'::regclass AND attname='c_now') AS atthasmissing;
ALTER TABLE fd_probe ADD COLUMN c_bool boolean NOT NULL DEFAULT false, ADD COLUMN c_txt varchar(64) NOT NULL DEFAULT 'not_eligible';
SELECT 'after constant defaults' AS step, pg_relation_filenode('fd_probe') AS relfilenode;
SELECT count(DISTINCT c_now) AS distinct_now_values FROM fd_probe;
ALTER TABLE fd_probe ADD COLUMN c_vol timestamptz NOT NULL DEFAULT clock_timestamp();
SELECT 'after DEFAULT clock_timestamp() (VOLATILE, control)' AS step, pg_relation_filenode('fd_probe') AS relfilenode;
SELECT proname, provolatile FROM pg_proc WHERE proname IN ('now','clock_timestamp') ORDER BY 1;
ROLLBACK;
