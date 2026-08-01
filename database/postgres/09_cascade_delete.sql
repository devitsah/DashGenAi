-- Migration: add ON DELETE CASCADE to FK constraints that block dashboard deletion
-- Run this once against your existing database.

-- 1. queries → dashboards
ALTER TABLE queries
    DROP CONSTRAINT IF EXISTS queries_tenant_id_dashboard_id_fkey,
    ADD CONSTRAINT queries_tenant_id_dashboard_id_fkey
        FOREIGN KEY (tenant_id, dashboard_id)
        REFERENCES dashboards (tenant_id, id)
        ON DELETE CASCADE;

-- 2. queries → widgets
ALTER TABLE queries
    DROP CONSTRAINT IF EXISTS queries_tenant_id_widget_id_fkey,
    ADD CONSTRAINT queries_tenant_id_widget_id_fkey
        FOREIGN KEY (tenant_id, widget_id)
        REFERENCES widgets (tenant_id, id)
        ON DELETE CASCADE;

-- 3. queries → prompts
ALTER TABLE queries
    DROP CONSTRAINT IF EXISTS queries_tenant_id_prompt_id_fkey,
    ADD CONSTRAINT queries_tenant_id_prompt_id_fkey
        FOREIGN KEY (tenant_id, prompt_id)
        REFERENCES prompts (tenant_id, id)
        ON DELETE CASCADE;

-- 4. widgets → dashboards
ALTER TABLE widgets
    DROP CONSTRAINT IF EXISTS widgets_tenant_id_dashboard_id_fkey,
    ADD CONSTRAINT widgets_tenant_id_dashboard_id_fkey
        FOREIGN KEY (tenant_id, dashboard_id)
        REFERENCES dashboards (tenant_id, id)
        ON DELETE CASCADE;

-- 5. prompts → dashboards (SET NULL so deleting a dashboard doesn't wipe prompt history)
ALTER TABLE prompts
    DROP CONSTRAINT IF EXISTS prompts_tenant_id_dashboard_id_fkey,
    ADD CONSTRAINT prompts_tenant_id_dashboard_id_fkey
        FOREIGN KEY (tenant_id, dashboard_id)
        REFERENCES dashboards (tenant_id, id)
        ON DELETE SET NULL;
