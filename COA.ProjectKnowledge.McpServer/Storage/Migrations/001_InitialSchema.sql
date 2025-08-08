-- Knowledge table with JSON documents for flexibility
CREATE TABLE IF NOT EXISTS knowledge (
    id TEXT PRIMARY KEY,
    type TEXT NOT NULL,
    content TEXT NOT NULL,
    code_snippets TEXT, -- JSON array of code snippets
    metadata TEXT,      -- JSON object for flexible fields
    workspace TEXT NOT NULL,
    created_at INTEGER NOT NULL,
    modified_at INTEGER NOT NULL,
    accessed_at INTEGER,
    access_count INTEGER DEFAULT 0,
    is_archived BOOLEAN DEFAULT 0
);

-- Relationships between knowledge entries
CREATE TABLE IF NOT EXISTS relationships (
    from_id TEXT NOT NULL,
    to_id TEXT NOT NULL,
    relationship_type TEXT NOT NULL,
    metadata TEXT, -- JSON object for relationship metadata
    created_at INTEGER NOT NULL,
    PRIMARY KEY (from_id, to_id, relationship_type),
    FOREIGN KEY (from_id) REFERENCES knowledge(id) ON DELETE CASCADE,
    FOREIGN KEY (to_id) REFERENCES knowledge(id) ON DELETE CASCADE
);

-- Indexes for performance
CREATE INDEX IF NOT EXISTS idx_knowledge_type ON knowledge(type);
CREATE INDEX IF NOT EXISTS idx_knowledge_workspace ON knowledge(workspace);
CREATE INDEX IF NOT EXISTS idx_knowledge_created ON knowledge(created_at DESC);
CREATE INDEX IF NOT EXISTS idx_knowledge_modified ON knowledge(modified_at DESC);
CREATE INDEX IF NOT EXISTS idx_knowledge_accessed ON knowledge(accessed_at DESC);
CREATE INDEX IF NOT EXISTS idx_relationships_from ON relationships(from_id);
CREATE INDEX IF NOT EXISTS idx_relationships_to ON relationships(to_id);

-- Full-text search virtual table
CREATE VIRTUAL TABLE IF NOT EXISTS knowledge_fts USING fts5(
    id UNINDEXED,
    content,
    type,
    metadata,
    tokenize='porter unicode61'
);

-- Trigger to keep FTS in sync
CREATE TRIGGER IF NOT EXISTS knowledge_fts_insert AFTER INSERT ON knowledge BEGIN
    INSERT INTO knowledge_fts(id, content, type, metadata)
    VALUES (new.id, new.content, new.type, new.metadata);
END;

CREATE TRIGGER IF NOT EXISTS knowledge_fts_update AFTER UPDATE ON knowledge BEGIN
    UPDATE knowledge_fts 
    SET content = new.content, type = new.type, metadata = new.metadata
    WHERE id = new.id;
END;

CREATE TRIGGER IF NOT EXISTS knowledge_fts_delete AFTER DELETE ON knowledge BEGIN
    DELETE FROM knowledge_fts WHERE id = old.id;
END;