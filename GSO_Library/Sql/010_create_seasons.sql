-- 010: Create seasons table with arrangement and performance junction tables

CREATE TABLE IF NOT EXISTS seasons (
    id              SERIAL PRIMARY KEY,
    name            VARCHAR(255) NOT NULL,
    ensemble_id     INTEGER NOT NULL REFERENCES ensembles(id) ON DELETE RESTRICT,
    start_date      DATE,
    end_date        DATE,
    notes           TEXT,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at      TIMESTAMPTZ NOT NULL DEFAULT now(),
    created_by      VARCHAR(255)
);

CREATE INDEX IF NOT EXISTS idx_seasons_ensemble_id ON seasons(ensemble_id);

CREATE TABLE IF NOT EXISTS season_arrangements (
    season_id       INTEGER NOT NULL REFERENCES seasons(id) ON DELETE CASCADE,
    arrangement_id  INTEGER NOT NULL REFERENCES arrangements(id) ON DELETE CASCADE,
    PRIMARY KEY (season_id, arrangement_id)
);

CREATE INDEX IF NOT EXISTS idx_season_arrangements_arrangement_id ON season_arrangements(arrangement_id);

CREATE TABLE IF NOT EXISTS season_performances (
    season_id       INTEGER NOT NULL REFERENCES seasons(id) ON DELETE CASCADE,
    performance_id  INTEGER NOT NULL REFERENCES performances(id) ON DELETE CASCADE,
    PRIMARY KEY (season_id, performance_id)
);

CREATE INDEX IF NOT EXISTS idx_season_performances_performance_id ON season_performances(performance_id);
