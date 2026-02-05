-- Application tables (non-Identity)
-- Run against the gso_library database after EF migrations have created the Identity tables.

CREATE TABLE IF NOT EXISTS series (
    id SERIAL PRIMARY KEY,
    name TEXT NOT NULL,
    description TEXT
);

CREATE TABLE IF NOT EXISTS games (
    id SERIAL PRIMARY KEY,
    name TEXT NOT NULL,
    description TEXT,
    series_id INTEGER NOT NULL REFERENCES series(id) ON DELETE CASCADE
);
CREATE INDEX IF NOT EXISTS ix_games_series_id ON games(series_id);

CREATE TABLE IF NOT EXISTS arrangements (
    id SERIAL PRIMARY KEY,
    name TEXT NOT NULL,
    description TEXT,
    arranger TEXT,
    composer TEXT,
    key TEXT,
    duration_seconds INTEGER,
    year INTEGER
);

CREATE TABLE IF NOT EXISTS arrangement_files (
    id SERIAL PRIMARY KEY,
    file_name TEXT NOT NULL,
    stored_file_name TEXT NOT NULL,
    content_type TEXT NOT NULL,
    file_size BIGINT NOT NULL,
    uploaded_at TIMESTAMPTZ NOT NULL,
    arrangement_id INTEGER NOT NULL REFERENCES arrangements(id) ON DELETE CASCADE
);
CREATE INDEX IF NOT EXISTS ix_arrangement_files_arrangement_id ON arrangement_files(arrangement_id);

CREATE TABLE IF NOT EXISTS instruments (
    id SERIAL PRIMARY KEY,
    name TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS performances (
    id SERIAL PRIMARY KEY,
    name TEXT NOT NULL,
    link TEXT NOT NULL,
    performance_date TIMESTAMPTZ,
    notes TEXT
);

CREATE TABLE IF NOT EXISTS arrangement_games (
    arrangement_id INTEGER NOT NULL REFERENCES arrangements(id) ON DELETE CASCADE,
    game_id INTEGER NOT NULL REFERENCES games(id) ON DELETE CASCADE,
    PRIMARY KEY (arrangement_id, game_id)
);
CREATE INDEX IF NOT EXISTS ix_arrangement_games_game_id ON arrangement_games(game_id);

CREATE TABLE IF NOT EXISTS arrangement_instruments (
    arrangement_id INTEGER NOT NULL REFERENCES arrangements(id) ON DELETE CASCADE,
    instrument_id INTEGER NOT NULL REFERENCES instruments(id) ON DELETE CASCADE,
    PRIMARY KEY (arrangement_id, instrument_id)
);
CREATE INDEX IF NOT EXISTS ix_arrangement_instruments_instrument_id ON arrangement_instruments(instrument_id);

CREATE TABLE IF NOT EXISTS arrangement_performances (
    arrangement_id INTEGER NOT NULL REFERENCES arrangements(id) ON DELETE CASCADE,
    performance_id INTEGER NOT NULL REFERENCES performances(id) ON DELETE CASCADE,
    PRIMARY KEY (arrangement_id, performance_id)
);
CREATE INDEX IF NOT EXISTS ix_arrangement_performances_performance_id ON arrangement_performances(performance_id);
