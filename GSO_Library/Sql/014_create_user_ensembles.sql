CREATE TABLE IF NOT EXISTS user_ensembles (
    user_id     TEXT    NOT NULL REFERENCES "AspNetUsers"("Id") ON DELETE CASCADE,
    ensemble_id INTEGER NOT NULL REFERENCES ensembles(id)     ON DELETE CASCADE,
    PRIMARY KEY (user_id, ensemble_id)
);

CREATE INDEX IF NOT EXISTS idx_user_ensembles_ensemble_id ON user_ensembles(ensemble_id);
