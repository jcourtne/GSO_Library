-- 004: Create ensembles table and add ensemble_id FK to performances

CREATE TABLE IF NOT EXISTS ensembles (
    id              SERIAL PRIMARY KEY,
    name            VARCHAR(255) NOT NULL,
    description     TEXT,
    website         VARCHAR(500),
    contact_info    TEXT,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at      TIMESTAMPTZ NOT NULL DEFAULT now(),
    created_by      VARCHAR(255)
);

-- Add optional ensemble FK to performances
ALTER TABLE performances ADD COLUMN IF NOT EXISTS ensemble_id INTEGER REFERENCES ensembles(id) ON DELETE SET NULL;

CREATE INDEX IF NOT EXISTS idx_performances_ensemble_id ON performances(ensemble_id);
