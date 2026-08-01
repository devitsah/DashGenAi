-- Run this in pgAdmin BEFORE restarting the backend.
-- It clears all stale RAG data so SchemaPreWarmService rebuilds
-- per-table chunks (one embedding per table) on next startup.

-- 1. Clear schema chunks (was 1 giant cluster, will become N per-table chunks)
DELETE FROM schema_chunks WHERE tenant_id = 1;

-- 2. Clear relationship chunks (will be rebuilt with human-readable join text)
DELETE FROM relationship_chunks WHERE tenant_id = 1;

-- 3. Clear metadata catalog (will be rebuilt with JSON array sample_values)
DELETE FROM metadata_catalog WHERE tenant_id = 1;

-- Verify all cleared
SELECT 'schema_chunks'    AS tbl, COUNT(*) FROM schema_chunks    WHERE tenant_id = 1
UNION ALL
SELECT 'relationship_chunks',     COUNT(*) FROM relationship_chunks WHERE tenant_id = 1
UNION ALL
SELECT 'metadata_catalog',        COUNT(*) FROM metadata_catalog   WHERE tenant_id = 1;
-- Expected: all 0 rows
