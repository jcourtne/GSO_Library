CREATE TABLE IF NOT EXISTS arrangement_ensembles (
    arrangement_id INTEGER NOT NULL REFERENCES arrangements(id) ON DELETE CASCADE,
    ensemble_id    INTEGER NOT NULL REFERENCES ensembles(id)   ON DELETE CASCADE,
    PRIMARY KEY (arrangement_id, ensemble_id)
);
