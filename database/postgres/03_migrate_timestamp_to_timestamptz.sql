-- Migration: TIMESTAMP -> TIMESTAMPTZ
-- Run once against an existing database. No USING clause needed —
-- Postgres casts TIMESTAMP to TIMESTAMPTZ losslessly using the server timezone.
-- The Docker postgres image defaults to UTC, so existing values are preserved as-is.

ALTER TABLE users
    ALTER COLUMN created_at TYPE TIMESTAMPTZ,
    ALTER COLUMN updated_at TYPE TIMESTAMPTZ;

ALTER TABLE user_sessions
    ALTER COLUMN expires_at TYPE TIMESTAMPTZ,
    ALTER COLUMN created_at TYPE TIMESTAMPTZ,
    ALTER COLUMN updated_at TYPE TIMESTAMPTZ;

ALTER TABLE dashboards
    ALTER COLUMN created_at TYPE TIMESTAMPTZ,
    ALTER COLUMN updated_at TYPE TIMESTAMPTZ;

ALTER TABLE prompts
    ALTER COLUMN created_at TYPE TIMESTAMPTZ,
    ALTER COLUMN updated_at TYPE TIMESTAMPTZ;

ALTER TABLE widgets
    ALTER COLUMN created_at TYPE TIMESTAMPTZ,
    ALTER COLUMN updated_at TYPE TIMESTAMPTZ;

ALTER TABLE queries
    ALTER COLUMN created_at TYPE TIMESTAMPTZ,
    ALTER COLUMN updated_at TYPE TIMESTAMPTZ;

ALTER TABLE audit_logs
    ALTER COLUMN created_at TYPE TIMESTAMPTZ,
    ALTER COLUMN updated_at TYPE TIMESTAMPTZ;

ALTER TABLE organizations
    ALTER COLUMN created_at TYPE TIMESTAMPTZ;

ALTER TABLE agents
    ALTER COLUMN hired_at   TYPE TIMESTAMPTZ,
    ALTER COLUMN created_at TYPE TIMESTAMPTZ;

ALTER TABLE tickets
    ALTER COLUMN created_at  TYPE TIMESTAMPTZ,
    ALTER COLUMN resolved_at TYPE TIMESTAMPTZ,
    ALTER COLUMN updated_at  TYPE TIMESTAMPTZ;

ALTER TABLE ticket_comments
    ALTER COLUMN created_at TYPE TIMESTAMPTZ;
