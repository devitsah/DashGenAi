CREATE EXTENSION IF NOT EXISTS vector;

CREATE TABLE schema_chunks (
    tenant_id   SMALLINT NOT NULL DEFAULT 1,
    chunk_name  VARCHAR(100) NOT NULL,
    tables      TEXT[] NOT NULL,
    chunk_text  TEXT NOT NULL,
    purpose     TEXT NULL,
    schema_hash VARCHAR(64) NOT NULL,
    embedding   vector(768) NOT NULL,
    updated_at  TIMESTAMPTZ DEFAULT CURRENT_TIMESTAMP,

    PRIMARY KEY (tenant_id, chunk_name)
);

CREATE INDEX idx_schema_chunks_embedding
    ON schema_chunks USING ivfflat (embedding vector_cosine_ops) WITH (lists = 10);

CREATE INDEX idx_schema_chunks_tenant ON schema_chunks (tenant_id);
