-- relationship_chunks: FK relationship descriptions embedded for semantic join-path retrieval
CREATE TABLE relationship_chunks (
    tenant_id         SMALLINT NOT NULL DEFAULT 1,
    chunk_name        VARCHAR(150) NOT NULL,
    from_table        VARCHAR(100) NOT NULL,
    to_table          VARCHAR(100) NOT NULL,
    relationship_text TEXT NOT NULL,
    schema_hash       VARCHAR(64) NOT NULL,
    embedding         vector(768) NOT NULL,
    updated_at        TIMESTAMPTZ DEFAULT CURRENT_TIMESTAMP,

    PRIMARY KEY (tenant_id, chunk_name)
);

CREATE INDEX idx_relationship_chunks_embedding
    ON relationship_chunks USING ivfflat (embedding vector_cosine_ops) WITH (lists = 10);

CREATE INDEX idx_relationship_chunks_tenant ON relationship_chunks (tenant_id);

-- metadata_catalog: per-column sample values embedded for filter suggestion retrieval
CREATE TABLE metadata_catalog (
    tenant_id     SMALLINT NOT NULL DEFAULT 1,
    table_name    VARCHAR(100) NOT NULL,
    column_name   VARCHAR(100) NOT NULL,
    sample_values TEXT NOT NULL,
    data_type     VARCHAR(100) NOT NULL,
    schema_hash   VARCHAR(64) NOT NULL,
    embedding     vector(768) NOT NULL,
    updated_at    TIMESTAMPTZ DEFAULT CURRENT_TIMESTAMP,

    PRIMARY KEY (tenant_id, table_name, column_name)
);

CREATE INDEX idx_metadata_catalog_embedding
    ON metadata_catalog USING ivfflat (embedding vector_cosine_ops) WITH (lists = 10);

CREATE INDEX idx_metadata_catalog_tenant ON metadata_catalog (tenant_id);

-- query_patterns table removed — SQL generation uses only real schema RAG context
-- (schema_chunks, relationship_chunks, metadata_catalog) with no hardcoded templates.
