-- Group instrumentations (e.g. "Full Orchestra", "String Quartet") are reference data that
-- can be linked many-to-many with arrangements, the same way games are.
CREATE TABLE IF NOT EXISTS group_instrumentations (
    id SERIAL PRIMARY KEY,
    name TEXT NOT NULL,
    description TEXT,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    created_by TEXT
);
CREATE UNIQUE INDEX IF NOT EXISTS ux_group_instrumentations_name_lower ON group_instrumentations (LOWER(name));

CREATE TABLE IF NOT EXISTS arrangement_group_instrumentations (
    arrangement_id INTEGER NOT NULL REFERENCES arrangements(id) ON DELETE CASCADE,
    group_instrumentation_id INTEGER NOT NULL REFERENCES group_instrumentations(id) ON DELETE CASCADE,
    PRIMARY KEY (arrangement_id, group_instrumentation_id)
);
CREATE INDEX IF NOT EXISTS ix_arrangement_group_instrumentations_group_instrumentation_id
    ON arrangement_group_instrumentations(group_instrumentation_id);
