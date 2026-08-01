CREATE TABLE users (
    tenant_id SMALLINT NOT NULL DEFAULT 1,
    id SERIAL NOT NULL,

    name VARCHAR(100) NOT NULL,
    email VARCHAR(255) NOT NULL,
    password_hash VARCHAR(255) NOT NULL,

    created_at TIMESTAMPTZ DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMPTZ DEFAULT CURRENT_TIMESTAMP,

    PRIMARY KEY (tenant_id, id),
    UNIQUE (tenant_id, email)
);

CREATE TABLE user_sessions (
    tenant_id SMALLINT NOT NULL DEFAULT 1,
    id SERIAL NOT NULL,

    user_id INT NOT NULL,

    token_hash VARCHAR(255) NOT NULL,
    

    expires_at TIMESTAMPTZ NOT NULL,

    created_at TIMESTAMPTZ DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMPTZ DEFAULT CURRENT_TIMESTAMP,

    PRIMARY KEY (tenant_id, id),

    FOREIGN KEY (tenant_id, user_id)
        REFERENCES users (tenant_id, id)
);

CREATE TABLE dashboards (
    tenant_id SMALLINT NOT NULL DEFAULT 1,
    id SERIAL NOT NULL,

    user_id INT NOT NULL,

    name VARCHAR(100) NOT NULL,
    description VARCHAR(500),

    visibility VARCHAR(20),
    is_default BOOLEAN DEFAULT FALSE,
    dashboard_style VARCHAR(50),
    version_no SMALLINT DEFAULT 1,

    -- Overall layout/grid config (widget-level data lives in the widgets table below;
    -- this holds dashboard-wide settings: grid size, theme overrides, filter defaults, etc.)
    definition JSONB NOT NULL DEFAULT '{}'::jsonb,

    created_by INT,
    updated_by INT,

    created_at TIMESTAMPTZ DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMPTZ DEFAULT CURRENT_TIMESTAMP,

    PRIMARY KEY (tenant_id, id),

    FOREIGN KEY (tenant_id, user_id)
        REFERENCES users (tenant_id, id),

    FOREIGN KEY (tenant_id, created_by)
        REFERENCES users (tenant_id, id),

    FOREIGN KEY (tenant_id, updated_by)
        REFERENCES users (tenant_id, id)
);

CREATE TABLE prompts (
    tenant_id SMALLINT NOT NULL DEFAULT 1,
    id SERIAL NOT NULL,

    user_id INT NOT NULL,
    dashboard_id INT,

    -- Links a clarification answer back to the original ambiguous prompt
    -- e.g. "Create a card" (id=5) -> "Show active users" (parent_prompt_id=5)
    parent_prompt_id INT,

    prompt_text TEXT NOT NULL,

    -- pending_clarification | processing | completed | failed
    status VARCHAR(30) NOT NULL DEFAULT 'pending',

    created_by INT,
    updated_by INT,

    created_at TIMESTAMPTZ DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMPTZ DEFAULT CURRENT_TIMESTAMP,

    PRIMARY KEY (tenant_id, id),

    FOREIGN KEY (tenant_id, user_id)
        REFERENCES users (tenant_id, id),

    FOREIGN KEY (tenant_id, dashboard_id)
        REFERENCES dashboards (tenant_id, id),

    FOREIGN KEY (tenant_id, parent_prompt_id)
        REFERENCES prompts (tenant_id, id),

    FOREIGN KEY (tenant_id, created_by)
        REFERENCES users (tenant_id, id),

    FOREIGN KEY (tenant_id, updated_by)
        REFERENCES users (tenant_id, id)
);

CREATE TABLE widgets (
    tenant_id SMALLINT NOT NULL DEFAULT 1,
    id SERIAL NOT NULL,

    dashboard_id INT NOT NULL,

    title VARCHAR(100) NOT NULL,
    widget_type VARCHAR(50) NOT NULL,  -- kpi_card | table | pie_chart | bar_chart | line_chart (Phase 2 adds more)

    width SMALLINT NOT NULL,
    height SMALLINT NOT NULL,
    position_x SMALLINT NOT NULL,
    position_y SMALLINT NOT NULL,

    refresh_interval INT,

    -- Phase 2: explicit data source label so the designer UI doesn't need to parse the
    -- linked query's SQL just to show/filter widgets by source (e.g. 'postgresql', later others)
    data_source VARCHAR(50) DEFAULT 'postgresql',

    -- visualization-specific settings (axis labels, colors, thresholds, etc.)
    config_json JSONB NOT NULL DEFAULT '{}'::jsonb,

    created_by INT,
    updated_by INT,

    created_at TIMESTAMPTZ DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMPTZ DEFAULT CURRENT_TIMESTAMP,

    PRIMARY KEY (tenant_id, id),

    FOREIGN KEY (tenant_id, dashboard_id)
        REFERENCES dashboards (tenant_id, id),

    FOREIGN KEY (tenant_id, created_by)
        REFERENCES users (tenant_id, id),

    FOREIGN KEY (tenant_id, updated_by)
        REFERENCES users (tenant_id, id)
);

CREATE TABLE queries (
    tenant_id SMALLINT NOT NULL DEFAULT 1,
    id SERIAL NOT NULL,

    dashboard_id INT NOT NULL,
    widget_id INT NOT NULL,   -- traces the generated SQL to the exact widget, not just the dashboard
    prompt_id INT NOT NULL,

    sql_query TEXT NOT NULL,

    created_by INT,
    updated_by INT,

    created_at TIMESTAMPTZ DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMPTZ DEFAULT CURRENT_TIMESTAMP,

    PRIMARY KEY (tenant_id, id),

    FOREIGN KEY (tenant_id, dashboard_id)
        REFERENCES dashboards (tenant_id, id),

    FOREIGN KEY (tenant_id, widget_id)
        REFERENCES widgets (tenant_id, id),

    FOREIGN KEY (tenant_id, prompt_id)
        REFERENCES prompts (tenant_id, id),

    FOREIGN KEY (tenant_id, created_by)
        REFERENCES users (tenant_id, id),

    FOREIGN KEY (tenant_id, updated_by)
        REFERENCES users (tenant_id, id)
);

CREATE TABLE audit_logs (
    tenant_id SMALLINT NOT NULL DEFAULT 1,
    id SERIAL NOT NULL,

    user_id INT NOT NULL,

    entity_type VARCHAR(50) NOT NULL,
    entity_id INT NOT NULL,

    action VARCHAR(50) NOT NULL,

    old_value JSONB,
    new_value JSONB,

    created_by INT,
    updated_by INT,

    created_at TIMESTAMPTZ DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMPTZ DEFAULT CURRENT_TIMESTAMP,

    PRIMARY KEY (tenant_id, id),

    FOREIGN KEY (tenant_id, user_id)
        REFERENCES users (tenant_id, id),

    FOREIGN KEY (tenant_id, created_by)
        REFERENCES users (tenant_id, id),

    FOREIGN KEY (tenant_id, updated_by)
        REFERENCES users (tenant_id, id)
);

-- ---------------------------------------------------------
-- Indexes for Phase 1 access patterns
-- ---------------------------------------------------------
CREATE INDEX idx_dashboards_tenant_user ON dashboards(tenant_id, user_id);
CREATE INDEX idx_prompts_tenant_user ON prompts(tenant_id, user_id);
CREATE INDEX idx_prompts_tenant_parent ON prompts(tenant_id, parent_prompt_id);
CREATE INDEX idx_widgets_tenant_dashboard ON widgets(tenant_id, dashboard_id);
CREATE INDEX idx_queries_tenant_widget ON queries(tenant_id, widget_id);
CREATE INDEX idx_queries_tenant_prompt ON queries(tenant_id, prompt_id);
CREATE INDEX idx_audit_logs_tenant_entity ON audit_logs(tenant_id, entity_type, entity_id);

-- Phase 2: enforce only one default dashboard per user (per tenant).
-- Partial unique index — only applies to rows where is_default = TRUE.
CREATE UNIQUE INDEX uq_dashboards_one_default_per_user
    ON dashboards(tenant_id, user_id)
    WHERE is_default = TRUE;