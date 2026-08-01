-- =========================================================
-- AI Dashboard Builder - Business Schema (IT Helpdesk / Ticketing)
-- Multi-tenant pattern: every table carries tenant_id, matching
-- the composite (tenant_id, id) PK/FK convention used in
-- 01_platform_schema.sql
-- =========================================================

CREATE TABLE organizations (
    tenant_id SMALLINT NOT NULL DEFAULT 1,
    id SERIAL NOT NULL,

    name VARCHAR(150) NOT NULL,
    industry VARCHAR(100),

    created_at TIMESTAMPTZ DEFAULT CURRENT_TIMESTAMP,

    PRIMARY KEY (tenant_id, id)
);

CREATE TABLE categories (
    tenant_id SMALLINT NOT NULL DEFAULT 1,
    id SERIAL NOT NULL,

    name VARCHAR(100) NOT NULL,

    PRIMARY KEY (tenant_id, id),
    UNIQUE (tenant_id, name)
);

CREATE TABLE agents (
    tenant_id SMALLINT NOT NULL DEFAULT 1,
    id SERIAL NOT NULL,

    name VARCHAR(150) NOT NULL,
    email VARCHAR(150) NOT NULL,
    team VARCHAR(100),
    is_active BOOLEAN DEFAULT TRUE,

    hired_at TIMESTAMPTZ,
    created_at TIMESTAMPTZ DEFAULT CURRENT_TIMESTAMP,

    PRIMARY KEY (tenant_id, id),
    UNIQUE (tenant_id, email)
);

CREATE TABLE tickets (
    tenant_id SMALLINT NOT NULL DEFAULT 1,
    id SERIAL NOT NULL,

    organization_id INT NOT NULL,
    agent_id INT,
    category_id INT NOT NULL,

    subject VARCHAR(255) NOT NULL,
    priority VARCHAR(20) NOT NULL,
    status VARCHAR(30) NOT NULL,
    channel VARCHAR(20) NOT NULL,

    sla_breached BOOLEAN DEFAULT FALSE,
    satisfaction_score INT,

    created_at TIMESTAMPTZ NOT NULL,
    resolved_at TIMESTAMPTZ,
    updated_at TIMESTAMPTZ NOT NULL,

    PRIMARY KEY (tenant_id, id),

    FOREIGN KEY (tenant_id, organization_id)
        REFERENCES organizations (tenant_id, id),

    FOREIGN KEY (tenant_id, agent_id)
        REFERENCES agents (tenant_id, id),

    FOREIGN KEY (tenant_id, category_id)
        REFERENCES categories (tenant_id, id)
);

CREATE TABLE ticket_comments (
    tenant_id SMALLINT NOT NULL DEFAULT 1,
    id SERIAL NOT NULL,

    ticket_id INT NOT NULL,
    agent_id INT,

    comment_text TEXT,
    is_internal BOOLEAN DEFAULT FALSE,

    created_at TIMESTAMPTZ NOT NULL,

    PRIMARY KEY (tenant_id, id),

    FOREIGN KEY (tenant_id, ticket_id)
        REFERENCES tickets (tenant_id, id),

    FOREIGN KEY (tenant_id, agent_id)
        REFERENCES agents (tenant_id, id)
);

-- ---------------------------------------------------------
-- Indexes (for dashboard query performance at 50K+ rows)
-- ---------------------------------------------------------
CREATE INDEX idx_tickets_tenant_org ON tickets(tenant_id, organization_id);
CREATE INDEX idx_tickets_tenant_priority ON tickets(tenant_id, priority);
CREATE INDEX idx_tickets_tenant_status ON tickets(tenant_id, status);
CREATE INDEX idx_tickets_tenant_created_at ON tickets(tenant_id, created_at);
CREATE INDEX idx_tickets_tenant_agent ON tickets(tenant_id, agent_id);
CREATE INDEX idx_tickets_tenant_category ON tickets(tenant_id, category_id);
CREATE INDEX idx_comments_tenant_ticket ON ticket_comments(tenant_id, ticket_id);