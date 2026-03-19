CREATE TABLE IF NOT EXISTS performance_files (
    id SERIAL PRIMARY KEY,
    performance_id INTEGER NOT NULL REFERENCES performances(id) ON DELETE CASCADE,
    file_name TEXT NOT NULL,
    stored_file_name TEXT NOT NULL,
    content_type TEXT NOT NULL,
    file_size BIGINT NOT NULL,
    uploaded_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    created_by TEXT
);
CREATE INDEX IF NOT EXISTS idx_performance_files_performance_id ON performance_files(performance_id);
